using TradeMind.ExecutionSessions.Domain;
using TradeMind.ExecutionSessions.Application.DTOs;

namespace TradeMind.ExecutionSessions.Application.Abstractions;

public interface IExecutionSessionUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
    Task AddAuditAsync(ExecutionSessionAuditEntry auditEntry, CancellationToken cancellationToken);
    Task AddOutboxAsync(ExecutionSessionOutboxMessage message, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
