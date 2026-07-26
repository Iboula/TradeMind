namespace TradeMind.ExecutionSessions.Infrastructure.Persistence.Entities;

public sealed class ExecutionSessionEntity
{
    public Guid Id { get; set; }
    public string CorrelationId { get; set; } = null!;
    public string? IdempotencyKeyHash { get; set; }
    public string? TenantId { get; set; }
    public string? UserId { get; set; }
    public string Instrument { get; set; } = null!;
    public string Timeframe { get; set; } = null!;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string Status { get; set; } = null!;
    public string CurrentStage { get; set; } = null!;
    public int SchemaVersion { get; set; }
    public string CoreVersion { get; set; } = null!;
    public string ApiVersion { get; set; } = null!;
    public string TriggerType { get; set; } = null!;
    public string Source { get; set; } = null!;
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string? FailureDetails { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public long ConcurrencyVersion { get; set; }
    public List<ExecutionSessionArtifactEntity> Artifacts { get; set; } = [];
    public List<ExecutionSessionTimelineEntity> Timeline { get; set; } = [];
}

public sealed class ExecutionSessionArtifactEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string ArtifactType { get; set; } = null!;
    public string ArtifactId { get; set; } = null!;
    public string Stage { get; set; } = null!;
    public int SchemaVersion { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string ContentHash { get; set; } = null!;
    public string? StorageReference { get; set; }
    public bool IsReplayable { get; set; }
    public string MetadataJson { get; set; } = "{}";
}

public sealed class ExecutionSessionTimelineEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string EventType { get; set; } = null!;
    public string Stage { get; set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string MetadataJson { get; set; } = "{}";
}

public sealed class ExecutionSessionAuditEntity
{
    public Guid AuditId { get; set; }
    public Guid SessionId { get; set; }
    public string EventType { get; set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string CorrelationId { get; set; } = null!;
    public string ActorType { get; set; } = null!;
    public string? ActorId { get; set; }
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? PreviousStage { get; set; }
    public string? NewStage { get; set; }
    public string? ArtifactId { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public int SchemaVersion { get; set; }
}

public sealed class ExecutionSessionOutboxEntity
{
    public Guid OutboxMessageId { get; set; }
    public Guid SessionId { get; set; }
    public string EventType { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public long ConcurrencyVersion { get; set; }
}

public sealed class DurableIdempotencyRecordEntity
{
    public string IdempotencyKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid? ExecutionSessionId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public int? ResponseStatusCode { get; set; }
    public string? ResponseContentType { get; set; }
    public byte[]? ResponseBody { get; set; }
    public long ConcurrencyVersion { get; set; }
}
