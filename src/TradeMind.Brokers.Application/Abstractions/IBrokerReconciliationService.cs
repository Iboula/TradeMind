using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Abstractions;

public enum BrokerReconciliationMismatchType
{
    MissingBrokerOrder,
    UnknownExternalOrder,
    StatusMismatch,
    QuantityMismatch,
    PriceMismatch,
    MissingFill,
    DuplicateExecution,
    PositionMismatch,
    StaleData,
    Unsupported
}

public sealed record BrokerReconciliationMismatch(BrokerReconciliationMismatchType Type, string Reference, string Description);

public sealed record BrokerReconciliationReport(
    BrokerReconciliationId ReconciliationId,
    BrokerConnectorId ConnectorId,
    BrokerAccountId AccountId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<BrokerReconciliationMismatch> Mismatches,
    BrokerError? Error)
{
    public bool IsConsistent => Error is null && Mismatches.Count == 0;
}

public interface IBrokerReconciliationService
{
    Task<BrokerReconciliationReport> ReconcileAsync(BrokerExecutionContext context, BrokerConnectorId connectorId, BrokerAccountId accountId, CancellationToken cancellationToken);
}

public enum BrokerPositionClassification
{
    Known,
    Orphaned,
    Unreconciled,
    Stale,
    UnknownExternal
}

public sealed record BrokerKnownPositionReference(
    BrokerPositionId PositionId,
    string? ExecutionSessionId,
    bool ExecutionTerminallyCompleted,
    DateTimeOffset ObservedAtUtc);

public sealed record BrokerPositionAlert(
    BrokerPositionId PositionId,
    BrokerPositionClassification Classification,
    string Reason,
    DateTimeOffset ObservedAtUtc);

public sealed record BrokerOrphanPositionReport(
    BrokerConnectorId ConnectorId,
    BrokerAccountId AccountId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<BrokerPositionAlert> Alerts)
{
    public bool IsSafeForExecution => Alerts.All(item => item.Classification == BrokerPositionClassification.Known);
    public bool HasUnsafePositions => Alerts.Any(item => item.Classification != BrokerPositionClassification.Known);
}

public interface IBrokerOrphanPositionDetector
{
    Task<BrokerOrphanPositionReport> DetectAsync(
        BrokerExecutionContext context,
        BrokerConnectorId connectorId,
        BrokerAccountId accountId,
        IReadOnlyCollection<BrokerKnownPositionReference> knownPositions,
        CancellationToken cancellationToken);
}
