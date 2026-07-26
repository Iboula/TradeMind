namespace TradeMind.ExecutionSessions.Domain;

public enum ExecutionSessionTriggerType
{
    Api,
    Scheduled,
    Replay,
    Internal,
    Unknown
}

public enum ExecutionSessionArtifactType
{
    MarketContext,
    ExpertDispatch,
    ExpertAnalysis,
    Consensus,
    TradingDecision,
    RiskAssessment,
    TradingPlan,
    TradingWorkspace,
    TradingAssistantResponse,
    PaperTradingSimulation
}

public enum ExecutionSessionActorType
{
    System,
    User,
    Service,
    Scheduler,
    Replay
}

public enum ExecutionSessionAuditEventType
{
    SessionCreated,
    SessionStarted,
    StageAdvanced,
    ArtifactLinked,
    SessionCompleted,
    SessionFailed,
    SessionCancelled,
    ReplayRequested,
    ConcurrencyConflict,
    IdempotencyReplay,
    PersistenceFailure
}

public sealed record ExecutionSessionAuditEntry
{
    public ExecutionSessionAuditEntry(
        Guid auditId,
        ExecutionSessionId sessionId,
        ExecutionSessionAuditEventType eventType,
        DateTimeOffset occurredAtUtc,
        ExecutionCorrelationId correlationId,
        ExecutionSessionActorType actorType,
        string? actorId,
        ExecutionSessionStatus? previousStatus,
        ExecutionSessionStatus? newStatus,
        ExecutionSessionStage? previousStage,
        ExecutionSessionStage? newStage,
        string? artifactId,
        IReadOnlyDictionary<string, string> metadata,
        int schemaVersion = 1)
    {
        if (auditId == Guid.Empty) throw new ArgumentException("Audit id cannot be empty.", nameof(auditId));
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(correlationId);
        if (occurredAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Audit timestamp must be UTC.", nameof(occurredAtUtc));
        if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        AuditId = auditId;
        SessionId = sessionId;
        EventType = eventType;
        OccurredAtUtc = occurredAtUtc;
        CorrelationId = correlationId;
        ActorType = actorType;
        ActorId = string.IsNullOrWhiteSpace(actorId) ? null : actorId.Trim();
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        PreviousStage = previousStage;
        NewStage = newStage;
        ArtifactId = string.IsNullOrWhiteSpace(artifactId) ? null : artifactId.Trim();
        Metadata = ExecutionSessionMetadata.Copy(metadata);
        SchemaVersion = schemaVersion;
    }

    public Guid AuditId { get; }
    public ExecutionSessionId SessionId { get; }
    public ExecutionSessionAuditEventType EventType { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public ExecutionCorrelationId CorrelationId { get; }
    public ExecutionSessionActorType ActorType { get; }
    public string? ActorId { get; }
    public ExecutionSessionStatus? PreviousStatus { get; }
    public ExecutionSessionStatus? NewStatus { get; }
    public ExecutionSessionStage? PreviousStage { get; }
    public ExecutionSessionStage? NewStage { get; }
    public string? ArtifactId { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    public int SchemaVersion { get; }
}
