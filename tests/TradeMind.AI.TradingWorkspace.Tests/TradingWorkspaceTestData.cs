using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.TradingWorkspace.Tests;

internal static class TradingWorkspaceTestData
{
    public static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
    public static readonly Instrument Instrument = new("EURUSD");
    public static readonly Timeframe Timeframe = Timeframe.H1;
    public static readonly CurrencyCode Usd = new("USD");

    public static WorkspaceSources Sources(
        DateTimeOffset? contextBuiltAtUtc = null,
        DateTimeOffset? completedAtUtc = null,
        TradingDecisionType decisionType = TradingDecisionType.LongSetup,
        RiskVerdict riskVerdict = RiskVerdict.Approved,
        TradingPlanStatus planStatus = TradingPlanStatus.Succeeded,
        DateTimeOffset? planExpiresAtUtc = null,
        Instrument? instrument = null,
        Timeframe? timeframe = null,
        bool criticalConflict = false)
    {
        var selectedInstrument = instrument ?? Instrument;
        var selectedTimeframe = timeframe ?? Timeframe;
        var contextId = MarketContextId.New();
        var consensusId = ConsensusId.New();
        var decisionId = TradingDecisionId.New();
        var assessmentId = RiskAssessmentId.New();
        var planId = TradingPlanId.New();
        var run = new AgentRunId("workspace-run");
        var agentId = new AgentId("workspace-agent");
        var reference = new ContextSourceReference("workspace", "synthetic-source");
        var contextTime = contextBuiltAtUtc ?? Now;
        var completion = completedAtUtc ?? Now.AddMinutes(4);
        var direction = decisionType == TradingDecisionType.ShortSetup ? AgentDirectionalBias.Bearish : AgentDirectionalBias.Bullish;
        var scenarioSource = new ConsensusScenario(
            "workspace-scenario",
            "Workspace scenario",
            "Synthetic workspace scenario",
            direction,
            ["activation"],
            ["invalidation"],
            ["entry", "stop", "target"],
            [],
            TimeSpan.FromHours(4),
            90,
            [run]);
        var scenario = new DecisionScenario(scenarioSource, true, 90, "Selected deterministically.");
        var entryPrice = decisionType == TradingDecisionType.ShortSetup ? 1.1000m : 1.1000m;
        var stopPrice = decisionType == TradingDecisionType.ShortSetup ? 1.1100m : 1.0900m;
        var targetPrice = decisionType == TradingDecisionType.ShortSetup ? 1.0800m : 1.1200m;
        var entry = decisionType is TradingDecisionType.LongSetup or TradingDecisionType.ShortSetup
            ? new EntryProposal(selectedInstrument, selectedTimeframe, new Price(entryPrice), AgentMarketLevelType.Entry, [run], [reference], "Source entry.")
            : null;
        var stop = decisionType is TradingDecisionType.LongSetup or TradingDecisionType.ShortSetup
            ? new StopProposal(selectedInstrument, selectedTimeframe, new Price(stopPrice), AgentMarketLevelType.Stop, [run], [reference], "Source stop.")
            : null;
        var targets = decisionType is TradingDecisionType.LongSetup or TradingDecisionType.ShortSetup
            ? new[] { new TargetProposal(selectedInstrument, selectedTimeframe, new Price(targetPrice), 1, [run], [reference], "Source target.") }
            : Array.Empty<TargetProposal>();
        var invalidation = new DecisionInvalidation(new ConsensusInvalidation("invalidation", [run]), direction, true, "Source invalidation.");
        var decisionRisk = new DecisionRisk(new ConsensusRisk("Critical workspace risk", ConsensusRiskSeverity.Critical, [run], [reference]), "Source risk.");
        var trace = new DecisionTraceReference(run, agentId, AgentVersion.Parse("1.0.0"), reference, DecisionTraceRole.Consensus);
        var sourceCreated = contextTime.AddMinutes(1);
        var sourceCompleted = completion;
        var decision = new TradingDecisionResult(
            decisionId,
            consensusId,
            contextId,
            selectedInstrument,
            selectedTimeframe,
            TradingDecisionStrategy.ConsensusAligned,
            TradingDecisionStatus.Succeeded,
            decisionType,
            new TradingDecisionConfidence(90, TradingDecisionConfidenceBand.High, 90, 90, ["synthetic"], []),
            decisionType is TradingDecisionType.LongSetup or TradingDecisionType.ShortSetup ? scenario : null,
            [],
            entry,
            stop,
            targets,
            [decisionRisk],
            [invalidation],
            [],
            [trace],
            [],
            [],
            sourceCreated.AddMinutes(1),
            sourceCompleted);
        var consensus = new ConsensusResult(
            consensusId,
            contextId,
            ConsensusStrategy.WeightedTransparent,
            ConsensusStatus.Succeeded,
            ConsensusLevel.Strong,
            direction,
            new ConsensusConfidence(90, ConsensusConfidenceBand.High, 90, 90, 5, 5, ["synthetic"], []),
            new ConsensusScore(90),
            new ConsensusScore(5),
            new ConsensusConclusion("Synthetic consensus.", direction, ConsensusLevel.Strong),
            [],
            [],
            [],
            [],
            criticalConflict ? [new ConsensusConflict(ConsensusConflictKind.Directional, ConsensusConflictSeverity.Critical, "Synthetic critical conflict.", [run])] : [],
            [],
            [scenarioSource],
            [decisionRisk.Source],
            [invalidation.Source],
            [new ConsensusSourceTrace(run, agentId, AgentVersion.Parse("1.0.0"), [reference])],
            [],
            [],
            sourceCreated,
            sourceCreated.AddMinutes(1));
        var risk = Risk(assessmentId, decision, riskVerdict, sourceCompleted.AddMinutes(1));
        var plan = Plan(planId, decision, risk, planStatus, planExpiresAtUtc ?? completion.AddHours(1));
        var analysis = new AgentAnalysisResult(
            run,
            agentId,
            AgentVersion.Parse("1.0.0"),
            contextId,
            AgentAnalysisStatus.Succeeded,
            contextTime.AddSeconds(30),
            contextTime.AddMinutes(1),
            direction,
            new AgentConfidence(90, AgentConfidenceBand.High, dataCoverage: 90, contextFreshness: 100),
            "Synthetic analysis",
            evidence: [new AgentEvidence(ContextProviderCategory.MarketSnapshot, reference, "Synthetic evidence.", sourceTimestampUtc: contextTime)],
            marketLevels: [],
            scenarios: [],
            invalidations: ["invalidation"],
            risks: ["Critical workspace risk"]);
        var context = Context(contextId, selectedInstrument, selectedTimeframe, contextTime);
        return new WorkspaceSources(context, analysis, consensus, decision, risk, plan);
    }

    public static TradingWorkspaceRequest Request(WorkspaceSources sources) =>
        new(
            WorkspaceId.New(),
            sources.Context,
            analyses: [sources.Analysis],
            consensus: sources.Consensus,
            decision: sources.Decision,
            risk: sources.Risk,
            plan: sources.Plan);

    public static TradingWorkspaceRequest Empty() => new(WorkspaceId.New());

    public static ITradingWorkspaceBuilder Builder(
        TradingWorkspaceOptions? options = null,
        ITradingWorkspaceConsistencyPolicy? consistency = null,
        ITradingWorkspaceFreshnessPolicy? freshness = null,
        TimeProvider? timeProvider = null) {
        var selectedOptions = options ?? new TradingWorkspaceOptions();
        return new TradingWorkspaceBuilder(
            consistency ?? new DefaultTradingWorkspaceConsistencyPolicy(),
            freshness ?? new DefaultTradingWorkspaceFreshnessPolicy(selectedOptions),
            Options.Create(selectedOptions),
            timeProvider ?? new FixedWorkspaceTimeProvider(Now.AddMinutes(10)),
            NullLogger<TradingWorkspaceBuilder>.Instance);
    }

    private static MarketContext Context(MarketContextId id, Instrument instrument, Timeframe timeframe, DateTimeOffset builtAtUtc) {
        var snapshot = new MarketSnapshot(
            SnapshotId.New(),
            new ConnectorId("workspace-connector"),
            new ExternalAccountReference("workspace-account"),
            instrument,
            timeframe,
            builtAtUtc,
            builtAtUtc.AddSeconds(1),
            [],
            null,
            [],
            [],
            [],
            [],
            new SnapshotQuality(SnapshotFreshness.Live, ConnectorCapabilities.None, ConnectorCapabilities.None));
        var trace = new ContextSourceTrace(
            new ContextProviderId("workspace-context"),
            ContextProviderCategory.MarketSnapshot,
            ContextRequirement.Required,
            ContextProviderExecutionStatus.Succeeded,
            builtAtUtc,
            builtAtUtc,
            TimeSpan.Zero,
            builtAtUtc,
            ContextFreshness.Fresh,
            1,
            "1.0.0",
            [new ContextSourceReference("snapshot", "workspace-snapshot")]);
        return new MarketContext(
            id,
            MarketContext.CurrentVersion,
            "user-1",
            "session-1",
            instrument,
            timeframe,
            builtAtUtc,
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
            new ContextQuality(100, 100, 100, 100, ContextQualityBand.Excellent));
    }

    private static RiskAssessmentResult Risk(RiskAssessmentId id, TradingDecisionResult decision, RiskVerdict verdict, DateTimeOffset completedAtUtc) {
        var currency = Usd;
        var constraint = new RiskConstraintEvaluation(RiskConstraintKind.DailyLoss, 500, 0, 500, RiskUnit.Money, currency, "workspace-daily", true, false, RiskConstraintImpact.NotBinding);
        var budget = new EffectiveRiskBudget(new Money(100, currency), new Money(100, currency), RiskConstraintKind.RiskAmountPerTrade, "workspace-budget", [constraint], verdict is RiskVerdict.Approved or RiskVerdict.Reduced, verdict == RiskVerdict.Reduced);
        var entry = decision.Entry?.Price ?? new Price(1.1000m);
        var stop = decision.Stop?.Price ?? new Price(1.0900m);
        var direction = decision.Type == TradingDecisionType.ShortSetup ? MarketDirection.Short : MarketDirection.Long;
        var distance = decimal.Abs(entry.Value - stop.Value);
        var stopDistance = decision.Entry is null ? null : new StopDistance(entry, stop, distance, 0.0001m, distance / 0.0001m, direction);
        var position = verdict is RiskVerdict.Approved or RiskVerdict.Reduced && decision.Entry is not null
            ? new PositionSizeProposal(0.10m, 0.10m, 0.10m, new Money(1000, currency), new Money(100, currency), null, 0.01m, 0.01m, 100, verdict == RiskVerdict.Reduced)
            : null;
        return new RiskAssessmentResult(
            id,
            decision.DecisionId,
            decision.MarketContextId,
            decision.Instrument,
            decision.Timeframe,
            RiskStrategy.Conservative,
            verdict == RiskVerdict.Rejected ? RiskAssessmentStatus.Rejected : RiskAssessmentStatus.Succeeded,
            verdict,
            budget,
            stopDistance,
            position,
            [],
            null,
            null,
            [constraint],
            decision.Risks,
            decision.Traces,
            [],
            [],
            [],
            completedAtUtc.AddMinutes(-1),
            completedAtUtc);
    }

    private static TradingPlanResult Plan(TradingPlanId id, TradingDecisionResult decision, RiskAssessmentResult risk, TradingPlanStatus status, DateTimeOffset expiresAtUtc) =>
        new(
            id,
            decision.DecisionId,
            risk.AssessmentId,
            decision.MarketContextId,
            decision.Instrument,
            decision.Timeframe,
            decision.Type == TradingDecisionType.ShortSetup ? TradingPlanDirection.Short : TradingPlanDirection.Long,
            TradingPlanStrategy.DecisionAligned,
            status,
            status == TradingPlanStatus.Expired ? TradingPlanType.ExpiredPlan : TradingPlanType.ExecutableCandidate,
            risk.Verdict,
            decision.PrimaryScenario is null ? null : new TradingPlanScenario(decision.PrimaryScenario, true),
            [],
            decision.Entry is null ? null : new TradingPlanEntry(decision.Entry),
            decision.Stop is null ? null : new TradingPlanStop(decision.Stop),
            decision.Targets.Select(target => new TradingPlanTarget(target)).ToArray(),
            risk.PositionSize is null ? null : new TradingPlanQuantity(risk.PositionSize),
            [],
            decision.Invalidations.Select(invalidation => new TradingPlanInvalidation(invalidation)).ToArray(),
            [],
            new PreTradeChecklist([]),
            [],
            decision.Risks.Select(riskItem => new TradingPlanRisk(riskItem)).ToArray(),
            decision.Traces.Select(trace => new TradingPlanTraceReference(trace, "decision")).ToArray(),
            [new TradingPlanLimitation("TEST", "Synthetic plan.")],
            [],
            [],
            "Synthetic trading plan.",
            risk.CompletedAtUtc,
            risk.CompletedAtUtc.AddSeconds(1),
            expiresAtUtc);
}

internal sealed record WorkspaceSources(
    MarketContext Context,
    AgentAnalysisResult Analysis,
    ConsensusResult Consensus,
    TradingDecisionResult Decision,
    RiskAssessmentResult Risk,
    TradingPlanResult Plan);

internal sealed class FixedWorkspaceTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class DelayedWorkspaceConsistencyPolicy(TimeSpan delay) : ITradingWorkspaceConsistencyPolicy
{
    public async Task<WorkspaceConsistency> EvaluateAsync(TradingWorkspaceRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
        return new WorkspaceConsistency([]);
    }
}
