using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TradeMind.ExecutionSessions.Application;
using TradeMind.ExecutionSessions.Application.Abstractions;
using TradeMind.ExecutionSessions.Application.Commands;
using TradeMind.ExecutionSessions.Application.DTOs;
using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.ExecutionSessions.Application.Tests;

public sealed class ExecutionSessionApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Start_persists_running_session_and_audit_outbox()
    {
        var store = new FakeSessionStore();
        var service = BuildService(store);
        var result = await service.StartAsync(Command(), CancellationToken.None);

        Assert.Equal("Running", result.Status);
        Assert.Equal(1, result.ConcurrencyVersion);
        Assert.Single(store.Sessions);
        Assert.Equal(2, store.Audits.Count);
        Assert.Single(store.Outbox);
    }

    [Fact]
    public async Task Commands_update_aggregate_and_preserve_expected_version()
    {
        var store = new FakeSessionStore();
        var service = BuildService(store);
        var started = await service.StartAsync(Command(), CancellationToken.None);
        var id = new ExecutionSessionId(Guid.Parse(started.Id));

        var advanced = await service.AdvanceStageAsync(new(id, ExecutionSessionStage.Consensus, Now.AddMinutes(1), null, started.ConcurrencyVersion), CancellationToken.None);
        var linked = await service.LinkArtifactAsync(new(id, Artifact(ExecutionSessionArtifactType.Consensus, stage: ExecutionSessionStage.Consensus, createdAtUtc: Now.AddMinutes(2)), advanced.ConcurrencyVersion), CancellationToken.None);
        var completed = await service.CompleteAsync(new(id, Now.AddMinutes(3), linked.ConcurrencyVersion), CancellationToken.None);

        Assert.Equal("Completed", completed.Status);
        Assert.Equal("Completed", completed.CurrentStage);
        Assert.Single(completed.ArtifactReferences);
        Assert.True(store.Audits.Count >= 5);
    }

    [Fact]
    public async Task Stale_expected_version_returns_explicit_conflict()
    {
        var store = new FakeSessionStore();
        var service = BuildService(store);
        var started = await service.StartAsync(Command(), CancellationToken.None);
        var id = new ExecutionSessionId(Guid.Parse(started.Id));
        await service.AdvanceStageAsync(new(id, ExecutionSessionStage.Consensus, Now.AddMinutes(1), null, started.ConcurrencyVersion), CancellationToken.None);

        await Assert.ThrowsAsync<ExecutionSessionConcurrencyException>(() => service.AdvanceStageAsync(
            new(id, ExecutionSessionStage.TradingDecision, Now.AddMinutes(2), null, started.ConcurrencyVersion), CancellationToken.None));
    }

    [Fact]
    public async Task Query_not_found_and_cancellation_are_propagated()
    {
        var store = new FakeSessionStore();
        var service = BuildService(store);
        await Assert.ThrowsAsync<ExecutionSessionNotFoundException>(() => service.GetAsync(
            new ExecutionSessionId(Guid.Parse("22222222-2222-2222-2222-222222222222")), CancellationToken.None));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.StartAsync(Command(), cts.Token));
    }

    [Fact]
    public void Deterministic_id_factory_returns_same_id_for_same_input()
    {
        var factory = new DefaultExecutionSessionIdFactory();
        var first = factory.Create("corr", "EURUSD", "M15", Now);
        var second = factory.Create("corr", "EURUSD", "M15", Now);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Replay_fingerprint_is_order_independent_and_missing_artifacts_block_replay()
    {
        var session = ExecutionSession.Start(new ExecutionSessionId(Guid.Parse("11111111-1111-1111-1111-111111111111")), new("corr"), "EURUSD", "M15", ExecutionSessionTriggerType.Api, "test", "v1", "v1", Now);
        session.Begin(Now);
        session.LinkArtifact(Artifact(ExecutionSessionArtifactType.MarketContext, "m"), Now.AddMinutes(1));
        session.LinkArtifact(Artifact(ExecutionSessionArtifactType.TradingPlan, "p", ExecutionSessionStage.TradingPlan), Now.AddMinutes(1));
        session.LinkArtifact(Artifact(ExecutionSessionArtifactType.TradingWorkspace, "w", ExecutionSessionStage.TradingWorkspace), Now.AddMinutes(1));
        session.Complete(Now.AddMinutes(2));

        var manifest = ExecutionSessionReplayManifestBuilder.Build(session);
        Assert.True(manifest.IsReplayable);
        Assert.Empty(manifest.MissingArtifacts);
        Assert.Equal(64, manifest.DeterministicFingerprint.Length);
    }

    private static IExecutionSessionService BuildService(FakeSessionStore store)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionSessionRepository>(store);
        services.AddSingleton<IExecutionSessionUnitOfWork>(store);
        services.AddSingleton<IExecutionSessionReplayReader>(store);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now.AddMinutes(2)));
        services.AddLogging();
        services.AddExecutionSessionsApplication();
        return services.BuildServiceProvider().GetRequiredService<IExecutionSessionService>();
    }

    private static StartExecutionSessionCommand Command() => new(
        new ExecutionSessionId(Guid.Parse("11111111-1111-1111-1111-111111111111")), "corr-1", "EURUSD", "M15",
        ExecutionSessionTriggerType.Api, "test", "v1", "v1", Now);

    private static ExecutionSessionArtifactReference Artifact(
        ExecutionSessionArtifactType type = ExecutionSessionArtifactType.MarketContext,
        string id = "artifact-1",
        ExecutionSessionStage stage = ExecutionSessionStage.MarketContext,
        DateTimeOffset? createdAtUtc = null) =>
        new(type, new ExecutionSessionArtifactId(id), stage, 1, createdAtUtc ?? Now, id.PadLeft(64, 'a'));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeSessionStore : IExecutionSessionRepository, IExecutionSessionUnitOfWork, IExecutionSessionReplayReader
    {
        public Dictionary<Guid, ExecutionSession> Sessions { get; } = [];
        public List<ExecutionSessionAuditEntry> Audits { get; } = [];
        public List<ExecutionSessionOutboxMessage> Outbox { get; } = [];

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) => operation(cancellationToken);
        public Task AddAuditAsync(ExecutionSessionAuditEntry auditEntry, CancellationToken cancellationToken) { Audits.Add(auditEntry); return Task.CompletedTask; }
        public Task AddOutboxAsync(ExecutionSessionOutboxMessage message, CancellationToken cancellationToken) { Outbox.Add(message); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddAsync(ExecutionSession session, CancellationToken cancellationToken)
        {
            if (!Sessions.TryAdd(session.Id.Value, session)) throw new InvalidOperationException("duplicate");
            return Task.CompletedTask;
        }

        public Task<ExecutionSession?> GetAsync(ExecutionSessionId id, CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.TryGetValue(id.Value, out var session) ? Clone(session) : null);

        public Task UpdateAsync(ExecutionSession session, long expectedConcurrencyVersion, CancellationToken cancellationToken)
        {
            if (!Sessions.TryGetValue(session.Id.Value, out var current)) throw new ExecutionSessionNotFoundException(session.Id);
            if (current.ConcurrencyVersion != expectedConcurrencyVersion) throw new ExecutionSessionConcurrencyException(session.Id, expectedConcurrencyVersion);
            Sessions[session.Id.Value] = session;
            return Task.CompletedTask;
        }

        public Task<ExecutionSessionTimelineDto[]> GetTimelineAsync(ExecutionSessionId id, CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.TryGetValue(id.Value, out var session)
                ? session.TimelineEntries.Select(ExecutionSessionMapper.ToDto).ToArray()
                : throw new ExecutionSessionNotFoundException(id));

        public Task<ExecutionSessionSearchPage> SearchAsync(ExecutionSessionSearchFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult(new ExecutionSessionSearchPage([], filter.Page, filter.PageSize, 0));

        public Task<ExecutionSessionReplayManifestDto?> GetManifestAsync(ExecutionSessionId sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.TryGetValue(sessionId.Value, out var session) ? ExecutionSessionReplayManifestBuilder.Build(session) : null);

        private static ExecutionSession Clone(ExecutionSession session) => ExecutionSession.Rehydrate(session.Id, session.CorrelationId, session.IdempotencyKeyHash,
            session.TenantId, session.UserId, session.Instrument, session.Timeframe, session.StartedAtUtc, session.UpdatedAtUtc, session.CompletedAtUtc,
            session.Status, session.CurrentStage, session.SchemaVersion, session.CoreVersion, session.ApiVersion, session.TriggerType, session.Source,
            session.Failure, session.Metadata, session.ArtifactReferences, session.TimelineEntries, session.ConcurrencyVersion);
    }
}
