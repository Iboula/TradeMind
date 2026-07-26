using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.ExecutionSessions.Domain.Tests;

public sealed class ExecutionSessionDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_creates_a_created_session_with_immutable_metadata()
    {
        var metadata = new Dictionary<string, string> { ["environment"] = "test" };
        var session = Create(metadata);
        metadata["environment"] = "changed";

        Assert.Equal(ExecutionSessionStatus.Created, session.Status);
        Assert.Equal(ExecutionSessionStage.MarketContext, session.CurrentStage);
        Assert.Equal("test", session.Metadata["environment"]);
        Assert.Equal(0, session.ConcurrencyVersion);
    }

    [Fact]
    public void Start_rejects_empty_ids_and_invalid_values()
    {
        Assert.Throws<ArgumentException>(() => new ExecutionSessionId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ExecutionCorrelationId(" "));
        Assert.Throws<ArgumentException>(() => Create(instrument: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(schemaVersion: 0));
        Assert.Throws<ArgumentException>(() => Create(startedAtUtc: Now.AddHours(1).ToOffset(TimeSpan.FromHours(1))));
    }

    [Fact]
    public void Begin_and_progression_are_monotonic()
    {
        var session = Running();
        session.AdvanceStage(ExecutionSessionStage.Consensus, Now.AddMinutes(1));

        Assert.Equal(ExecutionSessionStage.Consensus, session.CurrentStage);
        Assert.Equal(2, session.ConcurrencyVersion);
        Assert.Throws<ExecutionSessionDomainException>(() => session.AdvanceStage(ExecutionSessionStage.ExpertAnalysis, Now.AddMinutes(2)));
    }

    [Fact]
    public void Terminal_states_cannot_be_changed()
    {
        var session = Running();
        session.Complete(Now.AddMinutes(1));

        Assert.Equal(ExecutionSessionStatus.Completed, session.Status);
        Assert.Throws<ExecutionSessionDomainException>(() => session.Cancel(Now.AddMinutes(2)));
        Assert.Throws<ExecutionSessionDomainException>(() => session.LinkArtifact(Artifact(), Now.AddMinutes(2)));
    }

    [Fact]
    public void Failure_and_cancellation_are_strict_transitions()
    {
        var failure = Running();
        failure.Fail(new ExecutionSessionFailure("PROVIDER_FAILED", "provider failed"), Now.AddMinutes(1));
        Assert.Equal(ExecutionSessionStatus.Failed, failure.Status);
        Assert.NotNull(failure.Failure);

        var cancelled = Running();
        cancelled.Cancel(Now.AddMinutes(1));
        Assert.Equal(ExecutionSessionStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public void Artifact_ids_are_unique_and_artifacts_are_defensive()
    {
        var session = Running();
        session.LinkArtifact(Artifact(), Now.AddMinutes(1));
        Assert.Throws<ExecutionSessionDomainException>(() => session.LinkArtifact(Artifact(), Now.AddMinutes(2)));
        Assert.Throws<ExecutionSessionDomainException>(() => session.LinkArtifact(Artifact(ExecutionSessionArtifactType.Consensus), Now.AddMinutes(2)));

        var artifacts = session.ArtifactReferences;
        Assert.Single(artifacts);
        Assert.Throws<NotSupportedException>(() => ((IList<ExecutionSessionArtifactReference>)artifacts).Add(Artifact()));
    }

    [Fact]
    public void Timestamps_must_be_utc_and_monotonic()
    {
        var session = Running();
        Assert.Throws<ExecutionSessionDomainException>(() => session.AdvanceStage(ExecutionSessionStage.Consensus, Now.AddMinutes(-1)));
        Assert.Throws<ArgumentException>(() => session.AdvanceStage(ExecutionSessionStage.Consensus, Now.AddMinutes(1).ToOffset(TimeSpan.FromHours(1))));
    }

    [Fact]
    public void Rehydrate_preserves_state_and_collections_are_snapshots()
    {
        var original = Running();
        original.LinkArtifact(Artifact(), Now.AddMinutes(1));
        var rehydrated = ExecutionSession.Rehydrate(original.Id, original.CorrelationId, original.IdempotencyKeyHash, original.TenantId,
            original.UserId, original.Instrument, original.Timeframe, original.StartedAtUtc, original.UpdatedAtUtc, original.CompletedAtUtc,
            original.Status, original.CurrentStage, original.SchemaVersion, original.CoreVersion, original.ApiVersion, original.TriggerType,
            original.Source, original.Failure, original.Metadata, original.ArtifactReferences, original.TimelineEntries, original.ConcurrencyVersion);

        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(original.ArtifactReferences.Select(item => item.ArtifactId), rehydrated.ArtifactReferences.Select(item => item.ArtifactId));
        Assert.Equal(original.TimelineEntries.Count, rehydrated.TimelineEntries.Count);
    }

    private static ExecutionSession Running()
    {
        var session = Create();
        session.Begin(Now);
        return session;
    }

    private static ExecutionSession Create(
        IReadOnlyDictionary<string, string>? metadata = null,
        string instrument = "EURUSD",
        int schemaVersion = 1,
        DateTimeOffset? startedAtUtc = null) => ExecutionSession.Start(
        new ExecutionSessionId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        new ExecutionCorrelationId("corr-1"), instrument, "M15", ExecutionSessionTriggerType.Api, "test", "v1", "v1",
        startedAtUtc ?? Now, metadata, schemaVersion: schemaVersion);

    private static ExecutionSessionArtifactReference Artifact(ExecutionSessionArtifactType type = ExecutionSessionArtifactType.MarketContext) =>
        new(type, new ExecutionSessionArtifactId("artifact-1"), ExecutionSessionStage.MarketContext, 1, Now, "a".PadLeft(64, 'a'));
}
