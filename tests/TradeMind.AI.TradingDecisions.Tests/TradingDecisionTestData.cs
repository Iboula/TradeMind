using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.TradingDecisions.Tests;

internal static class TradingDecisionTestData
{
    public static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    public static readonly Instrument Instrument = new("EURUSD");
    public static readonly Timeframe H1 = Timeframe.H1;

    public static ConsensusResult Consensus(
        AgentDirectionalBias bias = AgentDirectionalBias.Bullish,
        ConsensusLevel level = ConsensusLevel.Strong,
        double confidence = 90,
        double coverage = 90,
        ConsensusStatus status = ConsensusStatus.Succeeded,
        IReadOnlyCollection<ConsensusScenario>? scenarios = null,
        IReadOnlyCollection<ConsensusMarketLevel>? levels = null,
        IReadOnlyCollection<ConsensusRisk>? risks = null,
        IReadOnlyCollection<ConsensusInvalidation>? invalidations = null,
        IReadOnlyCollection<ConsensusConflict>? conflicts = null,
        IReadOnlyCollection<ConsensusSourceTrace>? sources = null)
    {
        var consensusId = ConsensusId.New();
        var contextId = MarketContextId.New();
        var runIds = CollectRuns(scenarios, levels, risks, invalidations, sources);
        var traces = sources?.ToArray() ?? runIds.Select(run => Source(run.Value)).ToArray();
        return new ConsensusResult(
            consensusId,
            contextId,
            ConsensusStrategy.WeightedTransparent,
            status,
            level,
            bias,
            new ConsensusConfidence(
                confidence,
                confidence >= 70 ? ConsensusConfidenceBand.High : ConsensusConfidenceBand.Medium,
                coverage,
                bias is AgentDirectionalBias.Bullish or AgentDirectionalBias.Bearish ? 90 : 50,
                bias == AgentDirectionalBias.Mixed ? 50 : 5,
                100 - coverage,
                ["test-consensus"],
                ["Synthetic test evidence."]),
            new ConsensusScore(90),
            new ConsensusScore(5),
            new ConsensusConclusion($"Synthetic {bias} consensus.", bias, level),
            [],
            [],
            [],
            [],
            conflicts ?? [],
            levels ?? [],
            scenarios ?? [],
            risks ?? [],
            invalidations ?? [],
            traces,
            [],
            [],
            Now,
            Now.AddSeconds(1));
    }

    public static TradingDecisionRequest Request(
        ConsensusResult consensus,
        TimeSpan? timeout = null,
        ConsensusId? requestedConsensusId = null,
        int version = TradingDecisionRequest.CurrentVersion) =>
        new(
            TradingDecisionId.New(),
            requestedConsensusId ?? consensus.ConsensusId,
            consensus,
            consensus.MarketContextId,
            Instrument,
            H1,
            "user-1",
            "session-1",
            timeout: timeout,
            version: version);

    public static ConsensusScenario Scenario(
        AgentDirectionalBias direction = AgentDirectionalBias.Bullish,
        string id = "scenario-main",
        double confidence = 90,
        IReadOnlyCollection<string>? invalidations = null,
        IReadOnlyCollection<string>? risks = null,
        TimeSpan? horizon = null,
        IReadOnlyCollection<string>? levelIds = null) =>
        new(
            id,
            id,
            $"Synthetic {id}",
            direction,
            ["activation condition"],
            invalidations ?? ["below invalidation"],
            levelIds ?? ["entry", "stop", "target"],
            risks ?? [],
            horizon ?? TimeSpan.FromHours(4),
            confidence,
            [new AgentRunId("run-1")]);

    public static ConsensusMarketLevel Level(
        AgentMarketLevelType type,
        decimal price,
        string run = "run-1",
        string timeframe = "H1") =>
        new(
            type,
            new Price(price),
            timeframe == "H1" ? Timeframe.H1 : Timeframe.H4,
            AgentObservationImportance.High,
            $"Explicit {type} level",
            [new AgentRunId(run)],
            references: [new ContextSourceReference("level", $"{type}-{price}")]);

    public static ConsensusInvalidation Invalidation(string value = "below invalidation", string run = "run-1") =>
        new(value, [new AgentRunId(run)]);

    public static ConsensusRisk Risk(
        string description,
        ConsensusRiskSeverity severity,
        string run = "run-1") =>
        new(
            description,
            severity,
            [new AgentRunId(run)],
            [new ContextSourceReference("risk", description)]);

    public static ConsensusConflict CriticalConflict(string run = "run-1") =>
        new(
            ConsensusConflictKind.Directional,
            ConsensusConflictSeverity.Critical,
            "Critical Bullish/Bearish conflict",
            [new AgentRunId(run)],
            [new ContextSourceReference("conflict", "directional")]);

    public static ConsensusSourceTrace Source(string run = "run-1") =>
        new(
            new AgentRunId(run),
            new AgentId($"agent-{run}"),
            AgentVersion.Parse("1.0.0"),
            [new ContextSourceReference("analysis", run)]);

    public static ITradingDecisionEngine Engine(
        TradingDecisionOptions? options = null,
        ITradingDecisionEligibilityPolicy? eligibility = null,
        ITradingDecisionPolicy? policy = null,
        TimeProvider? timeProvider = null) =>
        new TradingDecisionEngine(
            eligibility ?? new DefaultTradingDecisionEligibilityPolicy(Options.Create(options ?? new TradingDecisionOptions())),
            policy ?? new DefaultTradingDecisionPolicy(Options.Create(options ?? new TradingDecisionOptions())),
            Options.Create(options ?? new TradingDecisionOptions()),
            timeProvider ?? new FixedTimeProvider(Now),
            NullLogger<TradingDecisionEngine>.Instance);

    public static IReadOnlyCollection<AgentRunId> CollectRuns(
        IReadOnlyCollection<ConsensusScenario>? scenarios,
        IReadOnlyCollection<ConsensusMarketLevel>? levels,
        IReadOnlyCollection<ConsensusRisk>? risks,
        IReadOnlyCollection<ConsensusInvalidation>? invalidations,
        IReadOnlyCollection<ConsensusSourceTrace>? sources) =>
        (scenarios ?? [])
            .SelectMany(item => item.SourceRuns)
            .Concat((levels ?? []).SelectMany(item => item.SourceRuns))
            .Concat((risks ?? []).SelectMany(item => item.SourceRuns))
            .Concat((invalidations ?? []).SelectMany(item => item.SourceRuns))
            .Concat((sources ?? []).Select(item => item.AgentRunId))
            .Distinct()
            .OrderBy(item => item.Value, StringComparer.Ordinal)
            .ToArray();
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class DelayedEligibilityPolicy(TimeSpan delay) : ITradingDecisionEligibilityPolicy
{
    private readonly DefaultTradingDecisionEligibilityPolicy _inner = new(Options.Create(new TradingDecisionOptions()));

    public async ValueTask<TradingDecisionEligibilityDecision> EvaluateAsync(
        TradingDecisionRequest request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
        return await _inner.EvaluateAsync(request, cancellationToken);
    }
}
