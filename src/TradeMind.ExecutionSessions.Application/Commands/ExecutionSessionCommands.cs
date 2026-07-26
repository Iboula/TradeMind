using MediatR;
using TradeMind.ExecutionSessions.Application.DTOs;
using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.ExecutionSessions.Application.Commands;

public sealed record StartExecutionSessionCommand(
    ExecutionSessionId? SessionId,
    string CorrelationId,
    string Instrument,
    string Timeframe,
    ExecutionSessionTriggerType TriggerType,
    string Source,
    string CoreVersion,
    string ApiVersion,
    DateTimeOffset? StartedAtUtc,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? IdempotencyKeyHash = null,
    string? TenantId = null,
    string? UserId = null,
    int SchemaVersion = 1) : IRequest<ExecutionSessionDto>;

public sealed record LinkExecutionArtifactCommand(
    ExecutionSessionId SessionId,
    ExecutionSessionArtifactReference Artifact,
    long ExpectedConcurrencyVersion) : IRequest<ExecutionSessionDto>;

public sealed record AdvanceExecutionStageCommand(
    ExecutionSessionId SessionId,
    ExecutionSessionStage TargetStage,
    DateTimeOffset? OccurredAtUtc,
    IReadOnlyDictionary<string, string>? Metadata,
    long ExpectedConcurrencyVersion) : IRequest<ExecutionSessionDto>;

public sealed record CompleteExecutionSessionCommand(
    ExecutionSessionId SessionId,
    DateTimeOffset? CompletedAtUtc,
    long ExpectedConcurrencyVersion) : IRequest<ExecutionSessionDto>;

public sealed record FailExecutionSessionCommand(
    ExecutionSessionId SessionId,
    ExecutionSessionFailure Failure,
    DateTimeOffset? OccurredAtUtc,
    long ExpectedConcurrencyVersion) : IRequest<ExecutionSessionDto>;

public sealed record CancelExecutionSessionCommand(
    ExecutionSessionId SessionId,
    DateTimeOffset? OccurredAtUtc,
    long ExpectedConcurrencyVersion) : IRequest<ExecutionSessionDto>;
