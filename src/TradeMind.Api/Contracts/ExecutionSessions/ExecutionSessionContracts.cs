using TradeMind.ExecutionSessions.Application.DTOs;
using TradeMind.Api.Contracts.Common;

namespace TradeMind.Api.Contracts.ExecutionSessions;

public sealed record CreateExecutionSessionApiRequest(
    int SchemaVersion,
    string CorrelationId,
    string Instrument,
    string Timeframe,
    string TriggerType,
    string Source,
    string CoreVersion,
    string ApiVersion,
    Guid? SessionId = null,
    DateTimeOffset? StartedAtUtc = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? IdempotencyKeyHash = null,
    string? TenantId = null,
    string? UserId = null);

public sealed record LinkExecutionArtifactApiRequest(
    int SchemaVersion,
    string ArtifactType,
    string ArtifactId,
    string Stage,
    int ArtifactSchemaVersion,
    DateTimeOffset CreatedAtUtc,
    string ContentHash,
    string? StorageReference = null,
    bool IsReplayable = true,
    IReadOnlyDictionary<string, string>? Metadata = null,
    long ExpectedConcurrencyVersion = 0);

public sealed record AdvanceExecutionStageApiRequest(
    int SchemaVersion,
    string TargetStage,
    DateTimeOffset? OccurredAtUtc = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    long ExpectedConcurrencyVersion = 0);

public sealed record ExecutionSessionMutationApiRequest(
    int SchemaVersion,
    long ExpectedConcurrencyVersion,
    DateTimeOffset? OccurredAtUtc = null);

public sealed record FailExecutionSessionApiRequest(
    int SchemaVersion,
    long ExpectedConcurrencyVersion,
    string Code,
    string Message,
    string? Details = null,
    DateTimeOffset? OccurredAtUtc = null);

public sealed record ExecutionSessionSearchApiQuery(
    string? Status = null,
    string? Instrument = null,
    DateTimeOffset? StartedFromUtc = null,
    DateTimeOffset? StartedToUtc = null,
    string? Stage = null,
    string? CorrelationId = null,
    string? ArtifactId = null,
    string? TriggerType = null,
    int Page = 1,
    int PageSize = 25);

public sealed record ExecutionSessionApiArtifact(
    string ArtifactType,
    string ArtifactId,
    string Stage,
    int SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    string ContentHash,
    string? StorageReference,
    bool IsReplayable,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ExecutionSessionApiTimeline(
    Guid TimelineId,
    string EventType,
    string Stage,
    DateTimeOffset OccurredAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ExecutionSessionApiFailure(string Code, string Message, string? Details);

public sealed record ExecutionSessionApiResource(
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
    ExecutionSessionApiFailure? Failure,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<ExecutionSessionApiArtifact> ArtifactReferences,
    IReadOnlyList<ExecutionSessionApiTimeline> TimelineEntries,
    long ConcurrencyVersion);

public sealed record ExecutionSessionApiResponse(ExecutionSessionApiResource Data, ApiResponseMetadata Metadata);

public sealed record ExecutionSessionSearchApiItem(
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

public sealed record ExecutionSessionSearchApiResponse(
    IReadOnlyList<ExecutionSessionSearchApiItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    ApiResponseMetadata Metadata);

public sealed record ExecutionSessionReplayManifestApiResponse(
    string SessionId,
    string? SourceSessionId,
    string CoreVersion,
    IReadOnlyList<int> SchemaVersions,
    IReadOnlyList<ExecutionSessionApiArtifact> OrderedArtifacts,
    IReadOnlyList<string> MissingArtifacts,
    IReadOnlyList<string> NonReplayableReasons,
    IReadOnlyList<DateTimeOffset> OriginalTimestamps,
    string DeterministicFingerprint,
    bool IsReplayable,
    ApiResponseMetadata Metadata);
