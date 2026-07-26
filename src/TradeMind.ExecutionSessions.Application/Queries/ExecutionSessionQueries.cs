using MediatR;
using TradeMind.ExecutionSessions.Application.DTOs;
using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.ExecutionSessions.Application.Queries;

public sealed record GetExecutionSessionQuery(ExecutionSessionId SessionId) : IRequest<ExecutionSessionDto>;

public sealed record GetExecutionSessionTimelineQuery(ExecutionSessionId SessionId)
    : IRequest<IReadOnlyList<ExecutionSessionTimelineDto>>;

public sealed record SearchExecutionSessionsQuery(ExecutionSessionSearchFilter Filter)
    : IRequest<ExecutionSessionSearchPage>;

public sealed record GetExecutionSessionReplayManifestQuery(ExecutionSessionId SessionId)
    : IRequest<ExecutionSessionReplayManifestDto>;
