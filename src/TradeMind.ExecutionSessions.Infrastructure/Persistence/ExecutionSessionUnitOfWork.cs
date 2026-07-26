using Microsoft.EntityFrameworkCore;
using TradeMind.ExecutionSessions.Application.Abstractions;
using TradeMind.ExecutionSessions.Application.DTOs;
using TradeMind.ExecutionSessions.Domain;
using TradeMind.ExecutionSessions.Infrastructure.Persistence.Entities;

namespace TradeMind.ExecutionSessions.Infrastructure.Persistence;

public sealed class ExecutionSessionUnitOfWork(ExecutionSessionsDbContext dbContext) : IExecutionSessionUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await operation(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public Task AddAuditAsync(ExecutionSessionAuditEntry auditEntry, CancellationToken cancellationToken)
    {
        var entity = new ExecutionSessionAuditEntity
        {
            AuditId = auditEntry.AuditId,
            SessionId = auditEntry.SessionId.Value,
            EventType = auditEntry.EventType.ToString(),
            OccurredAtUtc = auditEntry.OccurredAtUtc,
            CorrelationId = auditEntry.CorrelationId.Value,
            ActorType = auditEntry.ActorType.ToString(),
            ActorId = auditEntry.ActorId,
            PreviousStatus = auditEntry.PreviousStatus?.ToString(),
            NewStatus = auditEntry.NewStatus?.ToString(),
            PreviousStage = auditEntry.PreviousStage?.ToString(),
            NewStage = auditEntry.NewStage?.ToString(),
            ArtifactId = auditEntry.ArtifactId,
            MetadataJson = ExecutionSessionSerialization.Serialize(auditEntry.Metadata),
            SchemaVersion = auditEntry.SchemaVersion
        };
        return dbContext.Audit.AddAsync(entity, cancellationToken).AsTask();
    }

    public Task AddOutboxAsync(ExecutionSessionOutboxMessage message, CancellationToken cancellationToken)
    {
        var entity = new ExecutionSessionOutboxEntity
        {
            OutboxMessageId = message.OutboxMessageId,
            SessionId = message.SessionId.Value,
            EventType = message.EventType,
            Payload = message.Payload,
            OccurredAtUtc = message.OccurredAtUtc,
            ProcessedAtUtc = message.ProcessedAtUtc,
            AttemptCount = message.AttemptCount,
            LastError = message.LastError,
            ConcurrencyVersion = message.ConcurrencyVersion
        };
        return dbContext.Outbox.AddAsync(entity, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
