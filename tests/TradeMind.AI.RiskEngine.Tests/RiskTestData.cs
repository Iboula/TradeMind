using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.RiskEngine.Tests;

internal static class RiskTestData
{
    public static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
    public static readonly Instrument Instrument = new("EURUSD");
    public static readonly Timeframe Timeframe = Timeframe.H1;
    public static readonly CurrencyCode Usd = new("USD");

    public static TradingDecisionResult Decision(
        TradingDecisionType type = TradingDecisionType.LongSetup,
        decimal entry = 1.1000m,
        decimal? stop = null,
        IReadOnlyCollection<decimal>? targets = null,
        TradingDecisionStatus status = TradingDecisionStatus.Succeeded,
        Instrument? instrument = null,
        Timeframe? timeframe = null,
        IReadOnlyCollection<DecisionRisk>? risks = null,
        IReadOnlyCollection<DecisionTraceReference>? traces = null)
    {
        var selectedInstrument = instrument ?? Instrument;
        var selectedTimeframe = timeframe ?? Timeframe;
        var selectedStop = stop ?? (type == TradingDecisionType.ShortSetup ? 1.1100m : 1.0900m);
        var decisionId = TradingDecisionId.New();
        var consensusId = ConsensusId.New();
        var contextId = MarketContextId.New();
        var run = new AgentRunId("run-risk-test");
        var reference = new ContextSourceReference("risk-test", "synthetic");
        var scenario = new ConsensusScenario(
            "risk-scenario",
            "Risk scenario",
            "Synthetic risk scenario",
            type == TradingDecisionType.ShortSetup ? AgentDirectionalBias.Bearish : AgentDirectionalBias.Bullish,
            ["activation"],
            ["invalidation"],
            ["entry", "stop"],
            [],
            TimeSpan.FromHours(1),
            90,
            [run]);
        var primary = new DecisionScenario(scenario, true, 90, "Deterministic test scenario.");
        var entryProposal = type is TradingDecisionType.LongSetup or TradingDecisionType.ShortSetup
            ? new EntryProposal(selectedInstrument, selectedTimeframe, new Price(entry), AgentMarketLevelType.Entry, [run], [reference], "Explicit test entry.")
            : null;
        var stopProposal = type is TradingDecisionType.LongSetup or TradingDecisionType.ShortSetup
            ? new StopProposal(selectedInstrument, selectedTimeframe, new Price(selectedStop), AgentMarketLevelType.Stop, [run], [reference], "Explicit test stop.")
            : null;
        var targetProposals = (targets ?? [type == TradingDecisionType.ShortSetup ? 1.0800m : 1.1200m])
            .Select((price, index) => new TargetProposal(selectedInstrument, selectedTimeframe, new Price(price), index + 1, [run], [reference], "Explicit test target."))
            .ToArray();
        var consensusRisk = new ConsensusRisk("Synthetic risk", ConsensusRiskSeverity.High, [run], [reference]);
        var decisionRisks = risks ?? [new DecisionRisk(consensusRisk, "Preserved test risk.")];
        var decisionTraces = traces ??
        [
            new DecisionTraceReference(run, new AgentId("risk-agent"), AgentVersion.Parse("1.0.0"), reference, DecisionTraceRole.Consensus)
        ];

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
            primary,
            [],
            entryProposal,
            stopProposal,
            targetProposals,
            decisionRisks,
            [],
            [],
            decisionTraces,
            [],
            [],
            Now,
            Now.AddSeconds(1));
    }

    public static RiskProfile Profile(
        decimal? percent = 1,
        Money? amount = null,
        Money? daily = null,
        Money? weekly = null,
        Money? drawdown = null,
        Money? portfolioRisk = null,
        decimal minimumR = 1,
        IReadOnlyCollection<RiskLimit>? additional = null) =>
        new(
            "profile-risk-test",
            Usd,
            percent,
            amount ?? new Money(200, Usd),
            daily ?? new Money(500, Usd),
            weekly ?? new Money(1000, Usd),
            drawdown ?? new Money(1000, Usd),
            portfolioRisk,
            new Money(1000, Usd),
            new Money(1000, Usd),
            minimumR,
            additional ?? []);

    public static AccountRiskContext Account(
        decimal balance = 10_000,
        decimal equity = 10_000,
        decimal daily = 0,
        decimal weekly = 0,
        decimal drawdown = 0,
        decimal accountRisk = 0,
        decimal platformRisk = 0) =>
        new(
            new Money(balance, Usd),
            new Money(equity, Usd),
            new Money(daily, Usd),
            new Money(weekly, Usd),
            new Money(drawdown, Usd),
            new Money(accountRisk, Usd),
            new Money(platformRisk, Usd));

    public static InstrumentRiskSpecification Specification(
        decimal tickSize = 0.0001m,
        decimal tickValue = 10,
        decimal step = 0.01m,
        decimal minimum = 0.01m,
        decimal maximum = 100,
        Money? exposurePerQuantity = null) =>
        new(Instrument, Usd, tickSize, new Money(tickValue, Usd), step, minimum, maximum, exposurePerQuantity);

    public static PortfolioRiskContext Portfolio(decimal currentExposure = 0, decimal? maximumExposure = null) =>
        new(new Money(0, Usd), new Money(currentExposure, Usd), maximumExposure is { } maximum ? new Money(maximum, Usd) : null);

    public static RiskAssessmentRequest Request(
        TradingDecisionResult? decision = null,
        RiskProfile? profile = null,
        AccountRiskContext? account = null,
        InstrumentRiskSpecification? specification = null,
        PortfolioRiskContext? portfolio = null,
        TimeSpan? timeout = null,
        TradingDecisionId? requestedDecisionId = null) =>
        BuildRequest(decision ?? Decision(), profile, account, specification, portfolio, timeout, requestedDecisionId);

    private static RiskAssessmentRequest BuildRequest(
        TradingDecisionResult decision,
        RiskProfile? profile,
        AccountRiskContext? account,
        InstrumentRiskSpecification? specification,
        PortfolioRiskContext? portfolio,
        TimeSpan? timeout,
        TradingDecisionId? requestedDecisionId) =>
        new(
            RiskAssessmentId.New(),
            requestedDecisionId ?? decision.DecisionId,
            decision,
            profile ?? Profile(),
            account ?? Account(),
            specification ?? Specification(),
            portfolio,
            RiskStrategy.Conservative,
            timeout);

    public static IRiskEngine Engine(
        RiskEngineOptions? options = null,
        IRiskEligibilityPolicy? eligibility = null,
        TimeProvider? timeProvider = null,
        IPositionSizingPolicy? sizing = null,
        IRiskReductionPolicy? reduction = null) =>
        new TradeMind.AI.RiskEngine.Application.RiskEngine(
            eligibility ?? new DefaultRiskEligibilityPolicy(Options.Create(options ?? new RiskEngineOptions())),
            sizing ?? new DefaultPositionSizingPolicy(),
            reduction ?? new DefaultRiskReductionPolicy(),
            Options.Create(options ?? new RiskEngineOptions()),
            timeProvider ?? new FixedTimeProvider(Now),
            NullLogger<TradeMind.AI.RiskEngine.Application.RiskEngine>.Instance);
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class DelayedRiskEligibilityPolicy(TimeSpan delay) : IRiskEligibilityPolicy
{
    public async ValueTask<RiskEligibilityDecision> EvaluateAsync(RiskAssessmentRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
        return RiskEligibilityDecision.Include();
    }
}
