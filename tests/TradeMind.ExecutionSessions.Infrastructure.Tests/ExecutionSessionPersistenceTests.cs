using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeMind.ExecutionSessions.Application;
using TradeMind.ExecutionSessions.Application.Abstractions;
using TradeMind.ExecutionSessions.Application.DTOs;
using TradeMind.ExecutionSessions.Domain;
using TradeMind.ExecutionSessions.Infrastructure;
using TradeMind.ExecutionSessions.Infrastructure.Idempotency;
using TradeMind.ExecutionSessions.Infrastructure.Persistence;
using TradeMind.ExecutionSessions.Infrastructure.Persistence.Repositories;

namespace TradeMind.ExecutionSessions.Infrastructure.Tests;

public sealed class ExecutionSessionPersistenceTests(PostgreSqlExecutionSessionsFixture fixture)
    : IClassFixture<PostgreSqlExecutionSessionsFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Migration_creates_all_required_tables_and_indexes()
    {
        await using var context = fixture.CreateDbContext();
        var tables = await context.Database.SqlQueryRaw<string>("""
            SELECT table_name FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name LIKE 'execution_%'
            ORDER BY table_name;
            """).ToListAsync();

        Assert.Contains("execution_sessions", tables);
        Assert.Contains("execution_session_artifacts", tables);
        Assert.Contains("execution_session_timeline", tables);
        Assert.Contains("execution_session_audit", tables);
        Assert.Contains("execution_session_outbox", tables);
        Assert.Contains("execution_idempotency_records", tables);

        var indexes = await context.Database.SqlQueryRaw<string>("""
            SELECT indexname AS "Value" FROM pg_indexes
            WHERE schemaname = 'public' AND indexname LIKE 'ix_execution_%'
            ORDER BY indexname;
            """).ToListAsync();
        Assert.Contains("ix_execution_sessions_correlation_id", indexes);
        Assert.Contains("ix_execution_sessions_status_started", indexes);
        Assert.Contains("ix_execution_session_timeline_session_occurred", indexes);
        Assert.Contains("ix_execution_session_outbox_pending", indexes);
        Assert.Contains("ix_execution_idempotency_expires", indexes);
    }

    [Fact]
    public async Task Session_artifacts_timeline_audit_and_outbox_persist_atomically()
    {
        var session = CreateSession("persist-1");
        session.Begin(Now);
        session.AdvanceStage(ExecutionSessionStage.Consensus, Now.AddMinutes(1));
        session.LinkArtifact(Artifact("consensus-1", ExecutionSessionArtifactType.Consensus, ExecutionSessionStage.Consensus), Now.AddMinutes(1));

        await using var context = fixture.CreateDbContext();
        var repository = new ExecutionSessionRepository(context);
        var unitOfWork = new ExecutionSessionUnitOfWork(context);
        await unitOfWork.ExecuteAsync(async token =>
        {
            await repository.AddAsync(session, token);
            await unitOfWork.AddAuditAsync(Audit(session, ExecutionSessionAuditEventType.SessionCreated), token);
            await unitOfWork.AddOutboxAsync(new ExecutionSessionOutboxMessage(Guid.NewGuid(), session.Id, "SessionCreated", "{}", Now), token);
            await unitOfWork.SaveChangesAsync(token);
            return true;
        }, CancellationToken.None);

        await using var readContext = fixture.CreateDbContext();
        var persisted = await new ExecutionSessionRepository(readContext).GetAsync(session.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(3, persisted!.TimelineEntries.Count);
        Assert.Single(persisted.ArtifactReferences);
        Assert.Equal(1, await readContext.Audit.CountAsync(item => item.SessionId == session.Id.Value));
        Assert.Equal(1, await readContext.Outbox.CountAsync(item => item.SessionId == session.Id.Value));
    }

    [Fact]
    public async Task Repository_searches_with_deterministic_order_and_filters()
    {
        await PersistAsync(CreateSession("search-a", Now.AddMinutes(-2), "SEARCH-EURUSD"));
        await PersistAsync(CreateSession("search-b", Now.AddMinutes(-1), "SEARCH-GBPUSD"));
        await using var context = fixture.CreateDbContext();
        var page = await new ExecutionSessionRepository(context).SearchAsync(new ExecutionSessionSearchFilter(instrument: "SEARCH-EURUSD", pageSize: 1), CancellationToken.None);

        Assert.Equal(1, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal("search-a", page.Items[0].CorrelationId);
    }

    [Fact]
    public async Task Repository_scope_hides_other_tenants_and_missing_scope()
    {
        var owned = ExecutionSession.Start(new ExecutionSessionId(Guid.NewGuid()), new ExecutionCorrelationId("scope-owned"), "EURUSD", "M15",
            ExecutionSessionTriggerType.Api, "integration", "v1", "v1", Now, organizationId: "org-1", tenantId: "tenant-1");
        var other = ExecutionSession.Start(new ExecutionSessionId(Guid.NewGuid()), new ExecutionCorrelationId("scope-other"), "EURUSD", "M15",
            ExecutionSessionTriggerType.Api, "integration", "v1", "v1", Now, organizationId: "org-1", tenantId: "tenant-2");
        await PersistAsync(owned);
        await PersistAsync(other);

        await using var context = fixture.CreateDbContext();
        var scoped = new ExecutionSessionRepository(context, new FixedScope("org-1", "tenant-1"));
        Assert.NotNull(await scoped.GetAsync(owned.Id, CancellationToken.None));
        Assert.Null(await scoped.GetAsync(other.Id, CancellationToken.None));

        var missingScope = new ExecutionSessionRepository(context, new FixedScope(null, null));
        Assert.Null(await missingScope.GetAsync(owned.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_updates_use_postgresql_optimistic_concurrency()
    {
        var session = CreateSession("concurrency");
        session.Begin(Now);
        await PersistAsync(session);
        await using var context1 = fixture.CreateDbContext();
        await using var context2 = fixture.CreateDbContext();
        var first = await new ExecutionSessionRepository(context1).GetAsync(session.Id, CancellationToken.None);
        var second = await new ExecutionSessionRepository(context2).GetAsync(session.Id, CancellationToken.None);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, first!.ConcurrencyVersion);
        Assert.Equal(1, second!.ConcurrencyVersion);
        first.AdvanceStage(ExecutionSessionStage.Consensus, Now.AddMinutes(1));
        second!.AdvanceStage(ExecutionSessionStage.Consensus, Now.AddMinutes(1));
        var uow1 = new ExecutionSessionUnitOfWork(context1);
        var uow2 = new ExecutionSessionUnitOfWork(context2);
        await uow1.ExecuteAsync(async token =>
        {
            await new ExecutionSessionRepository(context1).UpdateAsync(first, 1, token);
            await uow1.SaveChangesAsync(token);
            return true;
        }, CancellationToken.None);

        await Assert.ThrowsAsync<ExecutionSessionConcurrencyException>(() => uow2.ExecuteAsync(async token =>
        {
            await new ExecutionSessionRepository(context2).UpdateAsync(second, 1, token);
            await uow2.SaveChangesAsync(token);
            return true;
        }, CancellationToken.None));
    }

    [Fact]
    public async Task Durable_idempotency_replays_equal_hash_and_rejects_conflict()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:TradeMind"] = fixture.ConnectionString,
            ["TradeMind:Persistence:Provider"] = "PostgreSql",
            ["TradeMind:Persistence:ConnectionStringName"] = "TradeMind"
        }).Build();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddExecutionSessions(configuration);
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IPersistentIdempotencyStore>();
        var calls = 0;
        var first = await store.ExecuteAsync("idempotency-1", "hash-a", _ =>
        {
            calls++;
            return Task.FromResult(new PersistentIdempotencyResponse(201, "application/json", [1, 2, 3]));
        }, CancellationToken.None);
        var replay = await store.ExecuteAsync("idempotency-1", "hash-a", _ =>
            Task.FromResult(new PersistentIdempotencyResponse(500, null, [])), CancellationToken.None);
        var conflict = await store.ExecuteAsync("idempotency-1", "hash-b", _ =>
            Task.FromResult(new PersistentIdempotencyResponse(500, null, [])), CancellationToken.None);

        Assert.False(first.IsReplay);
        Assert.True(replay.IsReplay);
        Assert.True(conflict.IsConflict);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Failed_transaction_does_not_leave_session_or_outbox_rows()
    {
        var session = CreateSession("rollback");
        await using var context = fixture.CreateDbContext();
        var repository = new ExecutionSessionRepository(context);
        var unitOfWork = new ExecutionSessionUnitOfWork(context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync<object?>(async token =>
        {
            await repository.AddAsync(session, token);
            await unitOfWork.SaveChangesAsync(token);
            throw new InvalidOperationException("rollback");
        }, CancellationToken.None));

        await using var readContext = fixture.CreateDbContext();
        Assert.False(await readContext.ExecutionSessions.AnyAsync(item => item.Id == session.Id.Value));
        Assert.False(await readContext.Outbox.AnyAsync(item => item.SessionId == session.Id.Value));
    }

    private async Task PersistAsync(ExecutionSession session)
    {
        await using var context = fixture.CreateDbContext();
        var repository = new ExecutionSessionRepository(context);
        var unitOfWork = new ExecutionSessionUnitOfWork(context);
        await unitOfWork.ExecuteAsync(async token =>
        {
            await repository.AddAsync(session, token);
            await unitOfWork.SaveChangesAsync(token);
            return true;
        }, CancellationToken.None);
    }

    private static ExecutionSession CreateSession(string correlation, DateTimeOffset? startedAt = null, string instrument = "EURUSD") =>
        ExecutionSession.Start(new ExecutionSessionId(Guid.NewGuid()), new ExecutionCorrelationId(correlation), instrument, "M15",
            ExecutionSessionTriggerType.Api, "integration", "v1", "v1", startedAt ?? Now);

    private static ExecutionSessionArtifactReference Artifact(string id, ExecutionSessionArtifactType type, ExecutionSessionStage stage) =>
        new(type, new ExecutionSessionArtifactId(id), stage, 1, Now, id.PadLeft(64, 'b'));

    private static ExecutionSessionAuditEntry Audit(ExecutionSession session, ExecutionSessionAuditEventType eventType) =>
        new(Guid.NewGuid(), session.Id, eventType, Now, session.CorrelationId, ExecutionSessionActorType.System, null,
            null, session.Status, null, session.CurrentStage, null, new Dictionary<string, string>());

    private sealed class FixedScope(string? organizationId, string? tenantId) : IExecutionSessionAccessScope
    {
        public string? OrganizationId { get; } = organizationId;
        public string? TenantId { get; } = tenantId;
        public bool IsRestricted => true;
        public bool IsAdministrativeOverride => false;
    }
}
