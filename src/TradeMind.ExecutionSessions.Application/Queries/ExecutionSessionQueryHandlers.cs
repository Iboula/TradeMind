using MediatR;
using Microsoft.Extensions.Logging;
using TradeMind.ExecutionSessions.Application.Abstractions;
using TradeMind.ExecutionSessions.Application.DTOs;

namespace TradeMind.ExecutionSessions.Application.Queries;

public sealed class GetExecutionSessionHandler(
    IExecutionSessionRepository repository,
    ILogger<GetExecutionSessionHandler> logger)
    : IRequestHandler<GetExecutionSessionQuery, ExecutionSessionDto>
{
    public async Task<ExecutionSessionDto> Handle(GetExecutionSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await repository.GetAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new ExecutionSessionNotFoundException(request.SessionId);
        logger.LogDebug("Execution session loaded. SessionId={SessionId}", request.SessionId);
        return ExecutionSessionMapper.ToDto(session);
    }
}

public sealed class GetExecutionSessionTimelineHandler(IExecutionSessionRepository repository)
    : IRequestHandler<GetExecutionSessionTimelineQuery, IReadOnlyList<ExecutionSessionTimelineDto>>
{
    public async Task<IReadOnlyList<ExecutionSessionTimelineDto>> Handle(
        GetExecutionSessionTimelineQuery request,
        CancellationToken cancellationToken) =>
        await repository.GetTimelineAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
}

public sealed class SearchExecutionSessionsHandler(IExecutionSessionRepository repository)
    : IRequestHandler<SearchExecutionSessionsQuery, ExecutionSessionSearchPage>
{
    public Task<ExecutionSessionSearchPage> Handle(SearchExecutionSessionsQuery request, CancellationToken cancellationToken) =>
        repository.SearchAsync(request.Filter, cancellationToken);
}

public sealed class GetExecutionSessionReplayManifestHandler(IExecutionSessionReplayReader replayReader)
    : IRequestHandler<GetExecutionSessionReplayManifestQuery, ExecutionSessionReplayManifestDto>
{
    public async Task<ExecutionSessionReplayManifestDto> Handle(
        GetExecutionSessionReplayManifestQuery request,
        CancellationToken cancellationToken)
    {
        return await replayReader.GetManifestAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new ExecutionSessionNotFoundException(request.SessionId);
    }
}
