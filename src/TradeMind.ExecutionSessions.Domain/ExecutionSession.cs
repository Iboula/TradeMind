namespace TradeMind.ExecutionSessions.Domain;

public sealed class ExecutionSession
{
    private readonly List<ExecutionSessionArtifactReference> _artifactReferences;
    private readonly List<ExecutionSessionTimelineEntry> _timelineEntries;

    private ExecutionSession(
        ExecutionSessionId id,
        ExecutionCorrelationId correlationId,
        string? idempotencyKeyHash,
        string? tenantId,
        string? userId,
        string? organizationId,
        string? createdByActorId,
        string? createdByActorType,
        string instrument,
        string timeframe,
        DateTimeOffset startedAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? completedAtUtc,
        ExecutionSessionStatus status,
        ExecutionSessionStage currentStage,
        int schemaVersion,
        string coreVersion,
        string apiVersion,
        ExecutionSessionTriggerType triggerType,
        string source,
        ExecutionSessionFailure? failure,
        IReadOnlyDictionary<string, string>? metadata,
        IEnumerable<ExecutionSessionArtifactReference>? artifactReferences,
        IEnumerable<ExecutionSessionTimelineEntry>? timelineEntries,
        long concurrencyVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(instrument);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeframe);
        ArgumentException.ThrowIfNullOrWhiteSpace(coreVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (startedAtUtc.Offset != TimeSpan.Zero || updatedAtUtc.Offset != TimeSpan.Zero
            || completedAtUtc is { } completed && completed.Offset != TimeSpan.Zero)
            throw new ArgumentException("Execution session timestamps must be UTC.");
        if (updatedAtUtc < startedAtUtc || completedAtUtc < startedAtUtc)
            throw new ArgumentException("Execution session timestamps are inconsistent.");
        if (schemaVersion <= 0 || concurrencyVersion < 0)
            throw new ArgumentOutOfRangeException(schemaVersion <= 0 ? nameof(schemaVersion) : nameof(concurrencyVersion));

        Id = id;
        CorrelationId = correlationId;
        IdempotencyKeyHash = NormalizeOptional(idempotencyKeyHash, 128);
        TenantId = NormalizeOptional(tenantId, 128);
        UserId = NormalizeOptional(userId, 128);
        OrganizationId = NormalizeOptional(organizationId, 128);
        CreatedByActorId = NormalizeOptional(createdByActorId, 128);
        CreatedByActorType = NormalizeOptional(createdByActorType, 32);
        Instrument = NormalizeRequired(instrument, 128, nameof(instrument));
        Timeframe = NormalizeRequired(timeframe, 32, nameof(timeframe));
        StartedAtUtc = startedAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Status = status;
        CurrentStage = currentStage;
        SchemaVersion = schemaVersion;
        CoreVersion = NormalizeRequired(coreVersion, 64, nameof(coreVersion));
        ApiVersion = NormalizeRequired(apiVersion, 64, nameof(apiVersion));
        TriggerType = triggerType;
        Source = NormalizeRequired(source, 128, nameof(source));
        Failure = failure;
        Metadata = ExecutionSessionMetadata.Copy(metadata);
        _artifactReferences = artifactReferences?.ToList() ?? [];
        _timelineEntries = timelineEntries?.ToList() ?? [];
        ConcurrencyVersion = concurrencyVersion;
        ValidateCollections();
    }

    public ExecutionSessionId Id { get; }
    public ExecutionCorrelationId CorrelationId { get; }
    public string? IdempotencyKeyHash { get; }
    public string? TenantId { get; }
    public string? UserId { get; }
    public string? OrganizationId { get; }
    public string? CreatedByActorId { get; }
    public string? CreatedByActorType { get; }
    public string Instrument { get; }
    public string Timeframe { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public ExecutionSessionStatus Status { get; private set; }
    public ExecutionSessionStage CurrentStage { get; private set; }
    public int SchemaVersion { get; }
    public string CoreVersion { get; }
    public string ApiVersion { get; }
    public ExecutionSessionTriggerType TriggerType { get; }
    public string Source { get; }
    public ExecutionSessionFailure? Failure { get; private set; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    public IReadOnlyList<ExecutionSessionArtifactReference> ArtifactReferences => _artifactReferences.AsReadOnly();
    public IReadOnlyList<ExecutionSessionTimelineEntry> TimelineEntries => _timelineEntries.AsReadOnly();
    public long ConcurrencyVersion { get; private set; }

    public static ExecutionSession Start(
        ExecutionSessionId id,
        ExecutionCorrelationId correlationId,
        string instrument,
        string timeframe,
        ExecutionSessionTriggerType triggerType,
        string source,
        string coreVersion,
        string apiVersion,
        DateTimeOffset startedAtUtc,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? idempotencyKeyHash = null,
        string? tenantId = null,
        string? userId = null,
        int schemaVersion = 1,
        string? organizationId = null,
        string? createdByActorId = null,
        string? createdByActorType = null) =>
        new(
            id,
            correlationId,
            idempotencyKeyHash,
            tenantId,
            userId,
            organizationId,
            createdByActorId,
            createdByActorType,
            instrument,
            timeframe,
            startedAtUtc,
            startedAtUtc,
            null,
            ExecutionSessionStatus.Created,
            ExecutionSessionStage.MarketContext,
            schemaVersion,
            coreVersion,
            apiVersion,
            triggerType,
            source,
            null,
            metadata,
            null,
            null,
            0);

    public static ExecutionSession Rehydrate(
        ExecutionSessionId id,
        ExecutionCorrelationId correlationId,
        string? idempotencyKeyHash,
        string? tenantId,
        string? userId,
        string instrument,
        string timeframe,
        DateTimeOffset startedAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? completedAtUtc,
        ExecutionSessionStatus status,
        ExecutionSessionStage currentStage,
        int schemaVersion,
        string coreVersion,
        string apiVersion,
        ExecutionSessionTriggerType triggerType,
        string source,
        ExecutionSessionFailure? failure,
        IReadOnlyDictionary<string, string>? metadata,
        IEnumerable<ExecutionSessionArtifactReference> artifactReferences,
        IEnumerable<ExecutionSessionTimelineEntry> timelineEntries,
        long concurrencyVersion,
        string? organizationId = null,
        string? createdByActorId = null,
        string? createdByActorType = null) =>
        new(id, correlationId, idempotencyKeyHash, tenantId, userId, organizationId, createdByActorId, createdByActorType, instrument, timeframe, startedAtUtc, updatedAtUtc,
            completedAtUtc, status, currentStage, schemaVersion, coreVersion, apiVersion, triggerType, source, failure,
            metadata, artifactReferences, timelineEntries, concurrencyVersion);

    public void Begin(DateTimeOffset occurredAtUtc)
    {
        EnsureUtc(occurredAtUtc);
        EnsureTimestamp(occurredAtUtc);
        if (Status != ExecutionSessionStatus.Created)
            throw new ExecutionSessionDomainException("Only a created execution session can start.");
        Status = ExecutionSessionStatus.Running;
        Touch(occurredAtUtc, "SessionStarted", CurrentStage);
    }

    public void AdvanceStage(
        ExecutionSessionStage targetStage,
        DateTimeOffset occurredAtUtc,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        EnsureMutableRunning();
        EnsureUtc(occurredAtUtc);
        EnsureTimestamp(occurredAtUtc);
        if (targetStage == ExecutionSessionStage.Completed || targetStage < CurrentStage)
            throw new ExecutionSessionDomainException("Execution session stages must progress monotonically.");
        if (targetStage == CurrentStage)
        {
            Touch(occurredAtUtc, "StageObserved", targetStage, metadata);
            return;
        }

        var previous = CurrentStage;
        CurrentStage = targetStage;
        Touch(occurredAtUtc, "StageAdvanced", targetStage, metadata ?? new Dictionary<string, string>
        {
            ["previousStage"] = previous.ToString()
        });
    }

    public void LinkArtifact(ExecutionSessionArtifactReference artifact, DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        EnsureMutableRunning();
        EnsureUtc(occurredAtUtc);
        EnsureTimestamp(occurredAtUtc);
        var existing = _artifactReferences.FirstOrDefault(item =>
            item.ArtifactType == artifact.ArtifactType && item.ArtifactId == artifact.ArtifactId);
        if (existing is not null)
            throw new ExecutionSessionDomainException("An artifact with the same type and id is already linked.");
        if (_artifactReferences.Any(item => item.ArtifactId == artifact.ArtifactId))
            throw new ExecutionSessionDomainException("An artifact id cannot be reused across incompatible artifact types.");
        if (artifact.Stage < CurrentStage)
            throw new ExecutionSessionDomainException("Artifact stage is incompatible with the current session stage.");
        _artifactReferences.Add(artifact);
        Touch(occurredAtUtc, "ArtifactLinked", artifact.Stage, new Dictionary<string, string>
        {
            ["artifactType"] = artifact.ArtifactType.ToString(),
            ["artifactId"] = artifact.ArtifactId.Value,
            ["contentHash"] = artifact.ContentHash
        });
    }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        EnsureMutableRunning();
        EnsureUtc(completedAtUtc);
        EnsureTimestamp(completedAtUtc);
        Status = ExecutionSessionStatus.Completed;
        CurrentStage = ExecutionSessionStage.Completed;
        CompletedAtUtc = completedAtUtc;
        Touch(completedAtUtc, "SessionCompleted", CurrentStage);
    }

    public void Fail(ExecutionSessionFailure failure, DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(failure);
        EnsureMutableRunning();
        EnsureUtc(occurredAtUtc);
        EnsureTimestamp(occurredAtUtc);
        Failure = failure;
        Status = ExecutionSessionStatus.Failed;
        Touch(occurredAtUtc, "SessionFailed", CurrentStage, new Dictionary<string, string> { ["failureCode"] = failure.Code });
    }

    public void Cancel(DateTimeOffset occurredAtUtc)
    {
        EnsureMutableRunning();
        EnsureUtc(occurredAtUtc);
        EnsureTimestamp(occurredAtUtc);
        Status = ExecutionSessionStatus.Cancelled;
        Touch(occurredAtUtc, "SessionCancelled", CurrentStage);
    }

    private void EnsureMutableRunning()
    {
        if (Status != ExecutionSessionStatus.Running)
            throw new ExecutionSessionDomainException("Only a running execution session can be changed.");
    }

    private void EnsureTimestamp(DateTimeOffset timestamp)
    {
        if (timestamp < UpdatedAtUtc)
            throw new ExecutionSessionDomainException("Execution session timestamps must be monotonic.");
    }

    private void Touch(
        DateTimeOffset occurredAtUtc,
        string eventType,
        ExecutionSessionStage stage,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        UpdatedAtUtc = occurredAtUtc;
        ConcurrencyVersion++;
        _timelineEntries.Add(new ExecutionSessionTimelineEntry(Guid.NewGuid(), eventType, stage, occurredAtUtc, metadata));
    }

    private void ValidateCollections()
    {
        if (_artifactReferences.GroupBy(item => item.ArtifactId).Any(group => group.Count() > 1))
            throw new ArgumentException("Execution session contains duplicate artifact ids.");
        if (_timelineEntries.Any(item => item.OccurredAtUtc < StartedAtUtc))
            throw new ArgumentException("Timeline entries cannot predate the session.");
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value cannot exceed {maxLength} characters.");
        return normalized;
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(value));
    }
}
