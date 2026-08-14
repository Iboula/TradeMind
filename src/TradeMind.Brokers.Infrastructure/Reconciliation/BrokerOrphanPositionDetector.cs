using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Application.Options;
using TradeMind.Brokers.Domain;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.Infrastructure.Reconciliation;

public sealed class BrokerOrphanPositionDetector(
    IBrokerConnectorRegistry registry,
    IBrokerClock clock,
    IOptions<BrokerOptions> options,
    ITradeMindTelemetry telemetry,
    ITradeMindMetrics metrics,
    IBrokerAuditWriter auditWriter,
    IBrokerExecutionSafetyState safetyState) : IBrokerOrphanPositionDetector
{
    public async Task<BrokerOrphanPositionReport> DetectAsync(
        BrokerExecutionContext context,
        BrokerConnectorId connectorId,
        BrokerAccountId accountId,
        IReadOnlyCollection<BrokerKnownPositionReference> knownPositions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(connectorId);
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(knownPositions);

        using var activity = telemetry.StartActivity(
            new TelemetryOperation("TradeMind.Brokers.OrphanDetection", "TradeMind.Brokers", TelemetryStage.Broker),
            TelemetryContext.System() with { Module = "Brokers", Operation = "OrphanDetection", CorrelationId = context.CorrelationId ?? "broker-orphan-detection" });
        var started = clock.UtcNow;
        var connector = registry.GetRequired(connectorId);
        var positions = await connector.GetPositionsAsync(context, new BrokerPositionQuery(), cancellationToken).ConfigureAwait(false);
        var references = knownPositions.ToDictionary(item => item.PositionId.Value, StringComparer.Ordinal);
        var alerts = positions
            .Where(item => item.AccountId == accountId)
            .Select(position => Classify(position, references, started))
            .OrderBy(item => item.PositionId.Value, StringComparer.Ordinal)
            .ToArray();
        var report = new BrokerOrphanPositionReport(connectorId, accountId, started, clock.UtcNow, alerts);

        foreach (var alert in alerts)
        {
            metrics.IncrementCounter(TelemetryMetricNames.BrokerOrphanPositionsDetected, 1, Dimensions(alert.Classification.ToString()));
            await auditWriter.WriteAsync(new BrokerAuditEntry(
                null,
                "OrphanPositionDetection",
                alert.Classification.ToString(),
                BrokerPermissionNames.ReadPositions,
                connector.Descriptor.Mode,
                context.ActorId,
                context.TenantId,
                context.OrganizationId,
                connectorId,
                accountId,
                context.ExecutionSessionId,
                null,
                context.CorrelationId,
                clock.UtcNow,
                new Dictionary<string, string>
                {
                    ["position_classification"] = alert.Classification.ToString(),
                    ["position_reference"] = alert.PositionId.Value
                }), cancellationToken).ConfigureAwait(false);
        }

        if (report.HasUnsafePositions)
        {
            safetyState.Block("BROKER_POSITION_REVIEW_REQUIRED", "Broker positions require explicit reconciliation before new writes.");
            activity.SetOutcome(TelemetryOutcome.Degraded);
        }
        else
        {
            activity.SetOutcome(TelemetryOutcome.Succeeded);
        }

        return report;
    }

    private BrokerPositionAlert Classify(
        BrokerPosition position,
        IReadOnlyDictionary<string, BrokerKnownPositionReference> references,
        DateTimeOffset now)
    {
        if (!references.TryGetValue(position.PositionId.Value, out var reference))
            return new(position.PositionId, BrokerPositionClassification.UnknownExternal, "No known execution reference exists for the broker position.", now);
        if (string.IsNullOrWhiteSpace(reference.ExecutionSessionId))
            return new(position.PositionId, BrokerPositionClassification.Orphaned, "The position has no active execution session reference.", now);
        if (reference.ExecutionTerminallyCompleted)
            return new(position.PositionId, BrokerPositionClassification.Unreconciled, "The referenced execution session is terminally completed.", now);
        if (now - position.UpdatedAtUtc > TimeSpan.FromSeconds(options.Value.OrphanPositionStaleAfterSeconds))
            return new(position.PositionId, BrokerPositionClassification.Stale, "The known position reference is older than the configured freshness window.", now);
        return new(position.PositionId, BrokerPositionClassification.Known, "The broker position matches an active execution reference.", now);
    }

    private static MetricDimensions Dimensions(string classification) => new(Module: "Brokers", Operation: "OrphanDetection", Stage: "Broker", Outcome: classification);
}
