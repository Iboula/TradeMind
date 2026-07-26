using MediatR;
using TradeMind.ExecutionSessions.Application.Commands;
using TradeMind.ExecutionSessions.Application.DTOs;
using TradeMind.ExecutionSessions.Application.Queries;
using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.ExecutionSessions.Application;

public interface IExecutionSessionService
{
    Task<ExecutionSessionDto> StartAsync(StartExecutionSessionCommand command, CancellationToken cancellationToken);
    Task<ExecutionSessionDto> LinkArtifactAsync(LinkExecutionArtifactCommand command, CancellationToken cancellationToken);
    Task<ExecutionSessionDto> AdvanceStageAsync(AdvanceExecutionStageCommand command, CancellationToken cancellationToken);
    Task<ExecutionSessionDto> CompleteAsync(CompleteExecutionSessionCommand command, CancellationToken cancellationToken);
    Task<ExecutionSessionDto> FailAsync(FailExecutionSessionCommand command, CancellationToken cancellationToken);
    Task<ExecutionSessionDto> CancelAsync(CancelExecutionSessionCommand command, CancellationToken cancellationToken);
    Task<ExecutionSessionDto> GetAsync(ExecutionSessionId sessionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExecutionSessionTimelineDto>> GetTimelineAsync(ExecutionSessionId sessionId, CancellationToken cancellationToken);
    Task<ExecutionSessionSearchPage> SearchAsync(ExecutionSessionSearchFilter filter, CancellationToken cancellationToken);
    Task<ExecutionSessionReplayManifestDto> GetReplayManifestAsync(ExecutionSessionId sessionId, CancellationToken cancellationToken);
}

public sealed class ExecutionSessionService(ISender sender) : IExecutionSessionService
{
    public Task<ExecutionSessionDto> StartAsync(StartExecutionSessionCommand command, CancellationToken cancellationToken) => sender.Send(command, cancellationToken);
    public Task<ExecutionSessionDto> LinkArtifactAsync(LinkExecutionArtifactCommand command, CancellationToken cancellationToken) => sender.Send(command, cancellationToken);
    public Task<ExecutionSessionDto> AdvanceStageAsync(AdvanceExecutionStageCommand command, CancellationToken cancellationToken) => sender.Send(command, cancellationToken);
    public Task<ExecutionSessionDto> CompleteAsync(CompleteExecutionSessionCommand command, CancellationToken cancellationToken) => sender.Send(command, cancellationToken);
    public Task<ExecutionSessionDto> FailAsync(FailExecutionSessionCommand command, CancellationToken cancellationToken) => sender.Send(command, cancellationToken);
    public Task<ExecutionSessionDto> CancelAsync(CancelExecutionSessionCommand command, CancellationToken cancellationToken) => sender.Send(command, cancellationToken);
    public Task<ExecutionSessionDto> GetAsync(ExecutionSessionId sessionId, CancellationToken cancellationToken) => sender.Send(new GetExecutionSessionQuery(sessionId), cancellationToken);
    public Task<IReadOnlyList<ExecutionSessionTimelineDto>> GetTimelineAsync(ExecutionSessionId sessionId, CancellationToken cancellationToken) => sender.Send(new GetExecutionSessionTimelineQuery(sessionId), cancellationToken);
    public Task<ExecutionSessionSearchPage> SearchAsync(ExecutionSessionSearchFilter filter, CancellationToken cancellationToken) => sender.Send(new SearchExecutionSessionsQuery(filter), cancellationToken);
    public Task<ExecutionSessionReplayManifestDto> GetReplayManifestAsync(ExecutionSessionId sessionId, CancellationToken cancellationToken) => sender.Send(new GetExecutionSessionReplayManifestQuery(sessionId), cancellationToken);
}
