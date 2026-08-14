using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradeMind.Brokers.Application.LiveSafety;
using TradeMind.Brokers.Domain;
using TradeMind.Brokers.Infrastructure.Persistence;
using TradeMind.Brokers.Infrastructure.Persistence.Entities;

namespace TradeMind.Brokers.Infrastructure.LiveSafety;

public sealed class PostgreSqlCriticalAuditWriter(IDbContextFactory<BrokerDbContext> dbContextFactory) : ICriticalAuditWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(CriticalAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Audit.Add(new BrokerAuditEntity
        {
            Id = Guid.NewGuid(),
            Operation = entry.Operation,
            Outcome = entry.Outcome,
            Mode = "LiveSafety",
            ActorId = entry.ActorId,
            TenantId = entry.TenantId,
            ConnectorId = entry.BrokerId,
            AccountId = entry.AccountId,
            CorrelationId = entry.ExecutionId,
            TimestampUtc = entry.TimestampUtc,
            MetadataJson = JsonSerializer.Serialize(entry.Metadata, JsonOptions)
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class PostgreSqlKillSwitchStore(
    IDbContextFactory<BrokerDbContext> dbContextFactory,
    TimeProvider timeProvider,
    ICriticalAuditWriter auditWriter) : IKillSwitchStore
{
    public async Task<KillSwitchSnapshot> GetAsync(KillSwitchKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.KillSwitches.AsNoTracking().SingleOrDefaultAsync(item => item.Key == key.ToString(), cancellationToken).ConfigureAwait(false);
        return entity is null
            ? new KillSwitchSnapshot(key, KillSwitchState.Disabled, null, timeProvider.GetUtcNow(), "Default safe state.")
            : ToDomain(entity, key);
    }

    public Task<KillSwitchSnapshot> DisableAsync(KillSwitchKey key, string actorId, string reason, CancellationToken cancellationToken = default) => ChangeAsync(key, KillSwitchState.Disabled, actorId, reason, false, cancellationToken);
    public Task<KillSwitchSnapshot> EnableAsync(KillSwitchKey key, string actorId, string reason, CancellationToken cancellationToken = default) => ChangeAsync(key, KillSwitchState.Enabled, actorId, reason, false, cancellationToken);
    public Task<KillSwitchSnapshot> EmergencyStopAsync(KillSwitchKey key, string actorId, string reason, CancellationToken cancellationToken = default) => ChangeAsync(key, KillSwitchState.EmergencyStopped, actorId, reason, false, cancellationToken);

    public Task<KillSwitchSnapshot> ReactivateEmergencyStoppedAsync(KillSwitchKey key, string actorId, string reason, bool explicitlyAudited, CancellationToken cancellationToken = default)
    {
        if (!explicitlyAudited) throw new InvalidOperationException("Emergency stop reactivation requires an explicit audit acknowledgement.");
        return ChangeAsync(key, KillSwitchState.Disabled, actorId, reason, true, cancellationToken);
    }

    private async Task<KillSwitchSnapshot> ChangeAsync(KillSwitchKey key, KillSwitchState state, string actorId, string reason, bool allowEmergencyReset, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var current = await GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (current.State == KillSwitchState.EmergencyStopped && !allowEmergencyReset)
            throw new InvalidOperationException("An emergency-stopped kill switch cannot be reactivated automatically.");
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.KillSwitches.SingleOrDefaultAsync(item => item.Key == key.ToString(), cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        if (entity is null)
        {
            entity = new BrokerKillSwitchEntity { Key = key.ToString(), Scope = key.Scope.ToString(), Value = key.Value };
            context.KillSwitches.Add(entity);
        }
        entity.State = state.ToString(); entity.ChangedBy = actorId.Trim(); entity.ChangedAtUtc = now; entity.Reason = reason.Trim();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = new KillSwitchSnapshot(key, state, entity.ChangedBy, now, entity.Reason);
        await auditWriter.WriteAsync(new CriticalAuditEntry("KillSwitch", state.ToString(), key.Value, key.Scope == KillSwitchScope.Broker ? key.Value : null, key.Scope == KillSwitchScope.Account ? key.Value : null, null, actorId, now, new Dictionary<string, string> { ["scope"] = key.Scope.ToString(), ["reason"] = reason.Trim() }), cancellationToken).ConfigureAwait(false);
        return snapshot;
    }

    private static KillSwitchSnapshot ToDomain(BrokerKillSwitchEntity entity, KillSwitchKey key) => new(key, Enum.TryParse<KillSwitchState>(entity.State, true, out var state) ? state : KillSwitchState.EmergencyStopped, entity.ChangedBy, entity.ChangedAtUtc, entity.Reason);
}

public sealed class PostgreSqlExecutionQuarantineStore(
    IDbContextFactory<BrokerDbContext> dbContextFactory,
    TimeProvider timeProvider,
    ICriticalAuditWriter auditWriter) : IExecutionQuarantineStore
{
    public async Task<ExecutionQuarantineRecord?> GetAsync(string executionId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.ExecutionQuarantines.AsNoTracking().SingleOrDefaultAsync(item => item.ExecutionId == executionId, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<ExecutionQuarantineRecord> QuarantineAsync(ExecutionQuarantineRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.ExecutionQuarantines.SingleOrDefaultAsync(item => item.ExecutionId == record.ExecutionId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            entity = new BrokerExecutionQuarantineEntity { ExecutionId = record.ExecutionId };
            context.ExecutionQuarantines.Add(entity);
        }
        if (entity.State != ExecutionQuarantineState.Quarantined.ToString())
        {
            entity.TenantId = record.TenantId; entity.BrokerId = record.BrokerId; entity.AccountId = record.AccountId; entity.Instrument = record.Instrument; entity.Reason = record.Reason.ToString(); entity.State = record.State.ToString(); entity.ChangedAtUtc = record.ChangedAtUtc; entity.ChangedBy = record.ChangedBy; entity.Explanation = record.Explanation;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        var stored = ToDomain(entity);
        await auditWriter.WriteAsync(new CriticalAuditEntry("ExecutionQuarantine", stored.State.ToString(), stored.TenantId, stored.BrokerId, stored.AccountId, stored.ExecutionId, stored.ChangedBy, stored.ChangedAtUtc, new Dictionary<string, string> { ["reason"] = stored.Reason.ToString() }), cancellationToken).ConfigureAwait(false);
        return stored;
    }

    public async Task<ExecutionQuarantineRecord> ReleaseAsync(string executionId, string actorId, string reason, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.ExecutionQuarantines.SingleOrDefaultAsync(item => item.ExecutionId == executionId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException($"Execution '{executionId}' is not quarantined.");
        entity.State = ExecutionQuarantineState.ExplicitlyReleased.ToString(); entity.ChangedAtUtc = timeProvider.GetUtcNow(); entity.ChangedBy = actorId.Trim(); entity.Explanation = reason.Trim();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var released = ToDomain(entity);
        await auditWriter.WriteAsync(new CriticalAuditEntry("ExecutionQuarantineRelease", released.State.ToString(), released.TenantId, released.BrokerId, released.AccountId, released.ExecutionId, released.ChangedBy, released.ChangedAtUtc, new Dictionary<string, string> { ["reason"] = released.Explanation }), cancellationToken).ConfigureAwait(false);
        return released;
    }

    private static ExecutionQuarantineRecord ToDomain(BrokerExecutionQuarantineEntity entity) => new(entity.ExecutionId, entity.TenantId, entity.BrokerId, entity.AccountId, entity.Instrument, Enum.Parse<ExecutionQuarantineReason>(entity.Reason), Enum.Parse<ExecutionQuarantineState>(entity.State), entity.ChangedAtUtc, entity.ChangedBy, entity.Explanation);
}

public sealed class PostgreSqlBrokerPositionOwnershipStore(IDbContextFactory<BrokerDbContext> dbContextFactory) : IBrokerPositionOwnershipStore
{
    public async Task<BrokerPositionOwnership?> GetAsync(string positionId, string tenantId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.PositionOwnership.AsNoTracking().SingleOrDefaultAsync(item => item.PositionId == positionId && item.TenantId == tenantId, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task SaveAsync(BrokerPositionOwnership ownership, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.PositionOwnership.SingleOrDefaultAsync(item => item.PositionId == ownership.PositionId && item.TenantId == ownership.TenantId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            entity = new BrokerPositionOwnershipEntity { PositionId = ownership.PositionId, TenantId = ownership.TenantId };
            context.PositionOwnership.Add(entity);
        }
        entity.ExecutionSessionId = ownership.ExecutionSessionId; entity.BrokerExecutionId = ownership.BrokerExecutionId; entity.TradingPlanId = ownership.TradingPlanId; entity.RiskAssessmentId = ownership.RiskAssessmentId; entity.BrokerAccountId = ownership.BrokerAccountId; entity.Symbol = ownership.Symbol; entity.Direction = ownership.Direction.ToString(); entity.OpenedAtUtc = ownership.OpenedAtUtc; entity.Source = ownership.Source; entity.ReconciliationState = ownership.ReconciliationState; entity.ConcurrencyVersion++;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static BrokerPositionOwnership ToDomain(BrokerPositionOwnershipEntity entity) => new(entity.PositionId, entity.ExecutionSessionId, entity.BrokerExecutionId, entity.TradingPlanId, entity.RiskAssessmentId, entity.TenantId, entity.BrokerAccountId, entity.Symbol, Enum.Parse<BrokerOrderSide>(entity.Direction), entity.OpenedAtUtc, entity.Source, entity.ReconciliationState);
}
