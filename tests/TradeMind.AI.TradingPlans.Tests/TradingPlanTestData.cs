using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.TradingPlans.Tests;

internal static class TradingPlanTestData
{
    public static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
    public static readonly Instrument Instrument = new("EURUSD");
    public static readonly Timeframe Timeframe = Timeframe.H1;
    public static readonly CurrencyCode Usd = new("USD");

    public static TradingDecisionResult Decision(
        TradingDecisionType type = TradingDecisionType.LongSetup,
        decimal? entry = 1.1000m,
        decimal? stop = null,
        bool includeStop = true,
        IReadOnlyCollection<decimal>? targets = null,
        bool primaryScenario = true,
        IReadOnlyCollection<string>? invalidations = null,
        bool invalidationCoherent = true,
        IReadOnlyCollection<DecisionScenario>? alternatives = null,
        IReadOnlyCollection<DecisionRisk>? risks = null,
        IReadOnlyCollection<ConsensusConflict>? conflicts = null,
        TradingDecisionStatus status = TradingDecisionStatus.Succeeded,
        Instrument? instrument = null,
        Timeframe? timeframe = null,
        DateTimeOffset? completedAtUtc = null,
        IReadOnlyCollection<DecisionTraceReference>? traces = null)
    {
        var selectedInstrument = instrument ?? Instrument;
        var selectedTimeframe = timeframe ?? Timeframe;
        var selectedStop = stop ?? (type == TradingDecisionType.ShortSetup ? 1.1100m : 1.0900m);
        var decisionId = TradingDecisionId.New();
        var consensusId = ConsensusId.New();
        var contextId = MarketContextId.New();
        var run = new AgentRunId("plan-test-run");
        var reference = new ContextSourceReference("plan-test", "synthetic");
        var scenarioSource = new ConsensusScenario(
            "scenario-main",
            "Main scenario",
            "Synthetic plan scenario",
            type == TradingDecisionType.ShortSetup ? AgentDirectionalBias.Bearish : AgentDirectionalBias.Bullish,
            ["activation-confirmed", "session-open"],
            ["invalidation-reached"],
            ["entry", "stop", "target"],
            [],
            TimeSpan.FromHours(4),
            90,
            [run]);
        var scenario = primaryScenario ? new DecisionScenario(scenarioSource, true, 90, "Selected by test.") : null;
        var directional = type is TradingDecisionType.LongSetup or TradingDecisionType.ShortSetup;
        var entryProposal = directional && entry is { } entryValue
            ? new EntryProposal(selectedInstrument, selectedTimeframe, new Price(entryValue), AgentMarketLevelType.Entry, [run], [reference], "Explicit plan entry.")
            : null;
        var stopProposal = directional && entry is not null && includeStop
            ? new StopProposal(selectedInstrument, selectedTimeframe, new Price(selectedStop), AgentMarketLevelType.Stop, [run], [reference], "Explicit plan stop.")
            : null;
        var targetValues = targets ?? (directional ? [type == TradingDecisionType.ShortSetup ? 1.0800m : 1.1200m] : []);
        var targetProposals = targetValues
            .Select((price, index) => new TargetProposal(selectedInstrument, selectedTimeframe, new Price(price), index + 1, [run], [reference], "Explicit plan target."))
            .ToArray();
        var invalidationValues = invalidations ?? ["invalidation-reached"];
        var expectedBias = type == TradingDecisionType.ShortSetup ? AgentDirectionalBias.Bearish : AgentDirectionalBias.Bullish;
        var decisionInvalidations = invalidationValues
            .Select(value => new DecisionInvalidation(
                new ConsensusInvalidation(value, [run]),
                expectedBias,
                invalidationCoherent,
                "Explicit plan invalidation."))
            .ToArray();
        var decisionRisk = new DecisionRisk(
            new ConsensusRisk("Critical plan risk", ConsensusRiskSeverity.Critical, [run], [reference]),
            "Preserved critical risk.");
        var decisionTraces = traces ??
        [
            new DecisionTraceReference(run, new AgentId("plan-agent"), AgentVersion.Parse("1.0.0"), reference, DecisionTraceRole.Scenario)
        ];
        var createdAt = (completedAtUtc ?? Now).AddMinutes(-1);

        return new TradingDecisionResult(
            decisionId,
            consensusId,
            contextId,
            selectedInstrument,
            selectedTimeframe,
            TradingDecisionStrategy.Conservative,
            status,
            type,
            new TradingDecisionConfidence(90, TradingDecisionConfidenceBand.High, 90, 90, ["synthetic"], ["test-only"]),
            scenario,
            alternatives ?? [],
            entryProposal,
            stopProposal,
            targetProposals,
            risks ?? [decisionRisk],
            decisionInvalidations,
            conflicts ?? [],
            decisionTraces,
            [],
            [],
            createdAt,
            completedAtUtc ?? Now);
    }

    public static RiskAssessmentResult Risk(
        TradingDecisionResult decision,
        RiskVerdict verdict = RiskVerdict.Approved,
        RiskAssessmentStatus status = RiskAssessmentStatus.Succeeded,
        decimal quantity = 0.10m,
        Instrument? instrument = null,
        Timeframe? timeframe = null,
        MarketContextId? contextId = null,
        MarketDirection? stopDirection = null,
        DateTimeOffset? completedAtUtc = null,
        IReadOnlyCollection<RiskConstraintEvaluation>? constraints = null)
    {
        var selectedInstrument = instrument ?? decision.Instrument;
        var selectedTimeframe = timeframe ?? decision.Timeframe;
        var riskId = RiskAssessmentId.New();
        var entry = decision.Entry?.Price ?? new Price(1.1000m);
        var stop = decision.Stop?.Price ?? new Price(1.0900m);
        var direction = stopDirection ?? (decision.Type == TradingDecisionType.ShortSetup ? MarketDirection.Short : MarketDirection.Long);
        var distance = decimal.Abs(entry.Value - stop.Value);
        var stopDistance = decision.Type is TradingDecisionType.LongSetup or TradingDecisionType.ShortSetup
            ? new StopDistance(entry, stop, distance, 0.0001m, distance / 0.0001m, direction)
            : null;
        var position = verdict is RiskVerdict.Approved or RiskVerdict.Reduced && decision.Type is TradingDecisionType.LongSetup or TradingDecisionType.ShortSetup
            ? new PositionSizeProposal(quantity, quantity, quantity, new Money(1000, Usd), new Money(quantity * 1000, Usd), null, 0.01m, 0.01m, 100, verdict == RiskVerdict.Reduced, verdict == RiskVerdict.Reduced ? ["test-reduction"] : [])
            : null;
        var constraint = new RiskConstraintEvaluation(RiskConstraintKind.DailyLoss, 500, 0, 500, RiskUnit.Money, Usd, "test-daily", true, false, RiskConstraintImpact.NotBinding);
        var budget = new EffectiveRiskBudget(new Money(100, Usd), new Money(100, Usd), RiskConstraintKind.RiskAmountPerTrade, "test-budget", [constraint], verdict is RiskVerdict.Approved or RiskVerdict.Reduced, verdict == RiskVerdict.Reduced);
        var completed = completedAtUtc ?? Now;

        return new RiskAssessmentResult(
            riskId,
            decision.DecisionId,
            contextId ?? decision.MarketContextId,
            selectedInstrument,
            selectedTimeframe,
            RiskStrategy.Conservative,
            status,
            verdict,
            budget,
            stopDistance,
            position,
            [],
            null,
            null,
            constraints ?? [constraint],
            decision.Risks,
            decision.Traces,
            ["synthetic-risk"],
            [],
            [],
            (completed - TimeSpan.FromMinutes(1)),
            completed);
    }

    public static TradingPlanRequest Request(
        TradingDecisionResult? decision = null,
        RiskAssessmentResult? risk = null,
        TradingPlanId? planId = null,
        TradingDecisionId? decisionId = null,
        RiskAssessmentId? riskAssessmentId = null,
        MarketContextId? marketContextId = null,
        DateTimeOffset? expiresAtUtc = null,
        TimeSpan? timeout = null,
        int version = TradingPlanRequest.CurrentVersion)
    {
        var selectedDecision = decision ?? Decision();
        var selectedRisk = risk ?? Risk(selectedDecision);
        return new TradingPlanRequest(
            planId ?? TradingPlanId.New(),
            decisionId ?? selectedDecision.DecisionId,
            riskAssessmentId ?? selectedRisk.AssessmentId,
            marketContextId ?? selectedDecision.MarketContextId,
            selectedDecision,
            selectedRisk,
            TradingPlanStrategy.Conservative,
            expiresAtUtc,
            timeout,
            version);
    }

    public static ITradingPlanGenerator Generator(
        TradingPlanOptions? options = null,
        ITradingPlanEligibilityPolicy? eligibility = null,
        ITradingPlanExpirationPolicy? expiration = null,
        TimeProvider? timeProvider = null) =>
        new TradingPlanGenerator(
            eligibility ?? new DefaultTradingPlanEligibilityPolicy(Options.Create(options ?? new TradingPlanOptions())),
            expiration ?? new DefaultTradingPlanExpirationPolicy(Options.Create(options ?? new TradingPlanOptions())),
            Options.Create(options ?? new TradingPlanOptions()),
            timeProvider ?? new FixedTimeProvider(Now),
            NullLogger<TradingPlanGenerator>.Instance);
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class DelayedPlanEligibilityPolicy(TimeSpan delay) : ITradingPlanEligibilityPolicy
{
    public async ValueTask<TradingPlanEligibilityDecision> EvaluateAsync(TradingPlanRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
        return TradingPlanEligibilityDecision.Include();
    }
}
