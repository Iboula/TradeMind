using TradeMind.ExecutionSessions.Application.DTOs;
using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.ExecutionSessions.Application.Abstractions;

public interface IExecutionSessionReplayReader
{
    Task<ExecutionSessionReplayManifestDto?> GetManifestAsync(
        ExecutionSessionId sessionId,
        CancellationToken cancellationToken);
}
