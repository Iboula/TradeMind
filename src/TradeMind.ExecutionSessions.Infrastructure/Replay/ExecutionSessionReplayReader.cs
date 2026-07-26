using TradeMind.ExecutionSessions.Application;
using TradeMind.ExecutionSessions.Application.Abstractions;
using TradeMind.ExecutionSessions.Application.DTOs;
using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.ExecutionSessions.Infrastructure.Replay;

public sealed class ExecutionSessionReplayReader(IExecutionSessionRepository repository) : IExecutionSessionReplayReader
{
    public async Task<ExecutionSessionReplayManifestDto?> GetManifestAsync(
        ExecutionSessionId sessionId,
        CancellationToken cancellationToken)
    {
        var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        return session is null ? null : ExecutionSessionReplayManifestBuilder.Build(session);
    }
}
