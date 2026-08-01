using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Abstractions;

public sealed record BrokerAuditEntry(
    BrokerExecutionId? ExecutionId,
    string Operation,
    string Outcome,
    string? Permission,
    BrokerExecutionMode Mode,
    string? ActorId,
    string? TenantId,
    string? OrganizationId,
    BrokerConnectorId? ConnectorId,
    BrokerAccountId? AccountId,
    string? ExecutionSessionId,
    string? TradingPlanId,
    string? CorrelationId,
    DateTimeOffset TimestampUtc,
    IReadOnlyDictionary<string, string> Metadata);

public interface IBrokerAuditWriter
{
    Task WriteAsync(BrokerAuditEntry entry, CancellationToken cancellationToken);
}
