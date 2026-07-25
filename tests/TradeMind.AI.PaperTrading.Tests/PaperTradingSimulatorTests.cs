using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Consensus.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.AI.PaperTrading.Application;
using TradeMind.AI.PaperTrading.Domain;
using TradeMind.AI.RiskEngine.Domain;
using TradeMind.AI.TradingDecisions.Domain;
using TradeMind.AI.TradingPlans.Domain;
using TradeMind.AI.TradingWorkspace.Domain;
using TradeMind.Market.Abstractions;
using Xunit;

namespace TradeMind.AI.PaperTrading.Tests;

public sealed class PaperTradingSimulatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Long_plan_fills_take_profit_and_builds_complete_outputs()
    {
        var source = PaperTradingTestData.Create();
        var simulator = PaperTradingTestData.Simulator();
        var result = await simulator.SimulateAsync(PaperTradingTestData.Request(source, [
            new(Now.AddMinutes(1), 100.8m, 101m),
            new(Now.AddMinutes(2), 99.9m, 100m),
            new(Now.AddMinutes(3), 102m, 102.1m)]), CancellationToken.None);

        Assert.Equal(PaperTradingStatus.Succeeded, result.Status);
        Assert.Equal(2m, result.RealizedPnl);
        Assert.Equal(2, result.Fills.Count);
        Assert.Equal(PaperExitReason.TakeProfit, result.Journal.Single().ExitReason);
        Assert.Equal(1, result.Statistics.WinningTrades);
        Assert.Contains(result.Timeline, item => item.Type == PaperTradingEventType.TakeProfitTriggered);
        Assert.NotEmpty(result.EquityCurve);
        Assert.False(string.IsNullOrWhiteSpace(result.ReplayFingerprint));
    }

    [Fact]
    public async Task Short_plan_fills_stop_loss_with_directional_pnl()
    {
        var source = PaperTradingTestData.Create(TradingPlanDirection.Short);
        var simulator = PaperTradingTestData.Simulator();
        var result = await simulator.SimulateAsync(PaperTradingTestData.Request(source, [
            new(Now.AddMinutes(1), 100m, 100.1m),
            new(Now.AddMinutes(2), 110m, 110.2m)]), CancellationToken.None);

        Assert.Equal(PaperTradingStatus.Succeeded, result.Status);
        Assert.Equal(-10.2m, result.RealizedPnl);
        Assert.Equal(PaperExitReason.StopLoss, result.Journal.Single().ExitReason);
        Assert.Equal(1, result.Statistics.LosingTrades);
    }

    [Fact]
    public async Task Unreached_entry_is_normal_no_fill_and_does_not_throw()
    {
        var source = PaperTradingTestData.Create();
        var result = await PaperTradingTestData.Simulator().SimulateAsync(
            PaperTradingTestData.Request(source, [new(Now.AddMinutes(1), 101m, 101.1m)]),
            CancellationToken.None);

        Assert.Equal(PaperTradingStatus.NoFill, result.Status);
        Assert.Empty(result.Fills);
        Assert.Empty(result.Journal);
        Assert.Equal(PaperOrderStatus.Expired, result.Orders.Single().Status);
        Assert.Contains(result.Warnings, warning => warning.Code == "ENTRY_NOT_FILLED");
    }

    [Fact]
    public async Task Invalid_plan_is_rejected_without_creating_orders()
    {
        var source = PaperTradingTestData.Create(withEntry: false);
        var result = await PaperTradingTestData.Simulator().SimulateAsync(
            PaperTradingTestData.Request(source, []),
            CancellationToken.None);

        Assert.Equal(PaperTradingStatus.Invalid, result.Status);
        Assert.Empty(result.Orders);
        Assert.Contains(result.Errors, error => error.Code == "ENTRY_MISSING");
    }

    [Fact]
    public async Task Non_directional_plan_is_a_normal_no_trade_result()
    {
        var source = PaperTradingTestData.Create(planType: TradingPlanType.NoTradePlan);
        var result = await PaperTradingTestData.Simulator().SimulateAsync(
            PaperTradingTestData.Request(source, []),
            CancellationToken.None);

        Assert.Equal(PaperTradingStatus.NoTrade, result.Status);
        Assert.Empty(result.Orders);
        Assert.Contains(result.Warnings, warning => warning.Code == "NO_TRADE_PLAN");
    }

    [Fact]
    public async Task Configured_price_limit_is_deterministic_and_visible()
    {
        var source = PaperTradingTestData.Create();
        var simulator = PaperTradingTestData.Simulator(new PaperTradingOptions { MaximumPricePoints = 1 });
        var result = await simulator.SimulateAsync(PaperTradingTestData.Request(source, [
            new(Now.AddMinutes(1), 99.9m, 100m),
            new(Now.AddMinutes(2), 102m, 102.1m)]), CancellationToken.None);

        Assert.Equal(PaperTradingStatus.PartiallySucceeded, result.Status);
        Assert.Contains(result.Warnings, warning => warning.Code == "PRICE_PATH_TRUNCATED");
        Assert.NotNull(result.OpenPosition);
    }

    [Fact]
    public async Task Open_position_exposes_unrealized_pnl_and_journal_entry()
    {
        var source = PaperTradingTestData.Create();
        var result = await PaperTradingTestData.Simulator().SimulateAsync(
            PaperTradingTestData.Request(source, [new(Now.AddMinutes(1), 99.9m, 100m)]),
            CancellationToken.None);

        Assert.Equal(PaperTradingStatus.Succeeded, result.Status);
        Assert.Equal(-0.1m, result.UnrealizedPnl);
        Assert.NotNull(result.OpenPosition);
        Assert.Single(result.Journal);
        Assert.False(result.Journal.Single().IsClosed);
    }

    [Fact]
    public async Task Price_path_is_sorted_and_replay_is_identical()
    {
        var source = PaperTradingTestData.Create();
        var points = new[]
        {
            new PaperPricePoint(Now.AddMinutes(3), 102m, 102.1m),
            new PaperPricePoint(Now.AddMinutes(2), 99.9m, 100m),
            new PaperPricePoint(Now.AddMinutes(1), 100.8m, 101m)
        };
        var simulator = PaperTradingTestData.Simulator();
        var first = await simulator.SimulateAsync(PaperTradingTestData.Request(source, points), CancellationToken.None);
        var second = await simulator.SimulateAsync(PaperTradingTestData.Request(source, points.Reverse().ToArray()), CancellationToken.None);

        Assert.Equal(first.ReplayFingerprint, second.ReplayFingerprint);
        Assert.Equal(first.RealizedPnl, second.RealizedPnl);
        Assert.Equal(first.Timeline.Select(item => item.Type), second.Timeline.Select(item => item.Type));
    }

    [Fact]
    public async Task External_cancellation_is_propagated()
    {
        var source = PaperTradingTestData.Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            PaperTradingTestData.Simulator().SimulateAsync(PaperTradingTestData.Request(source, []), cancellation.Token));
    }

    [Fact]
    public void Request_and_price_path_are_defensively_copied()
    {
        var source = PaperTradingTestData.Create();
        var points = new List<PaperPricePoint> { new(Now, 100m, 100.1m) };
        var request = PaperTradingTestData.Request(source, points);
        points.Clear();

        Assert.Single(request.PricePath);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaperPricePoint(Now, 0, 1));
    }

    [Fact]
    public void Dependency_injection_registers_scoped_simulator_and_validates_options()
    {
        using var provider = new ServiceCollection()
            .AddTradeMindPaperTrading(options => options.MaximumPricePoints = 10)
            .BuildServiceProvider();

        Assert.IsType<PaperTradingSimulator>(provider.GetRequiredService<IPaperTradingSimulator>());
        using var invalid = new ServiceCollection()
            .AddTradeMindPaperTrading(options => options.PointValue = 0)
            .BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => invalid.GetRequiredService<IOptions<PaperTradingOptions>>().Value);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

internal static class PaperTradingTestData
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    public static (TradingWorkspaceResult Workspace, TradingPlanResult Plan) Create(
        TradingPlanDirection direction = TradingPlanDirection.Long,
        bool withEntry = true,
        TradingPlanType planType = TradingPlanType.ExecutableCandidate)
    {
        var instrument = new Instrument("EURUSD");
        var timeframe = Timeframe.H1;
        var contextId = new MarketContextId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var decisionId = new TradingDecisionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var assessmentId = new RiskAssessmentId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var planId = new TradingPlanId(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        var run = new AgentRunId("paper-run");
        var agentId = new AgentId("paper-agent");
        var reference = new ContextSourceReference("paper-test", "paper-source");
        var bias = direction == TradingPlanDirection.Long ? AgentDirectionalBias.Bullish : AgentDirectionalBias.Bearish;
        var scenarioSource = new ConsensusScenario("paper-scenario", "Paper scenario", "Scenario for paper simulation.", bias, ["activation"], ["invalidation"], ["entry", "stop", "target"], [], TimeSpan.FromHours(1), 90, [run]);
        var scenario = new DecisionScenario(scenarioSource, true, 90, "Deterministic test selection.");
        var entryPrice = new Price(100m);
        var stopPrice = new Price(direction == TradingPlanDirection.Long ? 90m : 110m);
        var targetPrice = new Price(direction == TradingPlanDirection.Long ? 102m : 98m);
        var entryProposal = withEntry ? new EntryProposal(instrument, timeframe, entryPrice, AgentMarketLevelType.Entry, [run], [reference], "Test entry.") : null;
        var stopProposal = withEntry ? new StopProposal(instrument, timeframe, stopPrice, AgentMarketLevelType.Stop, [run], [reference], "Test stop.") : null;
        var targetProposals = withEntry ? new[] { new TargetProposal(instrument, timeframe, targetPrice, 1, [run], [reference], "Test target.") } : Array.Empty<TargetProposal>();
        var invalidation = new DecisionInvalidation(new ConsensusInvalidation("paper-invalidation", [run]), bias, true, "Test invalidation.");
        var risk = new DecisionRisk(new ConsensusRisk("Paper risk", ConsensusRiskSeverity.Medium, [run], [reference]), "Test risk.");
        var trace = new DecisionTraceReference(run, agentId, AgentVersion.Parse("1.0.0"), reference, DecisionTraceRole.Entry);
        var decision = new TradingDecisionResult(
            decisionId,
            new ConsensusId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            contextId,
            instrument,
            timeframe,
            TradingDecisionStrategy.ConsensusAligned,
            TradingDecisionStatus.Succeeded,
            direction == TradingPlanDirection.Long ? TradingDecisionType.LongSetup : TradingDecisionType.ShortSetup,
            new TradingDecisionConfidence(90, TradingDecisionConfidenceBand.High, 90, 90),
            scenario,
            [],
            entryProposal,
            stopProposal,
            targetProposals,
            [risk],
            [invalidation],
            [],
            [trace],
            [],
            [],
            Now,
            Now.AddMinutes(1));
        var currency = new CurrencyCode("USD");
        var position = new PositionSizeProposal(1m, 1m, 1m, new Money(100, currency), new Money(100, currency), null, 1m, 1m, 100m, false);
        var assessment = new RiskAssessmentResult(
            assessmentId,
            decisionId,
            contextId,
            instrument,
            timeframe,
            RiskStrategy.Conservative,
            RiskAssessmentStatus.Succeeded,
            RiskVerdict.Approved,
            null,
            null,
            position,
            [],
            null,
            null,
            [],
            [risk],
            [trace],
            [],
            [],
            [],
            Now,
            Now.AddMinutes(1));
        var plan = new TradingPlanResult(
            planId,
            decisionId,
            assessmentId,
            contextId,
            instrument,
            timeframe,
            direction,
            TradingPlanStrategy.DecisionAligned,
            TradingPlanStatus.Succeeded,
            planType,
            RiskVerdict.Approved,
            new TradingPlanScenario(scenario, true),
            [],
            entryProposal is null ? null : new TradingPlanEntry(entryProposal),
            stopProposal is null ? null : new TradingPlanStop(stopProposal),
            targetProposals.Select(target => new TradingPlanTarget(target)).ToArray(),
            new TradingPlanQuantity(position),
            [],
            [new TradingPlanInvalidation(invalidation)],
            [],
            new PreTradeChecklist([]),
            [],
            [new TradingPlanRisk(risk)],
            [new TradingPlanTraceReference(trace, "paper-test")],
            [],
            [],
            [],
            "Deterministic paper plan.",
            Now,
            Now.AddMinutes(1),
            Now.AddHours(1));
        var workspace = new TradingWorkspaceResult(
            workspaceId: new WorkspaceId(Guid.Parse("66666666-6666-6666-6666-666666666666")),
            TradingWorkspaceStatus.Succeeded,
            TradingWorkspaceState.PlanReady,
            TradingWorkspaceBuildMode.Snapshot,
            contextId,
            instrument,
            timeframe,
            new WorkspaceContextSummary(null),
            new WorkspaceAnalysisSummary(null, null, []),
            new WorkspaceConsensusSummary(null),
            new WorkspaceDecisionSummary(null),
            new WorkspaceRiskSummary(null),
            new WorkspacePlanSummary(plan, Now),
            new WorkspacePipelineProgress([]),
            new WorkspaceCompleteness(0, 1, 0, []),
            new WorkspaceFreshness(Now, null, null, null, null, null, null, plan.ExpiresAtUtc, null, false, false, false, false, false, false),
            new WorkspaceConsistency([]),
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            "Ready paper workspace.",
            Now,
            Now.AddMinutes(2));
        return (workspace, plan);
    }

    public static PaperTradingRequest Request((TradingWorkspaceResult Workspace, TradingPlanResult Plan) source, IReadOnlyCollection<PaperPricePoint> points) =>
        new(new PaperTradingSessionId(Guid.Parse("77777777-7777-7777-7777-777777777777")), source.Workspace, source.Plan, points, 1_000m);

    public static IPaperTradingSimulator Simulator(PaperTradingOptions? selectedOptions = null)
    {
        var options = Options.Create(selectedOptions ?? new PaperTradingOptions());
        return new PaperTradingSimulator(
            new DefaultPaperTradingEligibilityPolicy(),
            options,
            new FixedTimeProvider(Now.AddMinutes(3)),
            NullLogger<PaperTradingSimulator>.Instance);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
