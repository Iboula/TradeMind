using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

public interface ITradingWorkspaceBuilder
{
    Task<TradingWorkspaceResult> BuildAsync(
        TradingWorkspaceRequest request,
        CancellationToken cancellationToken);
}

public sealed class TradingWorkspaceBuilder(
    ITradingWorkspaceConsistencyPolicy consistencyPolicy,
    ITradingWorkspaceFreshnessPolicy freshnessPolicy,
    IOptions<TradingWorkspaceOptions> options,
    TimeProvider timeProvider,
    ILogger<TradingWorkspaceBuilder> logger) : ITradingWorkspaceBuilder
{
    private readonly TradingWorkspaceOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task<TradingWorkspaceResult> BuildAsync(
        TradingWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var createdAtUtc = timeProvider.GetUtcNow();
        using var timeoutSource = new CancellationTokenSource(request.Timeout ?? _options.Timeout, timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            var consistency = await consistencyPolicy.EvaluateAsync(request, linkedSource.Token).ConfigureAwait(false);
            var freshness = await freshnessPolicy.EvaluateAsync(request, createdAtUtc, linkedSource.Token).ConfigureAwait(false);
            linkedSource.Token.ThrowIfCancellationRequested();
            return Assemble(request, createdAtUtc, timeProvider.GetUtcNow(), consistency, freshness, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Trading workspace {WorkspaceId} was cancelled by the caller.", request.WorkspaceId);
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            logger.LogWarning("Trading workspace {WorkspaceId} exceeded its timeout.", request.WorkspaceId);
            return Failure(request, createdAtUtc, TradingWorkspaceStatus.TimedOut, "WORKSPACE_TIMEOUT", "The workspace operation exceeded its configured timeout.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Trading workspace {WorkspaceId} failed unexpectedly.", request.WorkspaceId);
            return Failure(request, createdAtUtc, TradingWorkspaceStatus.Failed, "WORKSPACE_FAILURE", "The workspace operation failed unexpectedly.");
        }
    }

    private TradingWorkspaceResult Failure(
        TradingWorkspaceRequest request,
        DateTimeOffset createdAtUtc,
        TradingWorkspaceStatus status,
        string code,
        string message)
    {
        var issue = new WorkspaceIssue(code, WorkspaceIssueCategory.Pipeline, WorkspaceIssueSeverity.Critical, true, "workspace", message);
        var freshness = new WorkspaceFreshness(createdAtUtc, null, null, null, null, null, null, null, null, false, false, false, false, false, false);
        return Assemble(request, createdAtUtc, timeProvider.GetUtcNow(), new WorkspaceConsistency([issue]), freshness, status, issue);
    }

    private TradingWorkspaceResult Assemble(
        TradingWorkspaceRequest request,
        DateTimeOffset createdAtUtc,
        DateTimeOffset completedAtUtc,
        WorkspaceConsistency consistency,
        WorkspaceFreshness freshness,
        TradingWorkspaceStatus? forcedStatus,
        WorkspaceIssue? forcedIssue)
    {
        var issues = new List<WorkspaceIssue>(consistency.Issues);
        issues.AddRange(FreshnessIssues(freshness));
        issues.AddRange(SourceIssues(request));
        if (forcedIssue is not null && issues.All(issue => issue.Code != forcedIssue.Code))
        {
            issues.Add(forcedIssue);
        }

        var progress = BuildProgress(request, freshness, issues);
        var completeness = BuildCompleteness(progress, issues);
        var state = forcedStatus is not null ? TradingWorkspaceState.Blocked : DetermineState(request, freshness, issues, progress);
        var status = forcedStatus ?? DetermineStatus(state, issues, request);
        var timeline = Bound(BuildTimeline(request, issues, completedAtUtc), _options.MaximumTimelineEntries, TimelineComparer);
        var alerts = Bound(issues.Select(issue => new WorkspaceAlert(issue)), _options.MaximumAlerts, AlertComparer);
        var blockers = Bound(
            issues.Where(issue => issue.Blocking).Select(issue => new WorkspaceBlocker(issue.Code, issue.Explanation, issue.Source, NextActionFor(issue))),
            _options.MaximumBlockers,
            BlockerComparer);
        var nextActions = Bound(BuildNextActions(request, state, issues, freshness), _options.MaximumNextActions, NextActionComparer);
        var criticalRisks = BuildCriticalRisks(request);
        var traces = Bound(BuildTraces(request), _options.MaximumTraces, TraceComparer);
        var warnings = Bound(BuildWarnings(request, issues), _options.MaximumWarnings, WarningComparer);
        var errors = Bound(issues.Where(issue => issue.Severity is WorkspaceIssueSeverity.Error or WorkspaceIssueSeverity.Critical || issue.Blocking), _options.MaximumErrors, IssueComparer);
        var limitations = BuildLimitations(request, timeline.Count, traces.Count, criticalRisks.Count);
        var contextId = request.Context?.Id ?? request.Consensus?.MarketContextId ?? request.Decision?.MarketContextId ?? request.Risk?.MarketContextId ?? request.Plan?.MarketContextId;
        var instrument = request.Context?.Instrument ?? request.Decision?.Instrument ?? request.Risk?.Instrument ?? request.Plan?.Instrument;
        var timeframe = request.Context?.Timeframe ?? request.Decision?.Timeframe ?? request.Risk?.Timeframe ?? request.Plan?.Timeframe;

        logger.LogInformation(
            "Built trading workspace {WorkspaceId} with state {State}, status {Status}, and {IssueCount} issues.",
            request.WorkspaceId,
            state,
            status,
            issues.Count);

        return new TradingWorkspaceResult(
            request.WorkspaceId,
            status,
            state,
            request.BuildMode,
            contextId,
            instrument,
            timeframe,
            new WorkspaceContextSummary(request.Context),
            new WorkspaceAnalysisSummary(request.Dispatch, request.DispatchExecution, request.Analyses),
            new WorkspaceConsensusSummary(request.Consensus),
            new WorkspaceDecisionSummary(request.Decision),
            new WorkspaceRiskSummary(request.Risk),
            new WorkspacePlanSummary(request.Plan, completedAtUtc),
            progress,
            completeness,
            freshness,
            new WorkspaceConsistency(issues),
            timeline,
            alerts,
            blockers,
            nextActions,
            criticalRisks,
            traces,
            warnings,
            limitations,
            errors,
            Summary(state, status, progress, issues),
            createdAtUtc,
            completedAtUtc);
    }

    private WorkspacePipelineProgress BuildProgress(
        TradingWorkspaceRequest request,
        WorkspaceFreshness freshness,
        IReadOnlyCollection<WorkspaceIssue> issues)
    {
        var dispatch = request.DispatchExecution?.Plan ?? request.Dispatch;
        var analysisAvailable = request.Analyses.Count > 0;
        var stages = new[]
        {
            Stage(WorkspacePipelineStageKind.Context, request.Context is not null, request.Context?.Status is MarketContextBuildStatus.Succeeded or MarketContextBuildStatus.PartiallySucceeded, !freshness.ContextStale, request.Context?.Id.ToString(), request.Context?.BuiltAtUtc, freshness.ContextStale, issues, "context"),
            Stage(WorkspacePipelineStageKind.Dispatch, dispatch is not null, dispatch is not null, true, dispatch?.DispatchId.ToString(), dispatch?.CreatedAtUtc, false, issues, "dispatch"),
            Stage(WorkspacePipelineStageKind.ExpertAnalysis, analysisAvailable, request.Analyses.Any(result => result.Status is AgentAnalysisStatus.Succeeded or AgentAnalysisStatus.PartiallySucceeded), true, request.Analyses.Count == 0 ? null : string.Join(",", request.Analyses.Select(result => result.AgentRunId.Value).OrderBy(value => value, StringComparer.Ordinal)), request.Analyses.Count == 0 ? null : request.Analyses.Max(result => result.CompletedAtUtc), false, issues, "agent"),
            Stage(WorkspacePipelineStageKind.Consensus, request.Consensus is not null, request.Consensus?.Status is ConsensusStatus.Succeeded or ConsensusStatus.PartiallySucceeded, !freshness.ConsensusStale, request.Consensus?.ConsensusId.ToString(), request.Consensus?.CompletedAtUtc, freshness.ConsensusStale, issues, "consensus"),
            Stage(WorkspacePipelineStageKind.Decision, request.Decision is not null, request.Decision?.Status is TradingDecisionStatus.Succeeded or TradingDecisionStatus.PartiallySucceeded, !freshness.DecisionStale, request.Decision?.DecisionId.ToString(), request.Decision?.CompletedAtUtc, freshness.DecisionStale, issues, "decision"),
            Stage(WorkspacePipelineStageKind.Risk, request.Risk is not null, request.Risk?.Status is RiskAssessmentStatus.Succeeded or RiskAssessmentStatus.PartiallySucceeded && request.Risk.Verdict is not RiskVerdict.Rejected, !freshness.RiskStale && !freshness.AccountInstrumentStale, request.Risk?.AssessmentId.ToString(), request.Risk?.CompletedAtUtc, freshness.RiskStale || freshness.AccountInstrumentStale, issues, "risk"),
            Stage(WorkspacePipelineStageKind.Plan, request.Plan is not null, request.Plan?.Status is TradingPlanStatus.Succeeded or TradingPlanStatus.PartiallySucceeded && !freshness.PlanExpired, !freshness.PlanExpired, request.Plan?.PlanId.ToString(), request.Plan?.CompletedAtUtc, freshness.PlanExpired, issues, "plan")
        };
        return new WorkspacePipelineProgress(stages);
    }

    private static WorkspacePipelineStage Stage(
        WorkspacePipelineStageKind stage,
        bool available,
        bool valid,
        bool fresh,
        string? sourceId,
        DateTimeOffset? timestamp,
        bool freshnessBlocking,
        IReadOnlyCollection<WorkspaceIssue> issues,
        string sourcePrefix)
    {
        var related = issues.Where(issue => issue.Source.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase) || issue.Source.Equals("workspace", StringComparison.OrdinalIgnoreCase)).ToArray();
        var blocking = freshnessBlocking || related.Any(issue => issue.Blocking);
        var status = !available ? WorkspaceStageStatus.NotAvailable
            : related.Any(issue => issue.Code == "CRITICAL_CONFLICT") ? WorkspaceStageStatus.Conflicted
            : freshnessBlocking ? (stage == WorkspacePipelineStageKind.Plan ? WorkspaceStageStatus.Expired : WorkspaceStageStatus.Stale)
            : blocking ? WorkspaceStageStatus.Blocked
            : valid ? WorkspaceStageStatus.Succeeded
            : WorkspaceStageStatus.Failed;
        return new WorkspacePipelineStage(
            stage,
            status,
            available,
            valid && !blocking,
            fresh,
            sourceId,
            timestamp,
            blocking,
            related.Where(issue => issue.Severity is WorkspaceIssueSeverity.Info or WorkspaceIssueSeverity.Warning).Select(issue => issue.Code).ToArray(),
            related.Where(issue => issue.Severity is WorkspaceIssueSeverity.Error or WorkspaceIssueSeverity.Critical).Select(issue => issue.Code).ToArray());
    }

    private static WorkspaceCompleteness BuildCompleteness(WorkspacePipelineProgress progress, IReadOnlyCollection<WorkspaceIssue> issues)
    {
        var missing = progress.Stages.Where(stage => !stage.Available).ToArray();
        return new WorkspaceCompleteness(progress.AvailableStageCount, progress.Stages.Count, issues.Count(issue => issue.Category == WorkspaceIssueCategory.Completeness && issue.Blocking), missing);
    }

    private static TradingWorkspaceState DetermineState(
        TradingWorkspaceRequest request,
        WorkspaceFreshness freshness,
        IReadOnlyCollection<WorkspaceIssue> issues,
        WorkspacePipelineProgress progress)
    {
        if (!progress.Stages.Any(stage => stage.Available)) return TradingWorkspaceState.Empty;
        if (freshness.PlanExpired && request.Plan is not null) return TradingWorkspaceState.Expired;
        if (issues.Any(issue => issue.Code == "CRITICAL_CONFLICT")) return TradingWorkspaceState.Conflicted;
        if (issues.Any(issue => issue.Code == "RISK_REJECTED")) return TradingWorkspaceState.Blocked;
        if (freshness.IsStale) return TradingWorkspaceState.Incomplete;
        if (issues.Any(issue => issue.Blocking)) return TradingWorkspaceState.Blocked;
        if (request.Plan is not null && progress.Stages.Single(stage => stage.Stage == WorkspacePipelineStageKind.Plan).Valid && !freshness.IsStale) return TradingWorkspaceState.PlanReady;
        if (request.Risk is not null) return TradingWorkspaceState.RiskReady;
        if (request.Decision is not null) return TradingWorkspaceState.DecisionReady;
        if (request.Consensus is not null) return TradingWorkspaceState.ConsensusReady;
        if (request.Analyses.Count > 0) return TradingWorkspaceState.AnalysisReady;
        if (request.Context is not null) return TradingWorkspaceState.ContextReady;
        return TradingWorkspaceState.Incomplete;
    }

    private static TradingWorkspaceStatus DetermineStatus(TradingWorkspaceState state, IReadOnlyCollection<WorkspaceIssue> issues, TradingWorkspaceRequest request)
    {
        if (state is TradingWorkspaceState.Empty || state is TradingWorkspaceState.ContextReady or TradingWorkspaceState.AnalysisReady or TradingWorkspaceState.ConsensusReady or TradingWorkspaceState.DecisionReady or TradingWorkspaceState.RiskReady or TradingWorkspaceState.PlanReady)
        {
            return issues.Any(issue => issue.Severity is WorkspaceIssueSeverity.Error or WorkspaceIssueSeverity.Critical) ? TradingWorkspaceStatus.PartiallySucceeded : TradingWorkspaceStatus.Succeeded;
        }

        return request.BuildMode == TradingWorkspaceBuildMode.Progressive ? TradingWorkspaceStatus.PartiallySucceeded : TradingWorkspaceStatus.Failed;
    }

    private static IReadOnlyList<WorkspaceIssue> SourceIssues(TradingWorkspaceRequest request)
    {
        var issues = new List<WorkspaceIssue>();
        if (request.Consensus is not null)
        {
            issues.AddRange(request.Consensus.Errors.Select(error => new WorkspaceIssue(error.Code, WorkspaceIssueCategory.Source, WorkspaceIssueSeverity.Error, true, "consensus", error.Message)));
        }

        if (request.Decision is not null)
        {
            issues.AddRange(request.Decision.Errors.Select(error => new WorkspaceIssue(error.Code, WorkspaceIssueCategory.Source, error.Fatal ? WorkspaceIssueSeverity.Critical : WorkspaceIssueSeverity.Error, error.Fatal, "decision", error.Message)));
        }

        if (request.Risk is not null)
        {
            issues.AddRange(request.Risk.Errors.Select(error => new WorkspaceIssue(error.Code, WorkspaceIssueCategory.Source, error.Fatal ? WorkspaceIssueSeverity.Critical : WorkspaceIssueSeverity.Error, error.Fatal, "risk", error.Message)));
        }

        if (request.Plan is not null)
        {
            issues.AddRange(request.Plan.Errors.Select(error => new WorkspaceIssue(error.Code, WorkspaceIssueCategory.Source, error.Blocking ? WorkspaceIssueSeverity.Error : WorkspaceIssueSeverity.Warning, error.Blocking, "plan", error.Message)));
        }

        foreach (var analysis in request.Analyses.Where(result => result.Status is AgentAnalysisStatus.Failed or AgentAnalysisStatus.TimedOut or AgentAnalysisStatus.Cancelled))
        {
            issues.Add(new WorkspaceIssue("AGENT_ANALYSIS_UNAVAILABLE", WorkspaceIssueCategory.Source, WorkspaceIssueSeverity.Warning, false, $"agent:{analysis.AgentRunId}", $"Agent analysis ended with status {analysis.Status}."));
        }

        var dispatch = request.DispatchExecution?.Plan ?? request.Dispatch;
        if (dispatch is not null)
        {
            issues.AddRange(dispatch.Rejections.Where(rejection => rejection.Requirement == AgentDispatchRequirement.Required).Select(rejection => new WorkspaceIssue("DISPATCH_REQUIRED_REJECTION", WorkspaceIssueCategory.Source, WorkspaceIssueSeverity.Error, true, "dispatch", rejection.Message)));
        }

        return issues;
    }

    private static IReadOnlyList<WorkspaceWarning> BuildWarnings(TradingWorkspaceRequest request, IReadOnlyCollection<WorkspaceIssue> issues)
    {
        var warnings = new List<WorkspaceWarning>();
        warnings.AddRange(issues.Where(issue => !issue.Blocking || issue.Severity == WorkspaceIssueSeverity.Warning).Select(issue => new WorkspaceWarning(issue.Code, issue.Category, issue.Source, issue.Explanation, issue.Traces)));
        if (request.Context is not null)
        {
            warnings.AddRange(request.Context.Warnings.Select(warning => new WorkspaceWarning(warning.Code, WorkspaceIssueCategory.Source, "context", warning.Message)));
        }

        if (request.Dispatch is not null)
        {
            warnings.AddRange(request.Dispatch.Warnings.Select(message => new WorkspaceWarning("DISPATCH_WARNING", WorkspaceIssueCategory.Source, "dispatch", message)));
        }

        if (request.DispatchExecution is not null)
        {
            warnings.AddRange(request.DispatchExecution.Warnings.Select(message => new WorkspaceWarning("DISPATCH_EXECUTION_WARNING", WorkspaceIssueCategory.Source, "dispatch-execution", message)));
        }

        if (request.Consensus is not null)
        {
            warnings.AddRange(request.Consensus.Warnings.Select(message => new WorkspaceWarning("CONSENSUS_WARNING", WorkspaceIssueCategory.Source, "consensus", message)));
        }

        if (request.Decision is not null)
        {
            warnings.AddRange(request.Decision.Warnings.Select(warning => new WorkspaceWarning(warning.Code, WorkspaceIssueCategory.Source, "decision", warning.Message)));
        }

        if (request.Risk is not null)
        {
            warnings.AddRange(request.Risk.Warnings.Select(warning => new WorkspaceWarning(warning.Code, WorkspaceIssueCategory.Source, "risk", warning.Message)));
        }

        if (request.Plan is not null)
        {
            warnings.AddRange(request.Plan.Warnings.Select(warning => new WorkspaceWarning(warning.Code, WorkspaceIssueCategory.Source, "plan", warning.Message)));
        }

        return warnings;
    }

    private IReadOnlyList<WorkspaceLimitation> BuildLimitations(TradingWorkspaceRequest request, int timelineCount, int traceCount, int criticalRiskCount)
    {
        var limitations = new List<WorkspaceLimitation>
        {
            new("NO_ORCHESTRATION", "The workspace only projects supplied results and never invokes downstream engines."),
            new("NO_RECALCULATION", "Consensus, decision, risk and sizing values are never recalculated."),
            new("NO_EXECUTION", "The workspace cannot execute, modify or submit an order."),
            new("PROGRESSIVE_SNAPSHOT", request.BuildMode == TradingWorkspaceBuildMode.Progressive ? "The workspace may represent an incomplete pipeline." : "The workspace was requested as a snapshot.")
        };
        if (timelineCount >= _options.MaximumTimelineEntries) limitations.Add(new("TIMELINE_TRUNCATED", "Timeline entries were bounded by configuration."));
        if (traceCount >= _options.MaximumTraces) limitations.Add(new("TRACES_TRUNCATED", "Trace references were bounded by configuration."));
        if (criticalRiskCount > _options.MaximumNonCriticalRisks) limitations.Add(new("CRITICAL_RISKS_PRESERVED", "Critical risks are preserved even when non-critical projections are bounded."));
        return Bound(limitations, _options.MaximumLimitations, LimitationComparer);
    }

    private static IReadOnlyList<WorkspaceRiskProjection> BuildCriticalRisks(TradingWorkspaceRequest request)
    {
        var risks = new List<WorkspaceRiskProjection>();
        if (request.Consensus is not null)
        {
            risks.AddRange(request.Consensus.Risks.Where(risk => risk.Severity == ConsensusRiskSeverity.Critical).Select(risk => new WorkspaceRiskProjection("consensus", risk.Description, risk.Severity)));
        }

        if (request.Decision is not null)
        {
            risks.AddRange(request.Decision.Risks.Where(risk => risk.IsCritical).Select(risk => new WorkspaceRiskProjection("decision", risk.Source.Description, risk.Source.Severity)));
        }

        if (request.Risk is not null)
        {
            risks.AddRange(request.Risk.Risks.Where(risk => risk.IsCritical).Select(risk => new WorkspaceRiskProjection("risk", risk.Source.Description, risk.Source.Severity)));
        }

        if (request.Plan is not null)
        {
            risks.AddRange(request.Plan.Risks.Where(risk => risk.IsCritical).Select(risk => new WorkspaceRiskProjection("plan", risk.Description, risk.Severity)));
        }

        return risks.OrderBy(risk => risk.Origin, StringComparer.Ordinal).ThenBy(risk => risk.Description, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<WorkspaceTraceReference> BuildTraces(TradingWorkspaceRequest request)
    {
        var traces = new List<WorkspaceTraceReference>();
        if (request.Context is not null)
        {
            foreach (var trace in request.Context.Traces)
            {
                foreach (var reference in trace.References)
                {
                    traces.Add(new WorkspaceTraceReference("context", trace.ProviderId.Value, reference.Kind, reference.Value, trace.SourceTimestampUtc));
                }
            }
        }

        foreach (var analysis in request.Analyses)
        {
            foreach (var evidence in analysis.Evidence)
            {
                traces.Add(new WorkspaceTraceReference("agent", analysis.AgentRunId.Value, evidence.SourceReference.Kind, evidence.SourceReference.Value, evidence.SourceTimestampUtc));
            }

            foreach (var observation in analysis.Observations)
            {
                foreach (var reference in observation.References)
                {
                    traces.Add(new WorkspaceTraceReference("agent", analysis.AgentRunId.Value, reference.Kind, reference.Value));
                }
            }
        }

        if (request.Consensus is not null)
        {
            foreach (var source in request.Consensus.Sources)
            {
                foreach (var reference in source.References)
                {
                    traces.Add(new WorkspaceTraceReference("consensus", source.AgentRunId.Value, reference.Kind, reference.Value));
                }
            }
        }

        AddDecisionTraces(traces, request.Decision);
        AddRiskTraces(traces, request.Risk);
        if (request.Plan is not null)
        {
            foreach (var trace in request.Plan.Traces)
            {
                traces.Add(new WorkspaceTraceReference("plan:" + trace.Origin, trace.Source.AgentRunId.Value, trace.Source.Reference.Kind, trace.Source.Reference.Value));
            }
        }

        return traces
            .GroupBy(trace => (trace.Origin, trace.SourceId, trace.Kind, trace.Value, trace.TimestampUtc))
            .Select(group => group.First())
            .OrderBy(trace => trace.Origin, StringComparer.Ordinal)
            .ThenBy(trace => trace.SourceId, StringComparer.Ordinal)
            .ThenBy(trace => trace.Kind, StringComparer.Ordinal)
            .ThenBy(trace => trace.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddDecisionTraces(List<WorkspaceTraceReference> traces, TradingDecisionResult? decision)
    {
        if (decision is null) return;
        foreach (var trace in decision.Traces)
        {
            traces.Add(new WorkspaceTraceReference("decision", trace.AgentRunId.Value, trace.Reference.Kind, trace.Reference.Value));
        }
    }

    private static void AddRiskTraces(List<WorkspaceTraceReference> traces, RiskAssessmentResult? risk)
    {
        if (risk is null) return;
        foreach (var trace in risk.Traces)
        {
            traces.Add(new WorkspaceTraceReference("risk", trace.AgentRunId.Value, trace.Reference.Kind, trace.Reference.Value));
        }
    }

    private static IReadOnlyList<WorkspaceTimelineEntry> BuildTimeline(TradingWorkspaceRequest request, IReadOnlyCollection<WorkspaceIssue> issues, DateTimeOffset completedAtUtc)
    {
        var timeline = new List<WorkspaceTimelineEntry>();
        if (request.Context is not null) timeline.Add(new(WorkspaceTimelineEventType.Context, request.Context.BuiltAtUtc, "context", request.Context.Id.ToString(), "Market context built."));
        var dispatch = request.DispatchExecution?.Plan ?? request.Dispatch;
        if (dispatch is not null) timeline.Add(new(WorkspaceTimelineEventType.Dispatch, dispatch.CreatedAtUtc, "dispatch", dispatch.DispatchId.ToString(), "Dispatch plan created."));
        foreach (var analysis in request.Analyses)
        {
            timeline.Add(new(WorkspaceTimelineEventType.AgentAnalysis, analysis.CompletedAtUtc, "agent", analysis.AgentRunId.Value, $"Agent analysis completed with status {analysis.Status}."));
        }

        if (request.Consensus is not null) timeline.Add(new(WorkspaceTimelineEventType.Consensus, request.Consensus.CompletedAtUtc, "consensus", request.Consensus.ConsensusId.ToString(), "Consensus result completed."));
        if (request.Decision is not null) timeline.Add(new(WorkspaceTimelineEventType.Decision, request.Decision.CompletedAtUtc, "decision", request.Decision.DecisionId.ToString(), "Trading decision completed."));
        if (request.Risk is not null) timeline.Add(new(WorkspaceTimelineEventType.Risk, request.Risk.CompletedAtUtc, "risk", request.Risk.AssessmentId.ToString(), "Risk assessment completed."));
        if (request.Plan is not null)
        {
            timeline.Add(new(WorkspaceTimelineEventType.Plan, request.Plan.CompletedAtUtc, "plan", request.Plan.PlanId.ToString(), "Trading plan completed."));
            timeline.Add(new(WorkspaceTimelineEventType.Expiration, request.Plan.ExpiresAtUtc, "plan", request.Plan.PlanId.ToString(), "Trading plan expiration."));
        }

        foreach (var issue in issues.Where(issue => issue.Blocking))
        {
            timeline.Add(new(WorkspaceTimelineEventType.Blocker, completedAtUtc, issue.Source, issue.Source, issue.Explanation, true));
        }

        timeline.Add(new(WorkspaceTimelineEventType.WorkspaceBuilt, completedAtUtc, "workspace", "workspace", "Trading workspace projection built."));
        return timeline
            .OrderBy(item => item.TimestampUtc)
            .ThenBy(item => item.Type)
            .ThenBy(item => item.SourceId, StringComparer.Ordinal)
            .ThenBy(item => item.Description, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<WorkspaceNextAction> BuildNextActions(TradingWorkspaceRequest request, TradingWorkspaceState state, IReadOnlyCollection<WorkspaceIssue> issues, WorkspaceFreshness freshness)
    {
        var actions = new List<WorkspaceNextAction>();
        if (state == TradingWorkspaceState.Empty)
        {
            actions.Add(new(WorkspaceNextActionType.RefreshContext, "No market context is available.", "workspace", true));
        }
        else if (freshness.ContextStale)
        {
            actions.Add(new(WorkspaceNextActionType.RefreshContext, "Market context is stale.", "context", true));
        }
        else if (request.Analyses.Count == 0)
        {
            actions.Add(new(WorkspaceNextActionType.RunExpertAnalysis, "No expert analysis is available.", "analysis", true));
        }
        else if (request.Consensus is null)
        {
            actions.Add(new(WorkspaceNextActionType.BuildConsensus, "Consensus is not available.", "consensus", true));
        }
        else if (request.Decision is null)
        {
            actions.Add(new(WorkspaceNextActionType.BuildDecision, "Trading decision is not available.", "decision", true));
        }
        else if (request.Risk is null)
        {
            actions.Add(new(WorkspaceNextActionType.AssessRisk, "Risk assessment is not available.", "risk", true));
        }
        else if (request.Plan is null)
        {
            actions.Add(new(WorkspaceNextActionType.GeneratePlan, "Trading plan is not available.", "plan", true));
        }

        if (issues.Any(issue => issue.Code == "CRITICAL_CONFLICT")) actions.Add(new(WorkspaceNextActionType.ReviewConflict, "A critical conflict requires review.", "consensus", true));
        if (issues.Any(issue => issue.Code == "RISK_REJECTED" || issue.Code == "PLAN_EXPIRED")) actions.Add(new(WorkspaceNextActionType.RequestUserConfirmation, "A blocking risk or plan condition requires explicit review.", "workspace", true));
        if (actions.Count == 0) actions.Add(new(WorkspaceNextActionType.NoAction, "The available workspace projection has no declared next action.", "workspace", false));
        return actions;
    }

    private static IReadOnlyList<WorkspaceIssue> FreshnessIssues(WorkspaceFreshness freshness)
    {
        var issues = new List<WorkspaceIssue>();
        if (freshness.ContextStale) issues.Add(new("CONTEXT_STALE", WorkspaceIssueCategory.Freshness, WorkspaceIssueSeverity.Critical, true, "context", "Market context exceeds its configured freshness threshold."));
        if (freshness.ConsensusStale) issues.Add(new("CONSENSUS_STALE", WorkspaceIssueCategory.Freshness, WorkspaceIssueSeverity.Error, true, "consensus", "Consensus exceeds its configured freshness threshold."));
        if (freshness.DecisionStale) issues.Add(new("DECISION_STALE", WorkspaceIssueCategory.Freshness, WorkspaceIssueSeverity.Error, true, "decision", "Decision exceeds its configured freshness threshold."));
        if (freshness.AccountInstrumentStale) issues.Add(new("ACCOUNT_INSTRUMENT_STALE", WorkspaceIssueCategory.Freshness, WorkspaceIssueSeverity.Error, true, "risk", "Account or instrument data exceeds its configured freshness threshold."));
        if (freshness.RiskStale) issues.Add(new("RISK_STALE", WorkspaceIssueCategory.Freshness, WorkspaceIssueSeverity.Error, true, "risk", "Risk assessment exceeds its configured freshness threshold."));
        if (freshness.PlanExpired) issues.Add(new("PLAN_EXPIRED", WorkspaceIssueCategory.Freshness, WorkspaceIssueSeverity.Critical, true, "plan", "Trading plan expiration has been reached."));
        return issues;
    }

    private static WorkspaceNextActionType NextActionFor(WorkspaceIssue issue) => issue.Code switch
    {
        "CONTEXT_STALE" => WorkspaceNextActionType.RefreshContext,
        "MISSING_ANALYSIS_DEPENDENCY" => WorkspaceNextActionType.RunExpertAnalysis,
        "MISSING_CONSENSUS_DEPENDENCY" => WorkspaceNextActionType.BuildConsensus,
        "MISSING_DECISION_DEPENDENCY" or "MISSING_PLAN_DECISION_DEPENDENCY" => WorkspaceNextActionType.BuildDecision,
        "MISSING_PLAN_RISK_DEPENDENCY" => WorkspaceNextActionType.AssessRisk,
        "PLAN_EXPIRED" => WorkspaceNextActionType.GeneratePlan,
        "CRITICAL_CONFLICT" => WorkspaceNextActionType.ReviewConflict,
        _ => WorkspaceNextActionType.RequestUserConfirmation
    };

    private static string Summary(TradingWorkspaceState state, TradingWorkspaceStatus status, WorkspacePipelineProgress progress, IReadOnlyCollection<WorkspaceIssue> issues) =>
        $"Workspace state={state}; status={status}; available-stages={progress.AvailableStageCount}; blocking-issues={issues.Count(issue => issue.Blocking)}.";

    private static IReadOnlyList<T> Bound<T>(IEnumerable<T> values, int maximum, IComparer<T> comparer)
    {
        return values.OrderBy(value => value, comparer).Take(maximum).ToArray();
    }

    private static readonly IComparer<WorkspaceAlert> AlertComparer = Comparer<WorkspaceAlert>.Create((left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));
    private static readonly IComparer<WorkspaceBlocker> BlockerComparer = Comparer<WorkspaceBlocker>.Create((left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));
    private static readonly IComparer<WorkspaceNextAction> NextActionComparer = Comparer<WorkspaceNextAction>.Create((left, right) => string.Compare(left.Type.ToString(), right.Type.ToString(), StringComparison.Ordinal));
    private static readonly IComparer<WorkspaceTraceReference> TraceComparer = Comparer<WorkspaceTraceReference>.Create((left, right) => string.Compare($"{left.Origin}|{left.SourceId}|{left.Kind}|{left.Value}", $"{right.Origin}|{right.SourceId}|{right.Kind}|{right.Value}", StringComparison.Ordinal));
    private static readonly IComparer<WorkspaceTimelineEntry> TimelineComparer = Comparer<WorkspaceTimelineEntry>.Create((left, right) => string.Compare($"{left.TimestampUtc:O}|{left.Type}|{left.SourceId}|{left.Description}", $"{right.TimestampUtc:O}|{right.Type}|{right.SourceId}|{right.Description}", StringComparison.Ordinal));
    private static readonly IComparer<WorkspaceWarning> WarningComparer = Comparer<WorkspaceWarning>.Create((left, right) => string.Compare($"{left.Code}|{left.Source}", $"{right.Code}|{right.Source}", StringComparison.Ordinal));
    private static readonly IComparer<WorkspaceIssue> IssueComparer = Comparer<WorkspaceIssue>.Create((left, right) => string.Compare($"{left.Code}|{left.Source}", $"{right.Code}|{right.Source}", StringComparison.Ordinal));
    private static readonly IComparer<WorkspaceLimitation> LimitationComparer = Comparer<WorkspaceLimitation>.Create((left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));
}
