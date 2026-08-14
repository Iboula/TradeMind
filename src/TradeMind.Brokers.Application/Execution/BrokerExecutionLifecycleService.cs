using Microsoft.Extensions.Logging;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Domain;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.Application.Execution;

public sealed record BrokerExecutionLifecycleResult(
    BrokerExecutionResult Execution,
    BrokerReconciliationReport? Reconciliation,
    BrokerPositionCloseResult? Cleanup,
    BrokerError? OriginalFailure,
    Exception? OriginalException,
    bool CleanupCritical)
{
    public bool Succeeded => Execution.Succeeded && OriginalFailure is null && !CleanupCritical;
    public bool WritesBlocked => CleanupCritical;
}

public interface IBrokerExecutionLifecycleService
{
    Task<BrokerExecutionLifecycleResult> ExecuteAndReconcileAsync(
        BrokerExecutionContext context,
        BrokerOrderExecutionCommand command,
        CancellationToken cancellationToken);
}

public sealed class BrokerExecutionLifecycleService(
    IBrokerExecutionService executionService,
    IBrokerReconciliationService reconciliationService,
    IBrokerExecutionSafetyState safetyState,
    IBrokerAuditWriter auditWriter,
    IBrokerClock clock,
    ITradeMindTelemetry telemetry,
    ITradeMindMetrics metrics,
    ILogger<BrokerExecutionLifecycleService> logger) : IBrokerExecutionLifecycleService
{
    public async Task<BrokerExecutionLifecycleResult> ExecuteAndReconcileAsync(
        BrokerExecutionContext context,
        BrokerOrderExecutionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        using var activity = telemetry.StartActivity(
            new TelemetryOperation("TradeMind.Brokers.ExecutionLifecycle", "TradeMind.Brokers", TelemetryStage.Broker),
            TelemetryContext.System() with { Module = "Brokers", Operation = "ExecutionLifecycle", CorrelationId = context.CorrelationId ?? "broker-lifecycle" });

        var execution = await executionService.SubmitOrderAsync(context, command, cancellationToken).ConfigureAwait(false);
        if (!execution.Succeeded || execution.Execution?.PositionId is null)
        {
            activity.SetOutcome(execution.Error is null ? TelemetryOutcome.Rejected : TelemetryOutcome.Failed);
            return new(execution, null, null, execution.Error, null, false);
        }

        BrokerReconciliationReport? reconciliation = null;
        BrokerError? originalFailure = null;
        Exception? originalException = null;
        try
        {
            reconciliation = await reconciliationService.ReconcileAsync(context, command.Order.ConnectorId, command.Order.AccountId, cancellationToken).ConfigureAwait(false);
            if (!reconciliation.IsConsistent)
            {
                originalFailure = reconciliation.Error ?? Error(command, "RECONCILIATION_MISMATCH", "Broker reconciliation reported a mismatch.");
                metrics.IncrementCounter(TelemetryMetricNames.BrokerReconciliationMismatches, reconciliation.Mismatches.Count, Dimensions("Mismatch"));
            }
            else
            {
                metrics.IncrementCounter(TelemetryMetricNames.BrokerReconciliationRuns, 1, Dimensions("Succeeded"));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            originalException = exception;
            originalFailure = Error(command, "RECONCILIATION_FAILED", "Broker reconciliation failed and cleanup was required.");
            logger.LogWarning(exception, "Broker reconciliation failed for execution {ExecutionId}.", command.Order.ExecutionId);
            metrics.IncrementCounter(TelemetryMetricNames.BrokerReconciliationRuns, 1, Dimensions("Failed"));
        }

        if (originalFailure is null)
        {
            activity.SetOutcome(TelemetryOutcome.Succeeded);
            return new(execution, reconciliation, null, null, null, false);
        }

        await auditWriter.WriteAsync(Audit(context, command, "Reconciliation", "Failed"), CancellationToken.None).ConfigureAwait(false);
        var cleanup = await executionService.ClosePositionAsync(context, command.Order.ConnectorId, new BrokerPositionCloseRequest(execution.Execution.PositionId, null), CancellationToken.None).ConfigureAwait(false);
        if (cleanup.Error is not null || cleanup.Execution is null)
        {
            safetyState.Block("CLEANUP_FAILED", "A broker position could not be closed after reconciliation failure.");
            metrics.IncrementCounter(TelemetryMetricNames.BrokerCleanupFailures, 1, Dimensions("Critical"));
            await auditWriter.WriteAsync(Audit(context, command, "Cleanup", "Critical"), CancellationToken.None).ConfigureAwait(false);
            activity.SetOutcome(TelemetryOutcome.Failed);
            return new(execution, reconciliation, cleanup, originalFailure, originalException, true);
        }

        metrics.IncrementCounter(TelemetryMetricNames.BrokerPositionsClosed, 1, Dimensions("Cleanup"));
        await auditWriter.WriteAsync(Audit(context, command, "Cleanup", "Succeeded"), CancellationToken.None).ConfigureAwait(false);
        activity.SetOutcome(TelemetryOutcome.Degraded);
        return new(execution, reconciliation, cleanup, originalFailure, originalException, false);
    }

    private BrokerAuditEntry Audit(BrokerExecutionContext context, BrokerOrderExecutionCommand command, string operation, string outcome) => new(
        command.Order.ExecutionId,
        operation,
        outcome,
        command.RequestedMode == BrokerExecutionMode.Demo ? BrokerPermissionNames.ExecuteDemo : BrokerPermissionNames.ExecuteSimulation,
        command.RequestedMode,
        context.ActorId,
        context.TenantId,
        context.OrganizationId,
        command.Order.ConnectorId,
        command.Order.AccountId,
        command.Order.ExecutionSessionId,
        command.Order.TradingPlanId,
        context.CorrelationId,
        clock.UtcNow,
        new Dictionary<string, string> { ["client_order_id"] = command.Order.ClientOrderId });

    private static BrokerError Error(BrokerOrderExecutionCommand command, string code, string message) => new(
        code,
        BrokerErrorCategory.ReconciliationRequired,
        message,
        true,
        true,
        null,
        new BrokerTraceReference(command.Order.CorrelationId, null, command.Order.ExecutionSessionId),
        command.Order.CreatedAtUtc);

    private static MetricDimensions Dimensions(string outcome) => new(Module: "Brokers", Operation: "ExecutionLifecycle", Stage: "Broker", Outcome: outcome);
}
