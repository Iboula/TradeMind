using System.Collections.ObjectModel;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Consensus.Domain;
using TradeMind.AI.ExpertAgents.Dispatch.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.AI.RiskEngine.Domain;
using TradeMind.AI.TradingDecisions.Domain;
using TradeMind.AI.TradingPlans.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.TradingWorkspace.Domain;

public sealed record WorkspaceId
{
    public WorkspaceId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workspace id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static WorkspaceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public enum TradingWorkspaceStatus
{
    Succeeded,
    PartiallySucceeded,
    Failed,
    TimedOut,
    Cancelled
}

public enum TradingWorkspaceState
{
    Empty,
    ContextReady,
    AnalysisReady,
    ConsensusReady,
    DecisionReady,
    RiskReady,
    PlanReady,
    Incomplete,
    Blocked,
    Conflicted,
    Expired
}

public enum TradingWorkspaceBuildMode
{
    Snapshot,
    Progressive
}

public enum WorkspacePipelineStageKind
{
    Context,
    Dispatch,
    ExpertAnalysis,
    Consensus,
    Decision,
    Risk,
    Plan
}

public enum WorkspaceStageStatus
{
    NotAvailable,
    Available,
    Succeeded,
    PartiallySucceeded,
    Failed,
    Blocked,
    Stale,
    Expired,
    Conflicted
}

public enum WorkspaceIssueCategory
{
    Validation,
    Consistency,
    Freshness,
    Completeness,
    Pipeline,
    Source
}

public enum WorkspaceIssueSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public enum WorkspaceTimelineEventType
{
    Context,
    Dispatch,
    AgentAnalysis,
    Consensus,
    Decision,
    Risk,
    Plan,
    Expiration,
    Blocker,
    WorkspaceBuilt
}

public enum WorkspaceNextActionType
{
    RefreshContext,
    RunExpertAnalysis,
    BuildConsensus,
    BuildDecision,
    AssessRisk,
    GeneratePlan,
    ReviewConflict,
    RequestUserConfirmation,
    NoAction
}

public sealed record WorkspaceTraceReference
{
    public WorkspaceTraceReference(
        string origin,
        string sourceId,
        string kind,
        string value,
        DateTimeOffset? timestampUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Origin = origin.Trim();
        SourceId = sourceId.Trim();
        Kind = kind.Trim();
        Value = value.Trim();
        TimestampUtc = timestampUtc;
    }

    public string Origin { get; }
    public string SourceId { get; }
    public string Kind { get; }
    public string Value { get; }
    public DateTimeOffset? TimestampUtc { get; }
}

public sealed record WorkspaceIssue
{
    public WorkspaceIssue(
        string code,
        WorkspaceIssueCategory category,
        WorkspaceIssueSeverity severity,
        bool blocking,
        string source,
        string explanation,
        IReadOnlyCollection<WorkspaceTraceReference>? traces = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        Code = code.Trim();
        Category = category;
        Severity = severity;
        Blocking = blocking;
        Source = source.Trim();
        Explanation = explanation.Trim();
        Traces = WorkspaceCollections.CopyList(traces);
    }

    public string Code { get; }
    public WorkspaceIssueCategory Category { get; }
    public WorkspaceIssueSeverity Severity { get; }
    public bool Blocking { get; }
    public string Source { get; }
    public string Explanation { get; }
    public IReadOnlyList<WorkspaceTraceReference> Traces { get; }
}

public sealed record WorkspaceWarning
{
    public WorkspaceWarning(
        string code,
        WorkspaceIssueCategory category,
        string source,
        string message,
        IReadOnlyCollection<WorkspaceTraceReference>? traces = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Category = category;
        Source = source.Trim();
        Message = message.Trim();
        Traces = WorkspaceCollections.CopyList(traces);
    }

    public string Code { get; }
    public WorkspaceIssueCategory Category { get; }
    public string Source { get; }
    public string Message { get; }
    public IReadOnlyList<WorkspaceTraceReference> Traces { get; }
}

public sealed record WorkspaceLimitation
{
    public WorkspaceLimitation(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
    }

    public string Code { get; }
    public string Message { get; }
}

public sealed record WorkspacePipelineStage
{
    public WorkspacePipelineStage(
        WorkspacePipelineStageKind stage,
        WorkspaceStageStatus status,
        bool available,
        bool valid,
        bool fresh,
        string? sourceId,
        DateTimeOffset? timestampUtc,
        bool blocking,
        IReadOnlyCollection<string>? warnings = null,
        IReadOnlyCollection<string>? errors = null)
    {
        if (warnings is null || errors is null)
        {
            throw new ArgumentNullException(warnings is null ? nameof(warnings) : nameof(errors));
        }

        Stage = stage;
        Status = status;
        Available = available;
        Valid = valid;
        Fresh = fresh;
        SourceId = string.IsNullOrWhiteSpace(sourceId) ? null : sourceId.Trim();
        TimestampUtc = timestampUtc;
        Blocking = blocking;
        Warnings = WorkspaceCollections.CopyStrings(warnings);
        Errors = WorkspaceCollections.CopyStrings(errors);
    }

    public WorkspacePipelineStageKind Stage { get; }
    public WorkspaceStageStatus Status { get; }
    public bool Available { get; }
    public bool Valid { get; }
    public bool Fresh { get; }
    public string? SourceId { get; }
    public DateTimeOffset? TimestampUtc { get; }
    public bool Blocking { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> Errors { get; }
}

public sealed record WorkspacePipelineProgress
{
    public WorkspacePipelineProgress(IReadOnlyCollection<WorkspacePipelineStage> stages)
    {
        ArgumentNullException.ThrowIfNull(stages);
        Stages = Array.AsReadOnly(stages.OrderBy(stage => stage.Stage).ToArray());
    }

    public IReadOnlyList<WorkspacePipelineStage> Stages { get; }
    public int AvailableStageCount => Stages.Count(stage => stage.Available);
    public int ValidStageCount => Stages.Count(stage => stage.Valid);
    public int BlockingStageCount => Stages.Count(stage => stage.Blocking);
}

public sealed record WorkspaceCompleteness
{
    public WorkspaceCompleteness(
        int availableStages,
        int totalStages,
        int missingDependencyCount,
        IReadOnlyCollection<WorkspacePipelineStage> missingStages)
    {
        if (availableStages < 0 || totalStages <= 0 || availableStages > totalStages || missingDependencyCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableStages));
        }

        ArgumentNullException.ThrowIfNull(missingStages);
        AvailableStages = availableStages;
        TotalStages = totalStages;
        MissingDependencyCount = missingDependencyCount;
        MissingStages = Array.AsReadOnly(missingStages.ToArray());
    }

    public int AvailableStages { get; }
    public int TotalStages { get; }
    public int MissingDependencyCount { get; }
    public IReadOnlyList<WorkspacePipelineStage> MissingStages { get; }
    public double Score => TotalStages == 0 ? 0 : AvailableStages * 100d / TotalStages;
}

public sealed record WorkspaceFreshness
{
    public WorkspaceFreshness(
        DateTimeOffset evaluatedAtUtc,
        TimeSpan? contextAge,
        TimeSpan? consensusAge,
        TimeSpan? decisionAge,
        TimeSpan? accountInstrumentAge,
        TimeSpan? riskAge,
        TimeSpan? planAge,
        DateTimeOffset? planExpiresAtUtc,
        DateTimeOffset? oldestCriticalTimestampUtc,
        bool contextStale,
        bool consensusStale,
        bool decisionStale,
        bool accountInstrumentStale,
        bool riskStale,
        bool planExpired)
    {
        EvaluatedAtUtc = evaluatedAtUtc;
        ContextAge = contextAge;
        ConsensusAge = consensusAge;
        DecisionAge = decisionAge;
        AccountInstrumentAge = accountInstrumentAge;
        RiskAge = riskAge;
        PlanAge = planAge;
        PlanExpiresAtUtc = planExpiresAtUtc;
        OldestCriticalTimestampUtc = oldestCriticalTimestampUtc;
        ContextStale = contextStale;
        ConsensusStale = consensusStale;
        DecisionStale = decisionStale;
        AccountInstrumentStale = accountInstrumentStale;
        RiskStale = riskStale;
        PlanExpired = planExpired;
    }

    public DateTimeOffset EvaluatedAtUtc { get; }
    public TimeSpan? ContextAge { get; }
    public TimeSpan? ConsensusAge { get; }
    public TimeSpan? DecisionAge { get; }
    public TimeSpan? AccountInstrumentAge { get; }
    public TimeSpan? RiskAge { get; }
    public TimeSpan? PlanAge { get; }
    public DateTimeOffset? PlanExpiresAtUtc { get; }
    public DateTimeOffset? OldestCriticalTimestampUtc { get; }
    public bool ContextStale { get; }
    public bool ConsensusStale { get; }
    public bool DecisionStale { get; }
    public bool AccountInstrumentStale { get; }
    public bool RiskStale { get; }
    public bool PlanExpired { get; }
    public bool IsStale => ContextStale || ConsensusStale || DecisionStale || AccountInstrumentStale || RiskStale;
}

public sealed record WorkspaceConsistency
{
    public WorkspaceConsistency(IReadOnlyCollection<WorkspaceIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public IReadOnlyList<WorkspaceIssue> Issues { get; }
    public bool IsConsistent => Issues.All(issue => !issue.Blocking);
    public bool HasBlockingIssues => Issues.Any(issue => issue.Blocking);
}

public sealed record WorkspaceContextSummary
{
    public WorkspaceContextSummary(MarketContext? source)
    {
        IsAvailable = source is not null;
        ContextId = source?.Id;
        Version = source?.Version;
        UserId = source?.UserId;
        SessionId = source?.SessionId;
        Instrument = source?.Instrument;
        Timeframe = source?.Timeframe;
        Status = source?.Status;
        BuiltAtUtc = source?.BuiltAtUtc;
        Quality = source?.Quality;
        SourceCount = source?.Traces.Count ?? 0;
    }

    public bool IsAvailable { get; }
    public MarketContextId? ContextId { get; }
    public int? Version { get; }
    public string? UserId { get; }
    public string? SessionId { get; }
    public Instrument? Instrument { get; }
    public Timeframe? Timeframe { get; }
    public MarketContextBuildStatus? Status { get; }
    public DateTimeOffset? BuiltAtUtc { get; }
    public ContextQuality? Quality { get; }
    public int SourceCount { get; }
}

public sealed record WorkspaceAnalysisSummary
{
    public WorkspaceAnalysisSummary(
        AgentDispatchPlan? dispatch,
        AgentDispatchExecutionResult? execution,
        IReadOnlyCollection<AgentAnalysisResult> analyses)
    {
        ArgumentNullException.ThrowIfNull(analyses);
        DispatchId = execution?.Plan.DispatchId ?? dispatch?.DispatchId;
        DispatchVersion = execution?.Plan.Version ?? dispatch?.Version;
        DispatchCreatedAtUtc = execution?.Plan.CreatedAtUtc ?? dispatch?.CreatedAtUtc;
        DispatchStatus = execution?.Status;
        AnalysisCount = analyses.Count;
        SuccessfulAnalysisCount = analyses.Count(result => result.Status == AgentAnalysisStatus.Succeeded);
        FailedAnalysisCount = analyses.Count(result => result.Status is AgentAnalysisStatus.Failed or AgentAnalysisStatus.TimedOut or AgentAnalysisStatus.Cancelled);
        AgentRunIds = WorkspaceCollections.CopyStrings(analyses.Select(result => result.AgentRunId.Value));
    }

    public DispatchId? DispatchId { get; }
    public int? DispatchVersion { get; }
    public DateTimeOffset? DispatchCreatedAtUtc { get; }
    public DispatchExecutionStatus? DispatchStatus { get; }
    public int AnalysisCount { get; }
    public int SuccessfulAnalysisCount { get; }
    public int FailedAnalysisCount { get; }
    public IReadOnlyList<string> AgentRunIds { get; }
    public bool IsAvailable => DispatchId is not null || AnalysisCount > 0;
}

public sealed record WorkspaceConsensusSummary
{
    public WorkspaceConsensusSummary(ConsensusResult? source)
    {
        IsAvailable = source is not null;
        ConsensusId = source?.ConsensusId;
        MarketContextId = source?.MarketContextId;
        Status = source?.Status;
        Level = source?.Level;
        Bias = source?.ConsolidatedBias;
        Confidence = source?.Confidence.Score;
        ConflictCount = source?.Conflicts.Count ?? 0;
        CriticalConflictCount = source?.Conflicts.Count(conflict => conflict.Severity == ConsensusConflictSeverity.Critical) ?? 0;
        CompletedAtUtc = source?.CompletedAtUtc;
    }

    public bool IsAvailable { get; }
    public ConsensusId? ConsensusId { get; }
    public MarketContextId? MarketContextId { get; }
    public ConsensusStatus? Status { get; }
    public ConsensusLevel? Level { get; }
    public AgentDirectionalBias? Bias { get; }
    public double? Confidence { get; }
    public int ConflictCount { get; }
    public int CriticalConflictCount { get; }
    public DateTimeOffset? CompletedAtUtc { get; }
}

public sealed record WorkspaceDecisionSummary
{
    public WorkspaceDecisionSummary(TradingDecisionResult? source)
    {
        IsAvailable = source is not null;
        DecisionId = source?.DecisionId;
        MarketContextId = source?.MarketContextId;
        Status = source?.Status;
        Type = source?.Type;
        Confidence = source?.Confidence.Score;
        EntryPresent = source?.Entry is not null;
        StopPresent = source?.Stop is not null;
        EntryPrice = source?.Entry?.Price;
        StopPrice = source?.Stop?.Price;
        TargetCount = source?.Targets.Count ?? 0;
        TargetPrices = WorkspaceCollections.CopyList(source?.Targets.OrderBy(target => target.Ordinal).Select(target => target.Price));
        Direction = source is null ? null : source.Type switch
        {
            TradingDecisionType.LongSetup => TradingPlanDirection.Long,
            TradingDecisionType.ShortSetup => TradingPlanDirection.Short,
            _ => TradingPlanDirection.None
        };
        CompletedAtUtc = source?.CompletedAtUtc;
    }

    public bool IsAvailable { get; }
    public TradingDecisionId? DecisionId { get; }
    public MarketContextId? MarketContextId { get; }
    public TradingDecisionStatus? Status { get; }
    public TradingDecisionType? Type { get; }
    public double? Confidence { get; }
    public TradingPlanDirection? Direction { get; }
    public bool EntryPresent { get; }
    public bool StopPresent { get; }
    public Price? EntryPrice { get; }
    public Price? StopPrice { get; }
    public int TargetCount { get; }
    public IReadOnlyList<Price> TargetPrices { get; }
    public DateTimeOffset? CompletedAtUtc { get; }
}

public sealed record WorkspaceRiskSummary
{
    public WorkspaceRiskSummary(RiskAssessmentResult? source)
    {
        IsAvailable = source is not null;
        AssessmentId = source?.AssessmentId;
        DecisionId = source?.DecisionId;
        MarketContextId = source?.MarketContextId;
        Status = source?.Status;
        Verdict = source?.Verdict;
        Quantity = source?.PositionSize?.FinalQuantity;
        Reduced = source?.PositionSize?.Reduced ?? false;
        CompletedAtUtc = source?.CompletedAtUtc;
    }

    public bool IsAvailable { get; }
    public RiskAssessmentId? AssessmentId { get; }
    public TradingDecisionId? DecisionId { get; }
    public MarketContextId? MarketContextId { get; }
    public RiskAssessmentStatus? Status { get; }
    public RiskVerdict? Verdict { get; }
    public decimal? Quantity { get; }
    public bool Reduced { get; }
    public DateTimeOffset? CompletedAtUtc { get; }
}

public sealed record WorkspacePlanSummary
{
    public WorkspacePlanSummary(TradingPlanResult? source, DateTimeOffset evaluatedAtUtc)
    {
        IsAvailable = source is not null;
        PlanId = source?.PlanId;
        DecisionId = source?.DecisionId;
        RiskAssessmentId = source?.RiskAssessmentId;
        MarketContextId = source?.MarketContextId;
        Status = source?.Status;
        Type = source?.Type;
        Direction = source?.Direction;
        Quantity = source?.Quantity?.FinalQuantity;
        EntryPrice = source?.Entry?.Price;
        StopPrice = source?.Stop?.Price;
        TargetPrices = WorkspaceCollections.CopyList(source?.Targets.OrderBy(target => target.Ordinal).Select(target => target.Price));
        ExpiresAtUtc = source?.ExpiresAtUtc;
        IsExpired = source is not null && source.ExpiresAtUtc <= evaluatedAtUtc;
        CompletedAtUtc = source?.CompletedAtUtc;
    }

    public bool IsAvailable { get; }
    public TradingPlanId? PlanId { get; }
    public TradingDecisionId? DecisionId { get; }
    public RiskAssessmentId? RiskAssessmentId { get; }
    public MarketContextId? MarketContextId { get; }
    public TradingPlanStatus? Status { get; }
    public TradingPlanType? Type { get; }
    public TradingPlanDirection? Direction { get; }
    public decimal? Quantity { get; }
    public Price? EntryPrice { get; }
    public Price? StopPrice { get; }
    public IReadOnlyList<Price> TargetPrices { get; }
    public DateTimeOffset? ExpiresAtUtc { get; }
    public bool IsExpired { get; }
    public DateTimeOffset? CompletedAtUtc { get; }
}

public sealed record WorkspaceRiskProjection
{
    public WorkspaceRiskProjection(string origin, string description, ConsensusRiskSeverity severity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Origin = origin.Trim();
        Description = description.Trim();
        Severity = severity;
    }

    public string Origin { get; }
    public string Description { get; }
    public ConsensusRiskSeverity Severity { get; }
}

public sealed record WorkspaceTimelineEntry
{
    public WorkspaceTimelineEntry(
        WorkspaceTimelineEventType type,
        DateTimeOffset timestampUtc,
        string source,
        string sourceId,
        string description,
        bool blocking = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Type = type;
        TimestampUtc = timestampUtc;
        Source = source.Trim();
        SourceId = sourceId.Trim();
        Description = description.Trim();
        Blocking = blocking;
    }

    public WorkspaceTimelineEventType Type { get; }
    public DateTimeOffset TimestampUtc { get; }
    public string Source { get; }
    public string SourceId { get; }
    public string Description { get; }
    public bool Blocking { get; }
}

public sealed record WorkspaceAlert
{
    public WorkspaceAlert(WorkspaceIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        Issue = issue;
    }

    public WorkspaceIssue Issue { get; }
    public string Code => Issue.Code;
    public bool Blocking => Issue.Blocking;
}

public sealed record WorkspaceBlocker
{
    public WorkspaceBlocker(string code, string reason, string source, WorkspaceNextActionType nextAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        Code = code.Trim();
        Reason = reason.Trim();
        Source = source.Trim();
        NextAction = nextAction;
    }

    public string Code { get; }
    public string Reason { get; }
    public string Source { get; }
    public WorkspaceNextActionType NextAction { get; }
}

public sealed record WorkspaceNextAction
{
    public WorkspaceNextAction(WorkspaceNextActionType type, string reason, string source, bool blocking)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        Type = type;
        Reason = reason.Trim();
        Source = source.Trim();
        Blocking = blocking;
    }

    public WorkspaceNextActionType Type { get; }
    public string Reason { get; }
    public string Source { get; }
    public bool Blocking { get; }
}

public sealed record TradingWorkspaceRequest
{
    public const int CurrentVersion = 1;

    public TradingWorkspaceRequest(
        WorkspaceId workspaceId,
        MarketContext? context = null,
        AgentDispatchPlan? dispatch = null,
        AgentDispatchExecutionResult? dispatchExecution = null,
        IReadOnlyCollection<AgentAnalysisResult>? analyses = null,
        ConsensusResult? consensus = null,
        TradingDecisionResult? decision = null,
        RiskAssessmentResult? risk = null,
        TradingPlanResult? plan = null,
        TradingWorkspaceBuildMode buildMode = TradingWorkspaceBuildMode.Progressive,
        TimeSpan? timeout = null,
        int version = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        if (timeout is { } value && value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        Version = version;
        WorkspaceId = workspaceId;
        Context = context;
        Dispatch = dispatch;
        DispatchExecution = dispatchExecution;
        Analyses = WorkspaceCollections.CopyList(analyses);
        Consensus = consensus;
        Decision = decision;
        Risk = risk;
        Plan = plan;
        BuildMode = buildMode;
        Timeout = timeout;
    }

    public int Version { get; }
    public WorkspaceId WorkspaceId { get; }
    public MarketContext? Context { get; }
    public AgentDispatchPlan? Dispatch { get; }
    public AgentDispatchExecutionResult? DispatchExecution { get; }
    public IReadOnlyList<AgentAnalysisResult> Analyses { get; }
    public ConsensusResult? Consensus { get; }
    public TradingDecisionResult? Decision { get; }
    public RiskAssessmentResult? Risk { get; }
    public TradingPlanResult? Plan { get; }
    public TradingWorkspaceBuildMode BuildMode { get; }
    public TimeSpan? Timeout { get; }
}

public sealed record TradingWorkspaceResult
{
    public const int CurrentSchemaVersion = 1;

    public TradingWorkspaceResult(
        WorkspaceId workspaceId,
        TradingWorkspaceStatus status,
        TradingWorkspaceState state,
        TradingWorkspaceBuildMode buildMode,
        MarketContextId? marketContextId,
        Instrument? instrument,
        Timeframe? timeframe,
        WorkspaceContextSummary context,
        WorkspaceAnalysisSummary analysis,
        WorkspaceConsensusSummary consensus,
        WorkspaceDecisionSummary decision,
        WorkspaceRiskSummary risk,
        WorkspacePlanSummary plan,
        WorkspacePipelineProgress progress,
        WorkspaceCompleteness completeness,
        WorkspaceFreshness freshness,
        WorkspaceConsistency consistency,
        IReadOnlyCollection<WorkspaceTimelineEntry> timeline,
        IReadOnlyCollection<WorkspaceAlert> alerts,
        IReadOnlyCollection<WorkspaceBlocker> blockers,
        IReadOnlyCollection<WorkspaceNextAction> nextActions,
        IReadOnlyCollection<WorkspaceRiskProjection> criticalRisks,
        IReadOnlyCollection<WorkspaceTraceReference> traces,
        IReadOnlyCollection<WorkspaceWarning> warnings,
        IReadOnlyCollection<WorkspaceLimitation> limitations,
        IReadOnlyCollection<WorkspaceIssue> errors,
        string summary,
        DateTimeOffset createdAtUtc,
        DateTimeOffset completedAtUtc,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(consensus);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(risk);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(completeness);
        ArgumentNullException.ThrowIfNull(freshness);
        ArgumentNullException.ThrowIfNull(consistency);
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(blockers);
        ArgumentNullException.ThrowIfNull(nextActions);
        ArgumentNullException.ThrowIfNull(criticalRisks);
        ArgumentNullException.ThrowIfNull(traces);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(limitations);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        if (completedAtUtc < createdAtUtc)
        {
            throw new ArgumentException("Workspace completion cannot precede creation.", nameof(completedAtUtc));
        }

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        WorkspaceId = workspaceId;
        Status = status;
        State = state;
        BuildMode = buildMode;
        MarketContextId = marketContextId;
        Instrument = instrument;
        Timeframe = timeframe;
        Context = context;
        Analysis = analysis;
        Consensus = consensus;
        Decision = decision;
        Risk = risk;
        Plan = plan;
        Progress = progress;
        Completeness = completeness;
        Freshness = freshness;
        Consistency = consistency;
        Timeline = Array.AsReadOnly(timeline.OrderBy(item => item.TimestampUtc).ThenBy(item => item.Type).ThenBy(item => item.SourceId, StringComparer.Ordinal).ToArray());
        Alerts = Array.AsReadOnly(alerts.ToArray());
        Blockers = Array.AsReadOnly(blockers.ToArray());
        NextActions = Array.AsReadOnly(nextActions.ToArray());
        CriticalRisks = Array.AsReadOnly(criticalRisks.ToArray());
        Traces = Array.AsReadOnly(traces.ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
        Limitations = Array.AsReadOnly(limitations.ToArray());
        Errors = Array.AsReadOnly(errors.ToArray());
        Summary = summary.Trim();
        CreatedAtUtc = createdAtUtc;
        CompletedAtUtc = completedAtUtc;
        SchemaVersion = schemaVersion;
    }

    public WorkspaceId WorkspaceId { get; }
    public TradingWorkspaceStatus Status { get; }
    public TradingWorkspaceState State { get; }
    public TradingWorkspaceBuildMode BuildMode { get; }
    public MarketContextId? MarketContextId { get; }
    public Instrument? Instrument { get; }
    public Timeframe? Timeframe { get; }
    public WorkspaceContextSummary Context { get; }
    public WorkspaceAnalysisSummary Analysis { get; }
    public WorkspaceConsensusSummary Consensus { get; }
    public WorkspaceDecisionSummary Decision { get; }
    public WorkspaceRiskSummary Risk { get; }
    public WorkspacePlanSummary Plan { get; }
    public WorkspacePipelineProgress Progress { get; }
    public WorkspaceCompleteness Completeness { get; }
    public WorkspaceFreshness Freshness { get; }
    public WorkspaceConsistency Consistency { get; }
    public IReadOnlyList<WorkspaceTimelineEntry> Timeline { get; }
    public IReadOnlyList<WorkspaceAlert> Alerts { get; }
    public IReadOnlyList<WorkspaceBlocker> Blockers { get; }
    public IReadOnlyList<WorkspaceNextAction> NextActions { get; }
    public IReadOnlyList<WorkspaceRiskProjection> CriticalRisks { get; }
    public IReadOnlyList<WorkspaceTraceReference> Traces { get; }
    public IReadOnlyList<WorkspaceWarning> Warnings { get; }
    public IReadOnlyList<WorkspaceLimitation> Limitations { get; }
    public IReadOnlyList<WorkspaceIssue> Errors { get; }
    public string Summary { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public int SchemaVersion { get; }
}

internal static class WorkspaceCollections
{
    public static IReadOnlyList<T> CopyList<T>(IEnumerable<T>? values) =>
        Array.AsReadOnly(values?.Where(value => value is not null).ToArray() ?? []);

    public static IReadOnlyList<string> CopyStrings(IEnumerable<string>? values) =>
        Array.AsReadOnly((values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());

    public static IReadOnlyDictionary<TKey, TValue> CopyDictionary<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>? values)
        where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>(values is null ? new Dictionary<TKey, TValue>() : new Dictionary<TKey, TValue>(values));
}
