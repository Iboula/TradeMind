using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Options;
using TradeMind.Brokers.Domain;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.Application.Execution;

public sealed class BrokerExecutionService(
    IBrokerConnectorRegistry registry,
    BrokerExecutionValidator validator,
    IBrokerAuthorizationPolicy authorizationPolicy,
    IBrokerIdempotencyStore idempotencyStore,
    IBrokerExecutionRecordWriter executionRecordWriter,
    IBrokerAuditWriter auditWriter,
    IBrokerClock clock,
    ITradeMindTelemetry telemetry,
    ITradeMindMetrics metrics,
    IOptions<BrokerOptions> options,
    ILogger<BrokerExecutionService> logger,
    IBrokerExecutionSafetyState? safetyState = null) : IBrokerExecutionService, IDisposable
{
    private readonly SemaphoreSlim _concurrencyGate = new(options.Value.MaximumConcurrentExecutions, options.Value.MaximumConcurrentExecutions);
    private readonly IBrokerExecutionSafetyState executionSafetyState = safetyState ?? new InMemoryBrokerExecutionSafetyState();

    public async Task<BrokerExecutionResult> SubmitOrderAsync(BrokerExecutionContext context, BrokerOrderExecutionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        var request = command.Order;
        var safety = executionSafetyState.Snapshot;
        if (!safety.WritesAllowed)
        {
            var blocked = Rejected(request, Error(request, BrokerErrorCategory.ConnectorUnavailable, safety.Code ?? "BROKER_EXECUTION_BLOCKED", safety.Reason ?? "Broker writes are blocked until safety review completes.", false, true));
            metrics.IncrementCounter(TelemetryMetricNames.BrokerExecutionBlocked, 1, Dimensions("Blocked"));
            await executionRecordWriter.WriteAsync(command, blocked, cancellationToken).ConfigureAwait(false);
            await WriteAuditAsync(context, request, command.RequestedMode, blocked, cancellationToken).ConfigureAwait(false);
            return blocked;
        }
        var connector = registry.GetRequired(request.ConnectorId);
        var eligibility = await validator.ValidateAsync(context, command, connector, cancellationToken).ConfigureAwait(false);
        if (!eligibility.IsEligible)
        {
            var rejected = Rejected(request, eligibility.Error!);
            await executionRecordWriter.WriteAsync(command, rejected, cancellationToken).ConfigureAwait(false);
            await WriteAuditAsync(context, request, command.RequestedMode, rejected, cancellationToken).ConfigureAwait(false);
            return rejected;
        }

        var idempotencyKey = Hash($"{request.TenantId}|{request.ConnectorId}|{request.AccountId}|SubmitOrder|{request.IdempotencyKey}");
        var requestHash = Hash(BuildRequestHash(request));
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(options.Value.OperationTimeout);
        try
        {
            var idempotent = await idempotencyStore.ExecuteAsync(idempotencyKey, requestHash, token => ExecuteConnectorAsync(context, command, connector, token), linked.Token).ConfigureAwait(false);
            var result = idempotent.IsReplay ? idempotent.Result with { Status = BrokerExecutionResultStatus.Replayed } : idempotent.Result;
            if (idempotent.IsConflict) result = result with { Status = BrokerExecutionResultStatus.Conflict };
            if (idempotent.IsInProgress) result = result with { Status = BrokerExecutionResultStatus.InProgress };
            if (idempotent.IsReplay) metrics.IncrementCounter(TelemetryMetricNames.BrokerIdempotencyHits, 1, Dimensions("Replay"));
            if (idempotent.IsConflict) metrics.IncrementCounter(TelemetryMetricNames.BrokerIdempotencyConflicts, 1, Dimensions("Conflict"));
            await executionRecordWriter.WriteAsync(command, result, cancellationToken).ConfigureAwait(false);
            await WriteAuditAsync(context, request, command.RequestedMode, result, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var recovered = await TryRecoverAfterTimeoutAsync(context, command, connector, idempotencyKey, requestHash).ConfigureAwait(false);
            if (recovered is not null)
            {
                await executionRecordWriter.WriteAsync(command, recovered, CancellationToken.None).ConfigureAwait(false);
                await WriteAuditAsync(context, request, command.RequestedMode, recovered, CancellationToken.None).ConfigureAwait(false);
                return recovered;
            }

            var timeout = Rejected(request, Error(request, BrokerErrorCategory.Timeout, "The broker operation timed out and its outcome is unknown; reconciliation is required and no retry is permitted.", true, true)) with { Status = BrokerExecutionResultStatus.ExecutionUnknown };
            await idempotencyStore.MarkExecutionUnknownAsync(idempotencyKey, requestHash, timeout, CancellationToken.None).ConfigureAwait(false);
            metrics.IncrementCounter(TelemetryMetricNames.ExecutionUnknown, 1, Dimensions("ExecutionUnknown"));
            await executionRecordWriter.WriteAsync(command, timeout, CancellationToken.None).ConfigureAwait(false);
            await WriteAuditAsync(context, request, command.RequestedMode, timeout, CancellationToken.None).ConfigureAwait(false);
            return timeout;
        }
        finally
        {
            linked.Dispose();
        }
    }

    public async Task<BrokerOrderModificationResult> ModifyOrderAsync(BrokerExecutionContext context, BrokerConnectorId connectorId, BrokerOrderModificationRequest request, CancellationToken cancellationToken)
    {
        var connector = registry.GetRequired(connectorId);
        var safetyError = ExecutionSafetyError(context, new BrokerExecutionId(request.OrderId.Value));
        if (safetyError is not null) return new(null, safetyError);
        if (!options.Value.Enabled) return new(null, Error(new BrokerExecutionId(request.OrderId.Value), BrokerErrorCategory.ConnectorUnavailable, "Broker execution is disabled.", false, context));
        var authorization = authorizationPolicy.Evaluate(context, connector.Descriptor.Mode, "ModifyOrder");
        if (!authorization.IsAllowed) return new(null, Error(new BrokerExecutionId(request.OrderId.Value), BrokerErrorCategory.Authorization, authorization.SafeReason!, false, context));
        if (!connector.Descriptor.Capabilities.HasFlag(BrokerCapability.ModifyOrders)) return new(null, Error(new BrokerExecutionId(request.OrderId.Value), BrokerErrorCategory.UnsupportedCapability, "Order modification is not supported.", false, context));
        await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await connector.ModifyOrderAsync(context, request, cancellationToken).ConfigureAwait(false); }
        finally { _concurrencyGate.Release(); }
    }

    public async Task<BrokerOrderCancellationResult> CancelOrderAsync(BrokerExecutionContext context, BrokerConnectorId connectorId, BrokerOrderCancellationRequest request, CancellationToken cancellationToken)
    {
        var connector = registry.GetRequired(connectorId);
        var safetyError = ExecutionSafetyError(context, new BrokerExecutionId(request.OrderId.Value));
        if (safetyError is not null) return new(null, safetyError);
        if (!options.Value.Enabled) return new(null, Error(new BrokerExecutionId(request.OrderId.Value), BrokerErrorCategory.ConnectorUnavailable, "Broker execution is disabled.", false, context));
        var authorization = authorizationPolicy.Evaluate(context, connector.Descriptor.Mode, "CancelOrder");
        if (!authorization.IsAllowed) return new(null, Error(new BrokerExecutionId(request.OrderId.Value), BrokerErrorCategory.Authorization, authorization.SafeReason!, false, context));
        if (!connector.Descriptor.Capabilities.HasFlag(BrokerCapability.CancelOrders)) return new(null, Error(new BrokerExecutionId(request.OrderId.Value), BrokerErrorCategory.UnsupportedCapability, "Order cancellation is not supported.", false, context));
        await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await connector.CancelOrderAsync(context, request, cancellationToken).ConfigureAwait(false); }
        finally { _concurrencyGate.Release(); }
    }

    public async Task<BrokerPositionCloseResult> ClosePositionAsync(BrokerExecutionContext context, BrokerConnectorId connectorId, BrokerPositionCloseRequest request, CancellationToken cancellationToken)
    {
        var connector = registry.GetRequired(connectorId);
        var safetyError = ExecutionSafetyError(context, new BrokerExecutionId(request.PositionId.Value));
        if (safetyError is not null)
        {
            var blocked = new BrokerPositionCloseResult(null, null, safetyError);
            await WriteCloseAuditAsync(context, connector, request, blocked, cancellationToken).ConfigureAwait(false);
            return blocked;
        }
        if (!options.Value.Enabled) return new(null, null, Error(new BrokerExecutionId(request.PositionId.Value), BrokerErrorCategory.ConnectorUnavailable, "Broker execution is disabled.", false, context));
        var authorization = authorizationPolicy.Evaluate(context, connector.Descriptor.Mode, "ClosePosition");
        if (!authorization.IsAllowed)
        {
            var denied = new BrokerPositionCloseResult(null, null, Error(new BrokerExecutionId(request.PositionId.Value), BrokerErrorCategory.Authorization, authorization.SafeReason!, false, context));
            await WriteCloseAuditAsync(context, connector, request, denied, cancellationToken).ConfigureAwait(false);
            return denied;
        }
        if (!connector.Descriptor.Capabilities.HasFlag(BrokerCapability.ClosePositions))
        {
            var unsupported = new BrokerPositionCloseResult(null, null, Error(new BrokerExecutionId(request.PositionId.Value), BrokerErrorCategory.UnsupportedCapability, "Position closing is not supported.", false, context));
            await WriteCloseAuditAsync(context, connector, request, unsupported, cancellationToken).ConfigureAwait(false);
            return unsupported;
        }
        await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await connector.ClosePositionAsync(context, request, cancellationToken).ConfigureAwait(false);
            await WriteCloseAuditAsync(context, connector, request, result, cancellationToken).ConfigureAwait(false);
            if (result.Error is null && result.Execution is not null) metrics.IncrementCounter(TelemetryMetricNames.BrokerPositionsClosed, 1, Dimensions("Closed"));
            return result;
        }
        finally { _concurrencyGate.Release(); }
    }

    private async Task<BrokerExecutionResult> ExecuteConnectorAsync(BrokerExecutionContext context, BrokerOrderExecutionCommand command, IBrokerConnector connector, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var request = command.Order;
        await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var activity = telemetry.StartActivity(new TelemetryOperation("TradeMind.Brokers.SubmitOrder", "TradeMind.Brokers", TelemetryStage.Persistence, "BrokerExecution", 1), TelemetryContext.System() with { CorrelationId = request.CorrelationId, ExecutionSessionId = request.ExecutionSessionId, TenantId = request.TenantId, ActorId = request.ActorId, Module = "Brokers", Operation = "SubmitOrder" });
            var response = await connector.SubmitOrderAsync(context, request, cancellationToken).ConfigureAwait(false);
            var status = response.Error is null ? BrokerExecutionResultStatus.Accepted : BrokerExecutionResultStatus.Rejected;
            var result = new BrokerExecutionResult(request.ExecutionId, status, response.Order, response.Execution, response.Error, "SubmitOrder", clock.UtcNow, command.RequestedMode == BrokerExecutionMode.Live);
            activity.SetOutcome(response.Error is null ? TelemetryOutcome.Succeeded : TelemetryOutcome.Rejected);
            metrics.IncrementCounter(TelemetryMetricNames.BrokerOperations, 1, Dimensions(status.ToString()));
            metrics.IncrementCounter(response.Error is null ? TelemetryMetricNames.BrokerOrdersSubmitted : TelemetryMetricNames.BrokerOrdersRejected, 1, Dimensions(status.ToString()));
            if (response.Execution is not null)
            {
                metrics.IncrementCounter(TelemetryMetricNames.BrokerOrdersFilled, 1, Dimensions("Filled"));
                metrics.IncrementCounter(TelemetryMetricNames.BrokerPositionsOpened, 1, Dimensions("Opened"));
            }
            metrics.RecordDuration(TelemetryMetricNames.BrokerOperationDuration, Stopwatch.GetElapsedTime(started), Dimensions(status.ToString()));
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Broker connector operation failed for execution {ExecutionId}.", request.ExecutionId.Value);
            var error = BrokerErrorMapper.FromException(exception, context, request.ExecutionSessionId, clock.UtcNow);
            metrics.IncrementCounter(TelemetryMetricNames.BrokerFailures, 1, Dimensions(error.Category.ToString()));
            return new BrokerExecutionResult(request.ExecutionId, BrokerExecutionResultStatus.Failed, null, null, error, "SubmitOrder", clock.UtcNow, command.RequestedMode == BrokerExecutionMode.Live);
        }
        finally { _concurrencyGate.Release(); }
    }

    private async Task WriteAuditAsync(BrokerExecutionContext context, BrokerOrderRequest request, BrokerExecutionMode mode, BrokerExecutionResult result, CancellationToken cancellationToken)
    {
        await auditWriter.WriteAsync(new BrokerAuditEntry(result.ExecutionId, result.Operation, result.Status.ToString(), mode == BrokerExecutionMode.Live ? BrokerPermissionNames.ExecuteLive : BrokerPermissionNames.ExecuteSimulation, mode, context.ActorId, context.TenantId, context.OrganizationId, request.ConnectorId, request.AccountId, request.ExecutionSessionId, request.TradingPlanId, context.CorrelationId, clock.UtcNow, new Dictionary<string, string> { ["client_order_id"] = request.ClientOrderId }), cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteCloseAuditAsync(BrokerExecutionContext context, IBrokerConnector connector, BrokerPositionCloseRequest request, BrokerPositionCloseResult result, CancellationToken cancellationToken)
    {
        await auditWriter.WriteAsync(new BrokerAuditEntry(
            result.Execution?.ExecutionId,
            "ClosePosition",
            result.Error?.Code ?? (result.Execution is null ? "Failed" : "Succeeded"),
            connector.Descriptor.Mode == BrokerExecutionMode.Live ? BrokerPermissionNames.ExecuteLive : BrokerPermissionNames.ClosePositions,
            connector.Descriptor.Mode,
            context.ActorId,
            context.TenantId,
            context.OrganizationId,
            connector.Descriptor.ConnectorId,
            result.Position?.AccountId,
            context.ExecutionSessionId,
            null,
            context.CorrelationId,
            clock.UtcNow,
            new Dictionary<string, string> { ["position_reference"] = request.PositionId.Value }), cancellationToken).ConfigureAwait(false);
    }

    private async Task<BrokerExecutionResult?> TryRecoverAfterTimeoutAsync(
        BrokerExecutionContext context,
        BrokerOrderExecutionCommand command,
        IBrokerConnector connector,
        string idempotencyKey,
        string requestHash)
    {
        using var timeout = new CancellationTokenSource(options.Value.OperationTimeout);
        try
        {
            var orders = await connector.GetOrdersAsync(context, new BrokerOrderQuery(command.Order.ClientOrderId), timeout.Token).ConfigureAwait(false);
            var order = orders.FirstOrDefault(item => item.ClientOrderId.Equals(command.Order.ClientOrderId, StringComparison.Ordinal));
            if (order is null) return null;

            BrokerPosition? position = null;
            if (order.FilledQuantity > 0)
            {
                var positions = await connector.GetPositionsAsync(context, new BrokerPositionQuery(order.Instrument), timeout.Token).ConfigureAwait(false);
                position = positions.FirstOrDefault(item => item.AccountId == order.AccountId && item.Instrument.Equals(order.Instrument, StringComparison.OrdinalIgnoreCase) && item.Side == order.Side);
            }

            var execution = order.FilledQuantity > 0
                ? new BrokerExecution(order.ExecutionId, order.OrderId, order.ConnectorId, order.AccountId, BrokerOrderStatus.Filled, order.FilledQuantity, order.AverageFillPrice, order.UpdatedAtUtc, positionId: position?.PositionId)
                : null;
            var recovered = new BrokerExecutionResult(command.Order.ExecutionId, BrokerExecutionResultStatus.Accepted, order, execution, null, "SubmitOrderReconciled", clock.UtcNow, command.RequestedMode == BrokerExecutionMode.Live);
            var persisted = await idempotencyStore.ExecuteAsync(idempotencyKey, requestHash, _ => Task.FromResult(recovered), CancellationToken.None).ConfigureAwait(false);
            metrics.IncrementCounter(TelemetryMetricNames.BrokerReconciliationRuns, 1, Dimensions("TimeoutRecovered"));
            return persisted.Result with { Status = persisted.IsReplay ? BrokerExecutionResultStatus.Replayed : persisted.IsConflict ? BrokerExecutionResultStatus.Conflict : persisted.Result.Status };
        }
        catch (OperationCanceledException)
        {
            metrics.IncrementCounter(TelemetryMetricNames.BrokerReconciliationRuns, 1, Dimensions("TimeoutUnresolved"));
            return null;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Broker timeout reconciliation failed for execution {ExecutionId}.", command.Order.ExecutionId);
            metrics.IncrementCounter(TelemetryMetricNames.BrokerReconciliationRuns, 1, Dimensions("TimeoutUnresolved"));
            return null;
        }
    }

    private BrokerExecutionResult Rejected(BrokerOrderRequest request, BrokerError error) => new(request.ExecutionId, BrokerExecutionResultStatus.Rejected, null, null, error, "SubmitOrder", clock.UtcNow);
    private BrokerError? ExecutionSafetyError(BrokerExecutionContext context, BrokerExecutionId executionId)
    {
        var safety = executionSafetyState.Snapshot;
        if (safety.WritesAllowed) return null;
        metrics.IncrementCounter(TelemetryMetricNames.BrokerExecutionBlocked, 1, Dimensions("Blocked"));
        return Error(executionId, BrokerErrorCategory.ConnectorUnavailable, safety.Code ?? "BROKER_EXECUTION_BLOCKED", safety.Reason ?? "Broker writes are blocked until safety review completes.", false, true, context);
    }
    private BrokerError Error(BrokerExecutionId executionId, BrokerErrorCategory category, string message, bool retryable, BrokerExecutionContext context) => Error(executionId, category, category.ToString().ToUpperInvariant(), message, retryable, false, context);
    private BrokerError Error(BrokerExecutionId executionId, BrokerErrorCategory category, string code, string message, bool retryable, bool requiresReconciliation, BrokerExecutionContext context) => new(code, category, message, retryable, requiresReconciliation, null, new BrokerTraceReference(context.CorrelationId, null, context.ExecutionSessionId), clock.UtcNow);
    private BrokerError Error(BrokerOrderRequest request, BrokerErrorCategory category, string message, bool retryable, bool requiresReconciliation = false) => Error(request.ExecutionId, category, category.ToString().ToUpperInvariant(), message, retryable, requiresReconciliation, new BrokerExecutionContext(true, request.ActorId, null, request.TenantId, request.OrganizationId, [], request.ExecutionSessionId, request.CorrelationId));
    private BrokerError Error(BrokerOrderRequest request, BrokerErrorCategory category, string code, string message, bool retryable, bool requiresReconciliation) => Error(request.ExecutionId, category, code, message, retryable, requiresReconciliation, new BrokerExecutionContext(true, request.ActorId, null, request.TenantId, request.OrganizationId, [], request.ExecutionSessionId, request.CorrelationId));
    private static string BuildRequestHash(BrokerOrderRequest request) => string.Join("|", request.ConnectorId.Value, request.AccountId.Value, request.ClientOrderId, request.Instrument, request.Side, request.OrderType, request.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture), request.RequestedPrice, request.StopPrice, request.StopLoss, string.Join(",", request.TakeProfits), request.TimeInForce, request.ExpirationUtc?.ToString("O"), request.TradingPlanId, request.RiskAssessmentId);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static MetricDimensions Dimensions(string outcome) => new(Module: "Brokers", Operation: "SubmitOrder", Stage: "Persistence", Outcome: outcome);

    public void Dispose() => _concurrencyGate.Dispose();
}
