using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Consensus.Domain;
using TradeMind.AI.ExpertAgents.Dispatch.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.AI.RiskEngine.Domain;
using TradeMind.AI.TradingDecisions.Domain;
using TradeMind.AI.TradingPlans.Domain;
using TradeMind.AI.TradingWorkspace.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.TradingWorkspace.Application;

public interface ITradingWorkspaceConsistencyPolicy
{
    Task<WorkspaceConsistency> EvaluateAsync(TradingWorkspaceRequest request, CancellationToken cancellationToken);
}

public interface ITradingWorkspaceFreshnessPolicy
{
    Task<WorkspaceFreshness> EvaluateAsync(TradingWorkspaceRequest request, DateTimeOffset evaluatedAtUtc, CancellationToken cancellationToken);
}

public sealed class DefaultTradingWorkspaceConsistencyPolicy : ITradingWorkspaceConsistencyPolicy
{
    public Task<WorkspaceConsistency> EvaluateAsync(TradingWorkspaceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var issues = new List<WorkspaceIssue>();
        var contextId = request.Context?.Id;

        if (request.Version != TradingWorkspaceRequest.CurrentVersion)
        {
            issues.Add(Issue("UNSUPPORTED_WORKSPACE_VERSION", WorkspaceIssueCategory.Validation, WorkspaceIssueSeverity.Error, true, "workspace", "Workspace request version is not supported."));
        }

        AddVersionIssue(issues, "context", request.Context?.Version, MarketContext.CurrentVersion);
        AddVersionIssue(issues, "dispatch", request.Dispatch?.Version, AgentDispatchPlan.CurrentVersion);
        AddVersionIssue(issues, "dispatch-execution", request.DispatchExecution?.Plan.Version, AgentDispatchPlan.CurrentVersion);
        AddVersionIssue(issues, "consensus", request.Consensus?.SchemaVersion, ConsensusResult.CurrentSchemaVersion);
        AddVersionIssue(issues, "decision", request.Decision?.SchemaVersion, TradingDecisionResult.CurrentSchemaVersion);
        AddVersionIssue(issues, "risk", request.Risk?.SchemaVersion, RiskAssessmentResult.CurrentSchemaVersion);
        AddVersionIssue(issues, "plan", request.Plan?.SchemaVersion, TradingPlanResult.CurrentSchemaVersion);
        foreach (var analysis in request.Analyses)
        {
            AddVersionIssue(issues, $"agent:{analysis.AgentRunId}", analysis.SchemaVersion, AgentAnalysisResult.CurrentSchemaVersion);
        }

        AddContextIssue(issues, contextId, "dispatch", request.Dispatch?.MarketContextId, "Dispatch does not use the workspace context.");
        AddContextIssue(issues, contextId, "dispatch-execution", request.DispatchExecution?.Plan.MarketContextId, "Dispatch execution does not use the workspace context.");
        foreach (var analysis in request.Analyses)
        {
            AddContextIssue(issues, contextId, $"agent:{analysis.AgentRunId}", analysis.MarketContextId, "Agent analysis does not use the workspace context.");
        }

        AddContextIssue(issues, contextId, "consensus", request.Consensus?.MarketContextId, "Consensus does not use the workspace context.");
        AddContextIssue(issues, contextId, "decision", request.Decision?.MarketContextId, "Decision does not use the workspace context.");
        AddContextIssue(issues, contextId, "risk", request.Risk?.MarketContextId, "Risk assessment does not use the workspace context.");
        AddContextIssue(issues, contextId, "plan", request.Plan?.MarketContextId, "Trading plan does not use the workspace context.");

        AddPairIssue(issues, request.Dispatch, request.DispatchExecution?.Plan, "DISPATCH_MISMATCH", "dispatch", "Dispatch plan and execution plan do not refer to the same dispatch.",
            (left, right) => left.DispatchId != right.DispatchId || left.Version != right.Version);

        if (request.Consensus is not null && request.Dispatch is null && request.DispatchExecution is null && request.Analyses.Count == 0)
        {
            issues.Add(Issue("MISSING_ANALYSIS_DEPENDENCY", WorkspaceIssueCategory.Completeness, WorkspaceIssueSeverity.Error, true, "consensus", "Consensus is present without dispatch or agent analysis inputs."));
        }

        if (request.Decision is not null && request.Consensus is null)
        {
            issues.Add(Issue("MISSING_CONSENSUS_DEPENDENCY", WorkspaceIssueCategory.Completeness, WorkspaceIssueSeverity.Error, true, "decision", "Decision is present without its consensus dependency."));
        }

        if (request.Decision is not null && request.Consensus is not null && request.Decision.ConsensusId != request.Consensus.ConsensusId)
        {
            issues.Add(Issue("CONSENSUS_ID_MISMATCH", WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Critical, true, "decision", "Decision and consensus identifiers differ."));
        }

        if (request.Risk is not null && request.Decision is null)
        {
            issues.Add(Issue("MISSING_DECISION_DEPENDENCY", WorkspaceIssueCategory.Completeness, WorkspaceIssueSeverity.Error, true, "risk", "Risk assessment is present without its decision dependency."));
        }

        if (request.Risk is not null && request.Decision is not null && request.Risk.DecisionId != request.Decision.DecisionId)
        {
            issues.Add(Issue("DECISION_ID_MISMATCH", WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Critical, true, "risk", "Risk assessment and decision identifiers differ."));
        }

        if (request.Plan is not null && request.Decision is null)
        {
            issues.Add(Issue("MISSING_PLAN_DECISION_DEPENDENCY", WorkspaceIssueCategory.Completeness, WorkspaceIssueSeverity.Error, true, "plan", "Trading plan is present without its decision dependency."));
        }

        if (request.Plan is not null && request.Risk is null)
        {
            issues.Add(Issue("MISSING_PLAN_RISK_DEPENDENCY", WorkspaceIssueCategory.Completeness, WorkspaceIssueSeverity.Error, true, "plan", "Trading plan is present without its risk dependency."));
        }

        if (request.Plan is not null && request.Decision is not null && request.Plan.DecisionId != request.Decision.DecisionId)
        {
            issues.Add(Issue("PLAN_DECISION_ID_MISMATCH", WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Critical, true, "plan", "Trading plan and decision identifiers differ."));
        }

        if (request.Plan is not null && request.Risk is not null && request.Plan.RiskAssessmentId != request.Risk.AssessmentId)
        {
            issues.Add(Issue("PLAN_RISK_ID_MISMATCH", WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Critical, true, "plan", "Trading plan and risk assessment identifiers differ."));
        }

        AddInstrumentAndTimeframeIssues(issues, request);
        AddDirectionIssues(issues, request);
        AddQuantityAndLevelIssues(issues, request);
        AddTemporalIssues(issues, request);
        AddRiskAndConflictIssues(issues, request);
        return Task.FromResult(new WorkspaceConsistency(issues.OrderBy(issue => issue.Code, StringComparer.Ordinal).ThenBy(issue => issue.Source, StringComparer.Ordinal).ToArray()));
    }

    private static void AddContextIssue(List<WorkspaceIssue> issues, MarketContextId? expected, string source, MarketContextId? actual, string message)
    {
        if (expected is not null && actual is not null && expected != actual)
        {
            issues.Add(Issue("MARKET_CONTEXT_ID_MISMATCH", WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Critical, true, source, message));
        }
    }

    private static void AddVersionIssue(List<WorkspaceIssue> issues, string source, int? actual, int expected)
    {
        if (actual is not null && actual != expected)
        {
            issues.Add(Issue("UNSUPPORTED_SOURCE_VERSION", WorkspaceIssueCategory.Validation, WorkspaceIssueSeverity.Error, true, source, $"Source version {actual} is not compatible with supported version {expected}."));
        }
    }

    private static void AddInstrumentAndTimeframeIssues(List<WorkspaceIssue> issues, TradingWorkspaceRequest request)
    {
        var sources = new List<(string Name, Instrument? Instrument, Timeframe? Timeframe)>();
        if (request.Context is not null) sources.Add(("context", request.Context.Instrument, request.Context.Timeframe));
        if (request.Decision is not null) sources.Add(("decision", request.Decision.Instrument, request.Decision.Timeframe));
        if (request.Risk is not null) sources.Add(("risk", request.Risk.Instrument, request.Risk.Timeframe));
        if (request.Plan is not null) sources.Add(("plan", request.Plan.Instrument, request.Plan.Timeframe));
        var instruments = sources.Select(item => item.Instrument).Where(item => item is not null).Cast<Instrument>().Select(item => item.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var timeframes = sources.Select(item => item.Timeframe).Where(item => item is not null).Cast<Timeframe>().Select(item => item.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (instruments.Length > 1)
        {
            issues.Add(Issue("INSTRUMENT_DIVERGENCE", WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Critical, true, "workspace", "Available sources disagree on the instrument."));
        }

        if (timeframes.Length > 1)
        {
            issues.Add(Issue("TIMEFRAME_DIVERGENCE", WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Critical, true, "workspace", "Available sources disagree on the timeframe."));
        }
    }

    private static void AddDirectionIssues(List<WorkspaceIssue> issues, TradingWorkspaceRequest request)
    {
        var decisionDirection = DirectionOf(request.Decision);
        var riskDirection = request.Risk?.StopDistance?.Direction;
        var planDirection = request.Plan?.Direction;
        if (decisionDirection is null)
        {
            return;
        }

        var riskAsPlanDirection = riskDirection switch
        {
            MarketDirection.Long => TradingPlanDirection.Long,
            MarketDirection.Short => TradingPlanDirection.Short,
            _ => TradingPlanDirection.None
        };
        if ((request.Risk is not null && riskDirection is not null && decisionDirection != riskAsPlanDirection)
            || (request.Plan is not null && planDirection is not null && decisionDirection != planDirection)
            || (request.Risk is not null && request.Plan is not null && riskDirection is not null && riskAsPlanDirection != planDirection))
        {
            issues.Add(Issue("DIRECTION_DIVERGENCE", WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Critical, true, "workspace", "Decision, risk and plan directions differ."));
        }
    }

    private static void AddQuantityAndLevelIssues(List<WorkspaceIssue> issues, TradingWorkspaceRequest request)
    {
        if (request.Decision is null || request.Plan is null)
        {
            return;
        }

        if (request.Risk?.PositionSize is not null && request.Plan.Quantity is not null && request.Risk.PositionSize.FinalQuantity != request.Plan.Quantity.FinalQuantity)
        {
            issues.Add(Issue("QUANTITY_DIVERGENCE", WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Critical, true, "plan", "Plan quantity differs from the Risk Engine quantity."));
        }

        if (request.Decision.Entry?.Price != request.Plan.Entry?.Price || request.Decision.Stop?.Price != request.Plan.Stop?.Price)
        {
            issues.Add(Issue("LEVEL_DIVERGENCE", WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Critical, true, "plan", "Plan entry or stop differs from the decision."));
        }

        var decisionTargets = request.Decision.Targets.OrderBy(target => target.Ordinal).Select(target => target.Price.Value).ToArray();
        var planTargets = request.Plan.Targets.OrderBy(target => target.Ordinal).Select(target => target.Price.Value).ToArray();
        if (!decisionTargets.SequenceEqual(planTargets))
        {
            issues.Add(Issue("TARGET_LEVEL_DIVERGENCE", WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Critical, true, "plan", "Plan targets differ from the decision targets."));
        }
    }

    private static void AddTemporalIssues(List<WorkspaceIssue> issues, TradingWorkspaceRequest request)
    {
        var ordered = new List<(string Source, DateTimeOffset Timestamp)>();
        if (request.Context is not null) ordered.Add(("context", request.Context.BuiltAtUtc));
        if (request.Dispatch is not null) ordered.Add(("dispatch", request.Dispatch.CreatedAtUtc));
        if (request.DispatchExecution is not null) ordered.Add(("dispatch-execution", request.DispatchExecution.CompletedAtUtc));
        if (request.Consensus is not null) ordered.Add(("consensus", request.Consensus.CreatedAtUtc));
        if (request.Decision is not null) ordered.Add(("decision", request.Decision.CreatedAtUtc));
        if (request.Risk is not null) ordered.Add(("risk", request.Risk.CreatedAtUtc));
        if (request.Plan is not null) ordered.Add(("plan", request.Plan.CreatedAtUtc));
        for (var index = 1; index < ordered.Count; index++)
        {
            if (ordered[index].Timestamp < ordered[index - 1].Timestamp)
            {
                issues.Add(Issue("TEMPORAL_ORDER_INVALID", WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Error, true, ordered[index].Source, $"Source timestamp for {ordered[index].Source} precedes {ordered[index - 1].Source}."));
            }
        }
    }

    private static void AddRiskAndConflictIssues(List<WorkspaceIssue> issues, TradingWorkspaceRequest request)
    {
        if (request.Risk?.Verdict == RiskVerdict.Rejected)
        {
            issues.Add(Issue("RISK_REJECTED", WorkspaceIssueCategory.Pipeline, WorkspaceIssueSeverity.Critical, true, "risk", "Risk Engine rejected the current decision."));
        }

        if (request.Consensus?.Conflicts.Any(conflict => conflict.Severity == ConsensusConflictSeverity.Critical) == true || request.Decision?.Conflicts.Any(conflict => conflict.Severity == ConsensusConflictSeverity.Critical) == true)
        {
            issues.Add(Issue("CRITICAL_CONFLICT", WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Critical, true, "consensus", "A critical conflict is present in the available analysis."));
        }
    }

    private static TradingPlanDirection? DirectionOf(TradingDecisionResult? decision) => decision?.Type switch
    {
        TradingDecisionType.LongSetup => TradingPlanDirection.Long,
        TradingDecisionType.ShortSetup => TradingPlanDirection.Short,
        _ => decision is null ? null : TradingPlanDirection.None
    };

    private static void AddPairIssue<T>(List<WorkspaceIssue> issues, T? left, T? right, string code, string source, string message, Func<T, T, bool> differs)
        where T : class
    {
        if (left is not null && right is not null && differs(left, right))
        {
            issues.Add(Issue(code, WorkspaceIssueCategory.Consistency, WorkspaceIssueSeverity.Critical, true, source, message));
        }
    }

    private static WorkspaceIssue Issue(string code, WorkspaceIssueCategory category, WorkspaceIssueSeverity severity, bool blocking, string source, string message) =>
        new(code, category, severity, blocking, source, message);
}

public sealed class DefaultTradingWorkspaceFreshnessPolicy(TradingWorkspaceOptions options) : ITradingWorkspaceFreshnessPolicy
{
    private readonly TradingWorkspaceOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public Task<WorkspaceFreshness> EvaluateAsync(TradingWorkspaceRequest request, DateTimeOffset evaluatedAtUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var contextAge = Age(request.Context?.BuiltAtUtc, evaluatedAtUtc);
        var consensusAge = Age(request.Consensus?.CompletedAtUtc, evaluatedAtUtc);
        var decisionAge = Age(request.Decision?.CompletedAtUtc, evaluatedAtUtc);
        var riskAge = Age(request.Risk?.CompletedAtUtc, evaluatedAtUtc);
        var accountInstrumentAge = riskAge;
        var planAge = Age(request.Plan?.CompletedAtUtc, evaluatedAtUtc);
        var planExpiry = request.Plan?.ExpiresAtUtc;
        var planExpired = planExpiry is not null && planExpiry <= evaluatedAtUtc;
        var timestamps = new[]
        {
            request.Context?.BuiltAtUtc,
            request.Consensus?.CompletedAtUtc,
            request.Decision?.CompletedAtUtc,
            request.Risk?.CompletedAtUtc,
            request.Plan?.CompletedAtUtc
        }.Where(value => value is not null).Select(value => value!.Value).ToArray();
        var oldest = timestamps.Length == 0 ? (DateTimeOffset?)null : timestamps.Min();
        return Task.FromResult(new WorkspaceFreshness(
            evaluatedAtUtc,
            contextAge,
            consensusAge,
            decisionAge,
            accountInstrumentAge,
            riskAge,
            planAge,
            planExpiry,
            oldest,
            IsStale(contextAge, _options.ContextMaximumAge),
            IsStale(consensusAge, _options.ConsensusMaximumAge),
            IsStale(decisionAge, _options.DecisionMaximumAge),
            IsStale(accountInstrumentAge, _options.AccountInstrumentMaximumAge),
            IsStale(riskAge, _options.RiskMaximumAge),
            planExpired));
    }

    private static TimeSpan? Age(DateTimeOffset? timestamp, DateTimeOffset now) => timestamp is null ? null : now - timestamp.Value < TimeSpan.Zero ? TimeSpan.Zero : now - timestamp.Value;

    private static bool IsStale(TimeSpan? age, TimeSpan maximumAge) => age is not null && age > maximumAge;
}
