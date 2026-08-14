namespace TradeMind.Brokers.Infrastructure.Persistence.Entities;

public sealed class BrokerExecutionEntity
{
    public string Id { get; set; } = null!;
    public string ConnectorId { get; set; } = null!;
    public string AccountId { get; set; } = null!;
    public string? TenantId { get; set; }
    public string? ExecutionSessionId { get; set; }
    public string Status { get; set; } = null!;
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class BrokerOrderEntity
{
    public string Id { get; set; } = null!;
    public string ExecutionId { get; set; } = null!;
    public string ConnectorId { get; set; } = null!;
    public string AccountId { get; set; } = null!;
    public string? TenantId { get; set; }
    public string ClientOrderId { get; set; } = null!;
    public string Instrument { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal FilledQuantity { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public long ConcurrencyVersion { get; set; }
}

public sealed class BrokerPositionEntity
{
    public string Id { get; set; } = null!;
    public string ConnectorId { get; set; } = null!;
    public string AccountId { get; set; } = null!;
    public string? TenantId { get; set; }
    public string Instrument { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public DateTimeOffset OpenedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public long ConcurrencyVersion { get; set; }
}

public sealed class BrokerFillEntity
{
    public string Id { get; set; } = null!;
    public string ExecutionId { get; set; } = null!;
    public string OrderId { get; set; } = null!;
    public string? PositionId { get; set; }
    public string ConnectorId { get; set; } = null!;
    public string AccountId { get; set; } = null!;
    public string? TenantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTimeOffset FilledAtUtc { get; set; }
}

public sealed class BrokerIdempotencyEntity
{
    public string KeyHash { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? ResultJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public long ConcurrencyVersion { get; set; }
}

public sealed class BrokerAuditEntity
{
    public Guid Id { get; set; }
    public string Operation { get; set; } = null!;
    public string Outcome { get; set; } = null!;
    public string Mode { get; set; } = null!;
    public string? ActorId { get; set; }
    public string? TenantId { get; set; }
    public string? OrganizationId { get; set; }
    public string? ConnectorId { get; set; }
    public string? AccountId { get; set; }
    public string? ExecutionSessionId { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public string MetadataJson { get; set; } = "{}";
}

public sealed class BrokerReconciliationEntity
{
    public string Id { get; set; } = null!;
    public string ConnectorId { get; set; } = null!;
    public string AccountId { get; set; } = null!;
    public string? TenantId { get; set; }
    public bool IsConsistent { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
}

public sealed class BrokerReconciliationMismatchEntity
{
    public long Id { get; set; }
    public string ReconciliationId { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Reference { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public sealed class BrokerKillSwitchEntity
{
    public string Key { get; set; } = null!;
    public string Scope { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string State { get; set; } = null!;
    public string? ChangedBy { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
    public string Reason { get; set; } = null!;
}

public sealed class BrokerExecutionQuarantineEntity
{
    public string ExecutionId { get; set; } = null!;
    public string TenantId { get; set; } = null!;
    public string BrokerId { get; set; } = null!;
    public string AccountId { get; set; } = null!;
    public string Instrument { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public string State { get; set; } = null!;
    public DateTimeOffset ChangedAtUtc { get; set; }
    public string ChangedBy { get; set; } = null!;
    public string Explanation { get; set; } = null!;
}

public sealed class BrokerPositionOwnershipEntity
{
    public string PositionId { get; set; } = null!;
    public string ExecutionSessionId { get; set; } = null!;
    public string BrokerExecutionId { get; set; } = null!;
    public string TradingPlanId { get; set; } = null!;
    public string RiskAssessmentId { get; set; } = null!;
    public string TenantId { get; set; } = null!;
    public string BrokerAccountId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string Direction { get; set; } = null!;
    public DateTimeOffset OpenedAtUtc { get; set; }
    public string Source { get; set; } = null!;
    public string ReconciliationState { get; set; } = null!;
    public long ConcurrencyVersion { get; set; }
}
