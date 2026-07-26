using Microsoft.EntityFrameworkCore;
using TradeMind.ExecutionSessions.Application;
using TradeMind.ExecutionSessions.Domain;
using TradeMind.ExecutionSessions.Infrastructure.Persistence.Entities;

namespace TradeMind.ExecutionSessions.Infrastructure.Persistence;

public sealed class ExecutionSessionsDbContext(DbContextOptions<ExecutionSessionsDbContext> options) : DbContext(options)
{
    public DbSet<ExecutionSessionEntity> ExecutionSessions => Set<ExecutionSessionEntity>();
    public DbSet<ExecutionSessionArtifactEntity> Artifacts => Set<ExecutionSessionArtifactEntity>();
    public DbSet<ExecutionSessionTimelineEntity> Timeline => Set<ExecutionSessionTimelineEntity>();
    public DbSet<ExecutionSessionAuditEntity> Audit => Set<ExecutionSessionAuditEntity>();
    public DbSet<ExecutionSessionOutboxEntity> Outbox => Set<ExecutionSessionOutboxEntity>();
    public DbSet<DurableIdempotencyRecordEntity> IdempotencyRecords => Set<DurableIdempotencyRecordEntity>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var entity = exception.Entries.FirstOrDefault()?.Entity as ExecutionSessionEntity;
            if (entity is not null)
            {
                throw new ExecutionSessionConcurrencyException(new ExecutionSessionId(entity.Id), entity.ConcurrencyVersion - 1);
            }

            throw;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExecutionSessionsDbContext).Assembly);
    }
}
