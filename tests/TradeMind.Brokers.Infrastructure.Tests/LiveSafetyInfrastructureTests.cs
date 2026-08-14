using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Application.LiveSafety;
using TradeMind.Brokers.Domain;
using TradeMind.Brokers.Infrastructure.Idempotency;
using TradeMind.Brokers.Infrastructure.LiveSafety;
using TradeMind.Brokers.Infrastructure.Locking;
using TradeMind.Brokers.Infrastructure.Persistence;
using TradeMind.Brokers.Infrastructure.Persistence.Entities;

namespace TradeMind.Brokers.Infrastructure.Tests;

public sealed class LiveSafetyInfrastructureTests(PostgreSqlBrokerFixture fixture) : IClassFixture<PostgreSqlBrokerFixture>
{
    [Fact]
    public async Task Migration_creates_live_safety_tables()
    {
        await using var context = fixture.CreateContext();
        var tables = await context.Database.SqlQuery<string>($"select table_name from information_schema.tables where table_schema = 'public' and table_name like 'broker_%' order by table_name").ToListAsync();
        Assert.Contains("broker_kill_switches", tables);
        Assert.Contains("broker_execution_quarantine", tables);
        Assert.Contains("broker_position_ownership", tables);
    }

    [Fact]
    public async Task PostgreSql_kill_switch_persists_emergency_stop_and_requires_explicit_reactivation()
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("delete from broker_audit; delete from broker_kill_switches;");
        var audit = new PostgreSqlCriticalAuditWriter(new TestFactory(fixture.ConnectionString));
        var store = new PostgreSqlKillSwitchStore(new TestFactory(fixture.ConnectionString), TimeProvider.System, audit);
        var key = new KillSwitchKey(KillSwitchScope.Tenant, "tenant-live");
        Assert.Equal(KillSwitchState.Disabled, (await store.GetAsync(key)).State);
        await store.EmergencyStopAsync(key, "operator", "incident");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.DisableAsync(key, "system", "automatic"));
        var reset = await store.ReactivateEmergencyStoppedAsync(key, "operator", "reviewed", true);
        Assert.Equal(KillSwitchState.Disabled, reset.State);
        Assert.Equal(2, await context.Audit.CountAsync(item => item.Operation == "KillSwitch"));
    }

    [Fact]
    public async Task PostgreSql_quarantine_is_tenant_scoped_and_release_is_explicit()
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("delete from broker_execution_quarantine; delete from broker_audit;");
        var audit = new PostgreSqlCriticalAuditWriter(new TestFactory(fixture.ConnectionString));
        var store = new PostgreSqlExecutionQuarantineStore(new TestFactory(fixture.ConnectionString), TimeProvider.System, audit);
        var record = new ExecutionQuarantineRecord("exec-live", "tenant-a", "broker", "account", "EURUSD", ExecutionQuarantineReason.UnknownBrokerResponse, ExecutionQuarantineState.Quarantined, DateTimeOffset.UtcNow, "system", "unknown");
        await store.QuarantineAsync(record);
        Assert.NotNull(await store.GetAsync(record.ExecutionId));
        var released = await store.ReleaseAsync(record.ExecutionId, "operator", "reconciled");
        Assert.Equal(ExecutionQuarantineState.ExplicitlyReleased, released.State);
        Assert.Null(await store.GetAsync("not-found", CancellationToken.None));
    }

    [Fact]
    public async Task PostgreSql_position_ownership_persists_all_required_fields()
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("delete from broker_position_ownership;");
        var store = new PostgreSqlBrokerPositionOwnershipStore(new TestFactory(fixture.ConnectionString));
        var ownership = new BrokerPositionOwnership("position-live", "session", "execution", "plan", "risk", "tenant-a", "account", "EURUSD", BrokerOrderSide.Buy, DateTimeOffset.UtcNow, "TradeMind", "Clean");
        await store.SaveAsync(ownership);
        var loaded = await store.GetAsync(ownership.PositionId, ownership.TenantId);
        Assert.NotNull(loaded);
        Assert.Equal(ownership.PositionId, loaded!.PositionId);
        Assert.Equal(ownership.ExecutionSessionId, loaded.ExecutionSessionId);
        Assert.Equal(ownership.BrokerExecutionId, loaded.BrokerExecutionId);
        Assert.Equal(ownership.TradingPlanId, loaded.TradingPlanId);
        Assert.Equal(ownership.RiskAssessmentId, loaded.RiskAssessmentId);
        Assert.Equal(ownership.Direction, loaded.Direction);
        Assert.Equal(ownership.ReconciliationState, loaded.ReconciliationState);
        Assert.Null(await store.GetAsync(ownership.PositionId, "tenant-b"));
    }

    [Fact]
    public async Task PostgreSql_advisory_lock_allows_one_instance_per_execution_scope()
    {
        var first = new PostgreSqlBrokerExecutionLock(fixture.ConnectionString, TimeProvider.System);
        var second = new PostgreSqlBrokerExecutionLock(fixture.ConnectionString, TimeProvider.System);
        var scope = new BrokerExecutionLockScope("tenant-a", "broker", "account", "EURUSD");
        var options = new BrokerExecutionLockOptions(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(20));
        var firstResult = await first.AcquireAsync(scope, "pod-a", options);
        Assert.True(firstResult.Acquired);
        await using (firstResult.Lease!)
        {
            var secondResult = await second.AcquireAsync(scope, "pod-b", options);
            Assert.False(secondResult.Acquired);
            Assert.Null(secondResult.Lease);
        }
        var recovered = await second.AcquireAsync(scope, "pod-b", options);
        Assert.True(recovered.Acquired);
        await recovered.Lease!.DisposeAsync();
    }

    [Fact]
    public async Task Durable_unknown_execution_is_replayed_without_retrying_the_operation()
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("delete from broker_idempotency;");
        var store = new PostgreSqlBrokerIdempotencyStore(new TestFactory(fixture.ConnectionString), TimeProvider.System, Options.Create(new TradeMind.Brokers.Application.Options.BrokerOptions { IdempotencyTtlMinutes = 60 }));
        var result = new BrokerExecutionResult(new BrokerExecutionId("execution-unknown"), BrokerExecutionResultStatus.ExecutionUnknown, null, null, null, "SubmitOrder", DateTimeOffset.UtcNow);
        await store.MarkExecutionUnknownAsync("unknown-key", "request-hash", result, CancellationToken.None);
        var calls = 0;
        var replay = await store.ExecuteAsync("unknown-key", "request-hash", _ => { calls++; return Task.FromResult(result); }, CancellationToken.None);
        Assert.True(replay.IsReplay);
        Assert.Equal(BrokerExecutionResultStatus.ExecutionUnknown, replay.Result.Status);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task PostgreSql_recovery_reader_detects_unknown_external_position_without_writing()
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("delete from broker_position_ownership; delete from broker_positions; delete from broker_reconciliation_runs;");
        context.Positions.Add(new BrokerPositionEntity
        {
            Id = "external-position",
            ConnectorId = "broker",
            AccountId = "account",
            TenantId = "tenant-a",
            Instrument = "EURUSD",
            Quantity = 1,
            AveragePrice = 1,
            OpenedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            ConcurrencyVersion = 1
        });
        await context.SaveChangesAsync();
        var reader = new PostgreSqlBrokerExecutionRecoveryReader(new TestFactory(fixture.ConnectionString));
        var snapshot = await reader.ReadAsync("tenant-a", "broker", "account");
        Assert.Equal("UnknownExternal", Assert.Single(snapshot.BrokerPositions).ReconciliationState);
        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.Positions.CountAsync(item => item.Id == "external-position"));
    }

    private sealed class TestFactory(string connectionString) : IDbContextFactory<BrokerDbContext>
    {
        public BrokerDbContext CreateDbContext() => new(new DbContextOptionsBuilder<BrokerDbContext>().UseNpgsql(connectionString, options => options.MigrationsAssembly(typeof(BrokerDbContext).Assembly.FullName)).Options);
        public Task<BrokerDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
