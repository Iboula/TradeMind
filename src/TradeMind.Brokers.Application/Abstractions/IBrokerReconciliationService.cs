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
