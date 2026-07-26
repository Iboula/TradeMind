using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.ExecutionSessions.Application.DTOs;

public sealed record ExecutionSessionArtifactDto(
    string ArtifactType,
    string ArtifactId,
    string Stage,
    int SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    string ContentHash,
    string? StorageReference,
    bool IsReplayable,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ExecutionSessionTimelineDto(
    Guid TimelineId,
    string EventType,
    string Stage,
    DateTimeOffset OccurredAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ExecutionSessionFailureDto(string Code, string Message, string? Details);

public sealed record ExecutionSessionDto(
    string Id,
    string CorrelationId,
    string? IdempotencyKeyHash,
    string? TenantId,
    string? UserId,
    string Instrument,
    string Timeframe,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string Status,
    string CurrentStage,
    int SchemaVersion,
    string CoreVersion,
    string ApiVersion,
    string TriggerType,
    string Source,
    ExecutionSessionFailureDto? Failure,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<ExecutionSessionArtifactDto> ArtifactReferences,
    IReadOnlyList<ExecutionSessionTimelineDto> TimelineEntries,
    long ConcurrencyVersion);

public sealed record ExecutionSessionSearchItem(
    string Id,
    string CorrelationId,
    string Instrument,
    string Timeframe,
    string Status,
    string CurrentStage,
    string TriggerType,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int ArtifactCount);

public sealed record ExecutionSessionSearchPage(
    IReadOnlyList<ExecutionSessionSearchItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ExecutionSessionSearchFilter
{
    public ExecutionSessionSearchFilter(
        ExecutionSessionStatus? status = null,
        string? instrument = null,
        DateTimeOffset? startedFromUtc = null,
        DateTimeOffset? startedToUtc = null,
        ExecutionSessionStage? stage = null,
        string? correlationId = null,
        string? artifactId = null,
        ExecutionSessionTriggerType? triggerType = null,
        int page = 1,
        int pageSize = 25)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if ((startedFromUtc is { } from && from.Offset != TimeSpan.Zero)
            || (startedToUtc is { } to && to.Offset != TimeSpan.Zero))
            throw new ArgumentException("Search timestamps must be UTC.");
        if (startedFromUtc > startedToUtc) throw new ArgumentException("Search date range is invalid.");
        Status = status;
        Instrument = string.IsNullOrWhiteSpace(instrument) ? null : instrument.Trim();
        StartedFromUtc = startedFromUtc;
        StartedToUtc = startedToUtc;
        Stage = stage;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        ArtifactId = string.IsNullOrWhiteSpace(artifactId) ? null : artifactId.Trim();
        TriggerType = triggerType;
        Page = page;
        PageSize = pageSize;
    }

    public ExecutionSessionStatus? Status { get; }
    public string? Instrument { get; }
    public DateTimeOffset? StartedFromUtc { get; }
    public DateTimeOffset? StartedToUtc { get; }
    public ExecutionSessionStage? Stage { get; }
    public string? CorrelationId { get; }
    public string? ArtifactId { get; }
    public ExecutionSessionTriggerType? TriggerType { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed record ExecutionSessionReplayManifestDto(
    string SessionId,
    string? SourceSessionId,
    string CoreVersion,
    IReadOnlyList<int> SchemaVersions,
    IReadOnlyList<ExecutionSessionArtifactDto> OrderedArtifacts,
    IReadOnlyList<string> MissingArtifacts,
    IReadOnlyList<string> NonReplayableReasons,
    IReadOnlyList<DateTimeOffset> OriginalTimestamps,
    string DeterministicFingerprint,
    bool IsReplayable);
