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
    ILogger<BrokerExecutionService> logger) : IBrokerExecutionService, IDisposable
{
    private readonly SemaphoreSlim _concurrencyGate = new(options.Value.MaximumConcurrentExecutions, options.Value.MaximumConcurrentExecutions);

    public async Task<BrokerExecutionResult> SubmitOrderAsync(BrokerExecutionContext context, BrokerOrderExecutionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        var request = command.Order;
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
            await executionRecordWriter.WriteAsync(command, result, cancellationToken).ConfigureAwait(false);
            await WriteAuditAsync(context, request, command.RequestedMode, result, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var timeout = Rejected(request, Error(request, BrokerErrorCategory.Timeout, "The broker operation timed out.", true));
            await executionRecordWriter.WriteAsync(command, timeout with { Status = BrokerExecutionResultStatus.Failed }, CancellationToken.None).ConfigureAwait(false);
            await WriteAuditAsync(context, request, command.RequestedMode, timeout, CancellationToken.None).ConfigureAwait(false);
            return timeout with { Status = BrokerExecutionResultStatus.Failed };
        }
        finally
        {
            linked.Dispose();
        }
    }

    public async Task<BrokerOrderModificationResult> ModifyOrderAsync(BrokerExecutionContext context, BrokerConnectorId connectorId, BrokerOrderModificationRequest request, CancellationToken cancellationToken)
    {
        var connector = registry.GetRequired(connectorId);
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
        if (!options.Value.Enabled) return new(null, null, Error(new BrokerExecutionId(request.PositionId.Value), BrokerErrorCategory.ConnectorUnavailable, "Broker execution is disabled.", false, context));
        var authorization = authorizationPolicy.Evaluate(context, connector.Descriptor.Mode, "ClosePosition");
        if (!authorization.IsAllowed) return new(null, null, Error(new BrokerExecutionId(request.PositionId.Value), BrokerErrorCategory.Authorization, authorization.SafeReason!, false, context));
        if (!connector.Descriptor.Capabilities.HasFlag(BrokerCapability.ClosePositions)) return new(null, null, Error(new BrokerExecutionId(request.PositionId.Value), BrokerErrorCategory.UnsupportedCapability, "Position closing is not supported.", false, context));
        await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await connector.ClosePositionAsync(context, request, cancellationToken).ConfigureAwait(false); }
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

    private BrokerExecutionResult Rejected(BrokerOrderRequest request, BrokerError error) => new(request.ExecutionId, BrokerExecutionResultStatus.Rejected, null, null, error, "SubmitOrder", clock.UtcNow);
    private BrokerError Error(BrokerExecutionId executionId, BrokerErrorCategory category, string message, bool retryable, BrokerExecutionContext context) => new(category.ToString().ToUpperInvariant(), category, message, retryable, false, null, new BrokerTraceReference(context.CorrelationId, null, context.ExecutionSessionId), clock.UtcNow);
    private BrokerError Error(BrokerOrderRequest request, BrokerErrorCategory category, string message, bool retryable) => Error(request.ExecutionId, category, message, retryable, new BrokerExecutionContext(true, request.ActorId, null, request.TenantId, request.OrganizationId, [], request.ExecutionSessionId, request.CorrelationId));
    private static string BuildRequestHash(BrokerOrderRequest request) => string.Join("|", request.ConnectorId.Value, request.AccountId.Value, request.ClientOrderId, request.Instrument, request.Side, request.OrderType, request.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture), request.RequestedPrice, request.StopPrice, request.StopLoss, string.Join(",", request.TakeProfits), request.TimeInForce, request.ExpirationUtc?.ToString("O"), request.TradingPlanId, request.RiskAssessmentId);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static MetricDimensions Dimensions(string outcome) => new(Module: "Brokers", Operation: "SubmitOrder", Stage: "Persistence", Outcome: outcome);

    public void Dispose() => _concurrencyGate.Dispose();
}
