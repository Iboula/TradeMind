using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Consensus.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.AI.RiskEngine.Domain;
using TradeMind.AI.TradingDecisions.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.TradingPlans.Domain;

public sealed record TradingPlanId
{
    public TradingPlanId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Trading plan id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static TradingPlanId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public enum TradingPlanStatus
{
    Succeeded,
    PartiallySucceeded,
    Rejected,
    InsufficientData,
    Expired,
    Failed,
    TimedOut,
    Cancelled
}

public enum TradingPlanType
{
    ExecutableCandidate,
    NoTradePlan,
    WaitPlan,
    MonitorPlan,
    ConflictedPlan,
    InsufficientDataPlan,
    ExpiredPlan
}

public enum TradingPlanStrategy
{
    Conservative,
    DecisionAligned,
    EvidenceFirst
}

public enum TradingPlanRiskOutcome
{
    Approved,
    ApprovedWithReduction,
    Rejected,
    InsufficientData
}

public enum TradingPlanEligibilityCode
{
    UnsupportedRequestVersion,
    UnsupportedDecisionVersion,
    UnsupportedRiskVersion,
    DecisionIdMismatch,
    RiskAssessmentIdMismatch,
    ContextMismatch,
    InstrumentMismatch,
    TimeframeMismatch,
    DecisionNotEligible,
    RiskNotEligible,
    MissingScenario,
    MissingEntry,
    MissingStop,
    MissingTargets,
    MissingQuantity,
    MissingInvalidation,
    DivergentDirection,
    DivergentLevels,
    DivergentQuantity,
    BlockingConflict,
    InvalidRequest,
    Unknown
}

public enum TradingPlanDirection
{
    None,
    Long,
    Short
}

public enum TradingPlanConditionType
{
    Activation,
    Invalidation,
    Abandonment
}

public enum TradingPlanRuleType
{
    WaitForActivation,
    CancelOnInvalidation,
    DoNotExceedApprovedQuantity,
    DoNotWidenStop,
    RespectTargets,
    ReevaluateRiskOnEntryChange,
    ExpirePlan,
    RebuildContextWhenStale
}

public enum PreTradeChecklistItemType
{
    DecisionValid,
    RiskApproved,
    ContextFresh,
    InstrumentCoherent,
    DirectionCoherent,
    EntryPresent,
    StopPresent,
    TargetsPresent,
    QuantityFromRiskEngine,
    InvalidationPresent,
    DailyWeeklyConstraintsValid,
    CriticalRisksAcknowledged,
    NoBlockingConflict,
    UserConfirmationRequired
}

public sealed record TradingPlanWarning
{
    public TradingPlanWarning(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
    }

    public string Code { get; }
    public string Message { get; }
}

public sealed record TradingPlanError
{
    public TradingPlanError(string code, string message, bool blocking = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
        Blocking = blocking;
    }

    public string Code { get; }
    public string Message { get; }
    public bool Blocking { get; }
}

public sealed record TradingPlanLimitation
{
    public TradingPlanLimitation(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
    }

    public string Code { get; }
    public string Message { get; }
}

public sealed record TradingPlanScenario
{
    public TradingPlanScenario(DecisionScenario source, bool isPrimary)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        IsPrimary = isPrimary;
        Id = source.Source.Id;
        Title = source.Source.Title;
        Description = source.Source.Description;
        Direction = source.Source.Direction;
        SelectionScore = source.SelectionScore;
        SelectionReason = source.SelectionReason;
    }

    public DecisionScenario Source { get; }
    public bool IsPrimary { get; }
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public AgentDirectionalBias Direction { get; }
    public double SelectionScore { get; }
    public string SelectionReason { get; }
}

public sealed record TradingPlanEntry
{
    public TradingPlanEntry(EntryProposal source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        Instrument = source.Instrument;
        Timeframe = source.Timeframe;
        Price = source.Price;
        SourceRuns = Array.AsReadOnly(source.SourceRuns.ToArray());
        References = Array.AsReadOnly(source.References.ToArray());
        Rationale = source.Rationale;
    }

    public EntryProposal Source { get; }
    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public Price Price { get; }
    public IReadOnlyList<AgentRunId> SourceRuns { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
    public string Rationale { get; }
}

public sealed record TradingPlanStop
{
    public TradingPlanStop(StopProposal source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        Instrument = source.Instrument;
        Timeframe = source.Timeframe;
        Price = source.Price;
        SourceRuns = Array.AsReadOnly(source.SourceRuns.ToArray());
        References = Array.AsReadOnly(source.References.ToArray());
        Rationale = source.Rationale;
    }

    public StopProposal Source { get; }
    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public Price Price { get; }
    public IReadOnlyList<AgentRunId> SourceRuns { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
    public string Rationale { get; }
}

public sealed record TradingPlanTarget
{
    public TradingPlanTarget(TargetProposal source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        Instrument = source.Instrument;
        Timeframe = source.Timeframe;
        Price = source.Price;
        Ordinal = source.Ordinal;
        SourceRuns = Array.AsReadOnly(source.SourceRuns.ToArray());
        References = Array.AsReadOnly(source.References.ToArray());
        Rationale = source.Rationale;
    }

    public TargetProposal Source { get; }
    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public Price Price { get; }
    public int Ordinal { get; }
    public IReadOnlyList<AgentRunId> SourceRuns { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
    public string Rationale { get; }
}

public sealed record TradingPlanQuantity
{
    public TradingPlanQuantity(PositionSizeProposal source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        RawQuantity = source.RawQuantity;
        RoundedQuantity = source.RoundedQuantity;
        FinalQuantity = source.FinalQuantity;
        ActualExposure = source.ActualExposure;
        Reduced = source.Reduced;
        ReductionReasons = Array.AsReadOnly(source.ReductionReasons.ToArray());
        Formula = source.Formula;
    }

    public PositionSizeProposal Source { get; }
    public decimal RawQuantity { get; }
    public decimal RoundedQuantity { get; }
    public decimal FinalQuantity { get; }
    public Money ActualRisk => Source.ActualRisk;
    public Money? ActualExposure { get; }
    public bool Reduced { get; }
    public IReadOnlyList<string> ReductionReasons { get; }
    public string Formula { get; }
}

public sealed record TradingPlanCondition
{
    public TradingPlanCondition(TradingPlanConditionType type, string expression, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        Type = type;
        Expression = expression.Trim();
        Source = source.Trim();
    }

    public TradingPlanConditionType Type { get; }
    public string Expression { get; }
    public string Source { get; }
}

public sealed record TradingPlanInvalidation
{
    public TradingPlanInvalidation(DecisionInvalidation source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        Description = source.Source.Description;
        Direction = source.Direction;
        IsCoherent = source.IsCoherent;
        Rationale = source.Rationale;
    }

    public DecisionInvalidation Source { get; }
    public string Description { get; }
    public AgentDirectionalBias Direction { get; }
    public bool IsCoherent { get; }
    public string Rationale { get; }
}

public sealed record TradingPlanAbandonCriterion
{
    public TradingPlanAbandonCriterion(string criterion, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(criterion);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        Criterion = criterion.Trim();
        Source = source.Trim();
    }

    public string Criterion { get; }
    public string Source { get; }
}

public sealed record PreTradeChecklistItem
{
    public PreTradeChecklistItem(
        PreTradeChecklistItemType type,
        string code,
        string description,
        bool isSatisfied,
        bool isBlocking,
        string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        Type = type;
        Code = code.Trim();
        Description = description.Trim();
        IsSatisfied = isSatisfied;
        IsBlocking = isBlocking;
        Source = source.Trim();
    }

    public PreTradeChecklistItemType Type { get; }
    public string Code { get; }
    public string Description { get; }
    public bool IsSatisfied { get; }
    public bool IsBlocking { get; }
    public string Source { get; }
}

public sealed record PreTradeChecklist
{
    public PreTradeChecklist(IReadOnlyCollection<PreTradeChecklistItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = Array.AsReadOnly(items.ToArray());
        IsBlocking = Items.Any(item => item.IsBlocking && !item.IsSatisfied);
        AllSatisfied = Items.All(item => item.IsSatisfied);
    }

    public IReadOnlyList<PreTradeChecklistItem> Items { get; }
    public bool IsBlocking { get; }
    public bool AllSatisfied { get; }
}

public sealed record DeclarativeTradeManagementRule
{
    public DeclarativeTradeManagementRule(TradingPlanRuleType type, string instruction, string source, decimal? tolerance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (tolerance is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }

        Type = type;
        Instruction = instruction.Trim();
        Source = source.Trim();
        Tolerance = tolerance;
    }

    public TradingPlanRuleType Type { get; }
    public string Instruction { get; }
    public string Source { get; }
    public decimal? Tolerance { get; }
}

public sealed record TradingPlanRisk
{
    public TradingPlanRisk(DecisionRisk source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        Description = source.Source.Description;
        Severity = source.Source.Severity;
        IsCritical = source.IsCritical;
        Rationale = source.Rationale;
    }

    public DecisionRisk Source { get; }
    public string Description { get; }
    public ConsensusRiskSeverity Severity { get; }
    public bool IsCritical { get; }
    public string Rationale { get; }
}

public sealed record TradingPlanTraceReference
{
    public TradingPlanTraceReference(DecisionTraceReference source, string origin)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        Source = source;
        Origin = origin.Trim();
    }

    public DecisionTraceReference Source { get; }
    public string Origin { get; }
}

public sealed record TradingPlanRequest
{
    public const int CurrentVersion = 1;

    public TradingPlanRequest(
        TradingPlanId planId,
        TradingDecisionId decisionId,
        RiskAssessmentId riskAssessmentId,
        MarketContextId marketContextId,
        TradingDecisionResult decision,
        RiskAssessmentResult risk,
        TradingPlanStrategy strategy = TradingPlanStrategy.Conservative,
        DateTimeOffset? expiresAtUtc = null,
        TimeSpan? timeout = null,
        int version = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(planId);
        ArgumentNullException.ThrowIfNull(decisionId);
        ArgumentNullException.ThrowIfNull(riskAssessmentId);
        ArgumentNullException.ThrowIfNull(marketContextId);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(risk);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        if (timeout is { } value && value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        PlanId = planId;
        DecisionId = decisionId;
        RiskAssessmentId = riskAssessmentId;
        MarketContextId = marketContextId;
        Decision = decision;
        Risk = risk;
        Strategy = strategy;
        ExpiresAtUtc = expiresAtUtc;
        Timeout = timeout;
        Version = version;
    }

    public TradingPlanRequest(
        TradingPlanId planId,
        TradingDecisionResult decision,
        RiskAssessmentResult risk,
        TradingPlanStrategy strategy = TradingPlanStrategy.Conservative,
        DateTimeOffset? expiresAtUtc = null,
        TimeSpan? timeout = null,
        int version = CurrentVersion)
        : this(
            planId,
            decision?.DecisionId ?? throw new ArgumentNullException(nameof(decision)),
            risk?.AssessmentId ?? throw new ArgumentNullException(nameof(risk)),
            decision.MarketContextId,
            decision,
            risk,
            strategy,
            expiresAtUtc,
            timeout,
            version)
    {
    }

    public int Version { get; }
    public TradingPlanId PlanId { get; }
    public TradingDecisionId DecisionId { get; }
    public RiskAssessmentId RiskAssessmentId { get; }
    public MarketContextId MarketContextId { get; }
    public TradingDecisionResult Decision { get; }
    public RiskAssessmentResult Risk { get; }
    public TradingPlanStrategy Strategy { get; }
    public DateTimeOffset? ExpiresAtUtc { get; }
    public TimeSpan? Timeout { get; }
}

public sealed record TradingPlanResult
{
    public const int CurrentSchemaVersion = 1;

    public TradingPlanResult(
        TradingPlanId planId,
        TradingDecisionId decisionId,
        RiskAssessmentId riskAssessmentId,
        MarketContextId marketContextId,
        Instrument instrument,
        Timeframe timeframe,
        TradingPlanDirection direction,
        TradingPlanStrategy strategy,
        TradingPlanStatus status,
        TradingPlanType type,
        RiskVerdict riskVerdict,
        TradingPlanScenario? primaryScenario,
        IReadOnlyCollection<TradingPlanScenario> alternatives,
        TradingPlanEntry? entry,
        TradingPlanStop? stop,
        IReadOnlyCollection<TradingPlanTarget> targets,
        TradingPlanQuantity? quantity,
        IReadOnlyCollection<TradingPlanCondition> conditions,
        IReadOnlyCollection<TradingPlanInvalidation> invalidations,
        IReadOnlyCollection<TradingPlanAbandonCriterion> abandonCriteria,
        PreTradeChecklist checklist,
        IReadOnlyCollection<DeclarativeTradeManagementRule> managementRules,
        IReadOnlyCollection<TradingPlanRisk> risks,
        IReadOnlyCollection<TradingPlanTraceReference> traces,
        IReadOnlyCollection<TradingPlanLimitation> limitations,
        IReadOnlyCollection<TradingPlanWarning> warnings,
        IReadOnlyCollection<TradingPlanError> errors,
        string summary,
        DateTimeOffset createdAtUtc,
        DateTimeOffset completedAtUtc,
        DateTimeOffset expiresAtUtc,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(planId);
        ArgumentNullException.ThrowIfNull(decisionId);
        ArgumentNullException.ThrowIfNull(riskAssessmentId);
        ArgumentNullException.ThrowIfNull(marketContextId);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(timeframe);
        ArgumentNullException.ThrowIfNull(alternatives);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(invalidations);
        ArgumentNullException.ThrowIfNull(abandonCriteria);
        ArgumentNullException.ThrowIfNull(checklist);
        ArgumentNullException.ThrowIfNull(managementRules);
        ArgumentNullException.ThrowIfNull(risks);
        ArgumentNullException.ThrowIfNull(traces);
        ArgumentNullException.ThrowIfNull(limitations);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        if (completedAtUtc < createdAtUtc)
        {
            throw new ArgumentException("Plan completion cannot precede creation.", nameof(completedAtUtc));
        }

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        PlanId = planId;
        DecisionId = decisionId;
        RiskAssessmentId = riskAssessmentId;
        MarketContextId = marketContextId;
        Instrument = instrument;
        Timeframe = timeframe;
        Direction = direction;
        Strategy = strategy;
        Status = status;
        Type = type;
        RiskVerdict = riskVerdict;
        PrimaryScenario = primaryScenario;
        Alternatives = Array.AsReadOnly(alternatives.ToArray());
        Entry = entry;
        Stop = stop;
        Targets = Array.AsReadOnly(targets.ToArray());
        Quantity = quantity;
        Conditions = Array.AsReadOnly(conditions.ToArray());
        Invalidations = Array.AsReadOnly(invalidations.ToArray());
        AbandonCriteria = Array.AsReadOnly(abandonCriteria.ToArray());
        Checklist = checklist;
        ManagementRules = Array.AsReadOnly(managementRules.ToArray());
        Risks = Array.AsReadOnly(risks.ToArray());
        Traces = Array.AsReadOnly(traces.ToArray());
        Limitations = Array.AsReadOnly(limitations.ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
        Errors = Array.AsReadOnly(errors.ToArray());
        Summary = summary.Trim();
        CreatedAtUtc = createdAtUtc;
        CompletedAtUtc = completedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        SchemaVersion = schemaVersion;
    }

    public TradingPlanId PlanId { get; }
    public TradingDecisionId DecisionId { get; }
    public RiskAssessmentId RiskAssessmentId { get; }
    public MarketContextId MarketContextId { get; }
    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public TradingPlanDirection Direction { get; }
    public TradingPlanStrategy Strategy { get; }
    public TradingPlanStatus Status { get; }
    public TradingPlanType Type { get; }
    public RiskVerdict RiskVerdict { get; }
    public TradingPlanRiskOutcome RiskOutcome => RiskVerdict switch
    {
        RiskVerdict.Approved => TradingPlanRiskOutcome.Approved,
        RiskVerdict.Reduced => TradingPlanRiskOutcome.ApprovedWithReduction,
        RiskVerdict.InsufficientData => TradingPlanRiskOutcome.InsufficientData,
        _ => TradingPlanRiskOutcome.Rejected
    };
    public TradingPlanScenario? PrimaryScenario { get; }
    public IReadOnlyList<TradingPlanScenario> Alternatives { get; }
    public TradingPlanEntry? Entry { get; }
    public TradingPlanStop? Stop { get; }
    public IReadOnlyList<TradingPlanTarget> Targets { get; }
    public TradingPlanQuantity? Quantity { get; }
    public IReadOnlyList<TradingPlanCondition> Conditions { get; }
    public IReadOnlyList<TradingPlanInvalidation> Invalidations { get; }
    public IReadOnlyList<TradingPlanAbandonCriterion> AbandonCriteria { get; }
    public PreTradeChecklist Checklist { get; }
    public IReadOnlyList<DeclarativeTradeManagementRule> ManagementRules { get; }
    public IReadOnlyList<TradingPlanRisk> Risks { get; }
    public IReadOnlyList<TradingPlanTraceReference> Traces { get; }
    public IReadOnlyList<TradingPlanLimitation> Limitations { get; }
    public IReadOnlyList<TradingPlanWarning> Warnings { get; }
    public IReadOnlyList<TradingPlanError> Errors { get; }
    public string Summary { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public int SchemaVersion { get; }
}
