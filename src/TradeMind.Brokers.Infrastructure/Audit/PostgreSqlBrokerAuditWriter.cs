using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Infrastructure.Persistence;
using TradeMind.Brokers.Infrastructure.Persistence.Entities;

namespace TradeMind.Brokers.Infrastructure.Audit;

public sealed class PostgreSqlBrokerAuditWriter(IDbContextFactory<BrokerDbContext> dbContextFactory) : IBrokerAuditWriter
{
    public async Task WriteAsync(BrokerAuditEntry entry, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Audit.Add(new BrokerAuditEntity
        {
            Id = Guid.NewGuid(),
            Operation = entry.Operation,
            Outcome = entry.Outcome,
            Mode = entry.Mode.ToString(),
            ActorId = entry.ActorId,
            TenantId = entry.TenantId,
            OrganizationId = entry.OrganizationId,
            ConnectorId = entry.ConnectorId?.Value,
            AccountId = entry.AccountId?.Value,
            ExecutionSessionId = entry.ExecutionSessionId,
            CorrelationId = entry.CorrelationId,
            TimestampUtc = entry.TimestampUtc,
            MetadataJson = JsonSerializer.Serialize(entry.Metadata)
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
