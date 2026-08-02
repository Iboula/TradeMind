using Microsoft.EntityFrameworkCore;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Infrastructure.Persistence.Entities;

namespace TradeMind.Brokers.Infrastructure.Persistence;

public sealed class BrokerReconciliationRecordWriter(IDbContextFactory<BrokerDbContext> dbContextFactory) : IBrokerReconciliationRecordWriter
{
    public async Task WriteAsync(TradeMind.Brokers.Domain.BrokerExecutionContext context, BrokerReconciliationReport report, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var existing = await db.Reconciliations.FindAsync([report.ReconciliationId.Value], cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            db.Reconciliations.Add(new BrokerReconciliationEntity
            {
                Id = report.ReconciliationId.Value,
                ConnectorId = report.ConnectorId.Value,
                AccountId = report.AccountId.Value,
                TenantId = context.TenantId,
                IsConsistent = report.IsConsistent,
                ErrorCode = report.Error?.Code,
                StartedAtUtc = report.StartedAtUtc,
                CompletedAtUtc = report.CompletedAtUtc
            });
        }

        foreach (var mismatch in report.Mismatches)
        {
            if (!await db.ReconciliationMismatches.AnyAsync(item => item.ReconciliationId == report.ReconciliationId.Value && item.Reference == mismatch.Reference && item.Type == mismatch.Type.ToString(), cancellationToken).ConfigureAwait(false))
            {
                db.ReconciliationMismatches.Add(new BrokerReconciliationMismatchEntity
                {
                    ReconciliationId = report.ReconciliationId.Value,
                    Type = mismatch.Type.ToString(),
                    Reference = mismatch.Reference,
                    Description = mismatch.Description
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
