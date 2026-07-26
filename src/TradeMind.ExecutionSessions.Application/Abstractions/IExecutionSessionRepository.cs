using TradeMind.ExecutionSessions.Application.DTOs;
using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.ExecutionSessions.Application.Abstractions;

public interface IExecutionSessionRepository
{
    Task<ExecutionSession?> GetAsync(ExecutionSessionId id, CancellationToken cancellationToken);
    Task AddAsync(ExecutionSession session, CancellationToken cancellationToken);
    Task UpdateAsync(ExecutionSession session, long expectedConcurrencyVersion, CancellationToken cancellationToken);
    Task<ExecutionSessionTimelineDto[]> GetTimelineAsync(ExecutionSessionId id, CancellationToken cancellationToken);
    Task<ExecutionSessionSearchPage> SearchAsync(ExecutionSessionSearchFilter filter, CancellationToken cancellationToken);
}
