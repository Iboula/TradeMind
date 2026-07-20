using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.ExpertAgents.Consensus.Tests;

internal static class ConsensusTestData
{
    public static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    public static MarketContext Context(string userId = "user-1", double quality = 90)
    {
        var snapshotId = SnapshotId.New();
        var snapshot = new MarketSnapshot(
            snapshotId,
            new ConnectorId("test-connector"),
            new ExternalAccountReference("account-1"),
            new Instrument("EURUSD"),
            Timeframe.H1,
            Now,
            Now,
            [],
            null,
            [],
            [],
            [],
            [],
            new SnapshotQuality(SnapshotFreshness.Live, ConnectorCapabilities.Candles, ConnectorCapabilities.None));
        var reference = new ContextSourceReference("snapshot", snapshotId.ToString());
        var trace = new ContextSourceTrace(
            new ContextProviderId("market-snapshot"),
            ContextProviderCategory.MarketSnapshot,
            ContextRequirement.Required,
            ContextProviderExecutionStatus.Succeeded,
            Now,
            Now,
            TimeSpan.Zero,
            Now,
            ContextFreshness.Fresh,
            1,
            "1.0",
            [reference]);
        return new MarketContext(
            MarketContextId.New(),
            MarketContext.CurrentVersion,
            userId,
            "session-1",
            new Instrument("EURUSD"),
            Timeframe.H1,
            Now,
            MarketContextBuildStatus.Succeeded,
            snapshot,
            null,
            null,
            null,
            null,
            null,
            null,
            [trace],
            [],
            new ContextQuality(quality, quality, quality, quality, quality >= 80 ? ContextQualityBand.Excellent : ContextQualityBand.Limited));
    }

    public static AgentAnalysisResult Result(
        MarketContext context,
        string run,
        string agent,
        AgentDirectionalBias bias,
        AgentAnalysisStatus status = AgentAnalysisStatus.Succeeded,
        double confidence = 90,
        double freshness = 90,
        double coverage = 90,
        IReadOnlyCollection<AgentMarketLevel>? levels = null,
        IReadOnlyCollection<AgentScenario>? scenarios = null,
        IReadOnlyCollection<string>? risks = null,
        IReadOnlyCollection<string>? invalidations = null,
        IReadOnlyCollection<AgentEvidence>? evidence = null,
        int schemaVersion = AgentAnalysisResult.CurrentSchemaVersion)
    {
        var source = new ContextSourceReference("snapshot", context.MarketSnapshot.Id.ToString());
        return new AgentAnalysisResult(
            new AgentRunId(run),
            new AgentId(agent),
            AgentVersion.Parse("1.0.0"),
            context.Id,
            status,
            Now,
            Now.AddSeconds(1),
            bias,
            AgentConfidence.FromScore(confidence, coverage, freshness),
            $"{bias} analysis from {agent}",
            observations: [new AgentObservation("signal", AgentObservationImportance.High, $"{bias} observation", references: [source])],
            evidence: evidence ?? [new AgentEvidence(ContextProviderCategory.MarketSnapshot, source, "Snapshot evidence.", 90, ContextFreshness.Fresh, Now)],
            marketLevels: levels,
            scenarios: scenarios,
            invalidations: invalidations,
            risks: risks,
            errors: status is AgentAnalysisStatus.Succeeded or AgentAnalysisStatus.PartiallySucceeded
                ? []
                : [new AgentError(AgentErrorCode.AgentUnavailable, "Synthetic unavailable result.")],
            schemaVersion: schemaVersion);
    }

    public static ConsensusRequest Request(
        MarketContext context,
        IReadOnlyCollection<AgentAnalysisResult>? analyses = null,
        TimeSpan? timeout = null) =>
        new(ConsensusId.New(), context.Id, analyses ?? [], timeout: timeout);

    public static IConsensusEngine Engine(
        ConsensusOptions? options = null,
        IConsensusEligibilityPolicy? eligibility = null,
        IConsensusWeightingPolicy? weighting = null,
        IConsensusConflictPolicy? conflicts = null,
        TimeProvider? timeProvider = null)
    {
        var configured = options ?? new ConsensusOptions();
        return new ConsensusEngine(
            eligibility ?? new DefaultConsensusEligibilityPolicy(Options.Create(configured)),
            weighting ?? new DefaultConsensusWeightingPolicy(Options.Create(configured)),
            conflicts ?? new DefaultConsensusConflictPolicy(Options.Create(configured)),
            Options.Create(configured),
            timeProvider ?? new FixedTimeProvider(Now),
            NullLogger<ConsensusEngine>.Instance);
    }

    public static AgentMarketLevel Level(string id, AgentMarketLevelType type, decimal price, string timeframe = "H1") =>
        new(id, type, new Price(price), timeframe == "H1" ? Timeframe.H1 : Timeframe.H4, AgentObservationImportance.High, $"{type} at {price}");

    public static AgentScenario Scenario(string id, AgentDirectionalBias direction, string activation, string invalidation, TimeSpan? horizon = null) =>
        new(id, id, $"Scenario {id}", direction, [activation], [invalidation], horizon: horizon ?? TimeSpan.FromHours(4), confidence: AgentConfidence.FromScore(80, 90, 90));
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class DelayedEligibilityPolicy(TimeSpan delay) : IConsensusEligibilityPolicy
{
    private readonly DefaultConsensusEligibilityPolicy _inner = new(Options.Create(new ConsensusOptions()));

    public async ValueTask<ConsensusEligibilityDecision> EvaluateAsync(
        AgentAnalysisResult result,
        ConsensusRequest request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
        return await _inner.EvaluateAsync(result, request, cancellationToken);
    }
}
