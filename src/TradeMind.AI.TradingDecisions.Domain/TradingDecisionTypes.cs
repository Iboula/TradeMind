using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Consensus.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.TradingDecisions.Domain;

public sealed record TradingDecisionId
{
    public TradingDecisionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Trading decision id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static TradingDecisionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public enum TradingDecisionType
{
    LongSetup,
    ShortSetup,
    Wait,
    Monitor,
    Conflicted,
    NoTrade,
    InsufficientData
}

public enum TradingDecisionStatus
{
    Succeeded,
    PartiallySucceeded,
    InsufficientData,
    Failed,
    TimedOut,
    Cancelled
}

public enum TradingDecisionStrategy
{
    Conservative,
    ConsensusAligned,
    EvidenceFirst
}

public enum TradingDecisionConfidenceBand
{
    VeryLow,
    Low,
    Medium,
    High,
    VeryHigh
}

public enum TradingDecisionRejectionCode
{
    UnsupportedRequestVersion,
    UnsupportedConsensusVersion,
    ConsensusMismatch,
    MarketContextMismatch,
    ConsensusStatusNotEligible,
    InsufficientConsensusConfidence,
    InsufficientConsensusCoverage,
    CriticalConflict,
    MissingDirectionalScenario,
    MissingInvalidation,
    MissingEntry,
    MissingStop,
    IncoherentLevels,
    InvalidRequest,
    Unknown
}

public enum DecisionTraceRole
{
    Consensus,
    Scenario,
    Entry,
    Stop,
    Target,
    Risk,
    Invalidation
}

public sealed record TradingDecisionConfidence
{
    public TradingDecisionConfidence(
        double score,
        TradingDecisionConfidenceBand band,
        double consensusConfidence,
        double coverage,
        IReadOnlyCollection<string>? factors = null,
        IReadOnlyCollection<string>? limitations = null)
    {
        Validate(score, nameof(score));
        Validate(consensusConfidence, nameof(consensusConfidence));
        Validate(coverage, nameof(coverage));
        Score = score;
        Band = band;
        ConsensusConfidence = consensusConfidence;
        Coverage = coverage;
        Factors = CopyStrings(factors);
        Limitations = CopyStrings(limitations);
    }

    public double Score { get; }
    public TradingDecisionConfidenceBand Band { get; }
    public double ConsensusConfidence { get; }
    public double Coverage { get; }
    public IReadOnlyList<string> Factors { get; }
    public IReadOnlyList<string> Limitations { get; }
    public bool IsProbabilityOfProfit => false;

    private static void Validate(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static IReadOnlyList<string> CopyStrings(IEnumerable<string>? values) =>
        Array.AsReadOnly((values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray());
}

public sealed record DecisionScenario
{
    public DecisionScenario(
        ConsensusScenario source,
        bool isPrimary,
        double selectionScore,
        string selectionReason)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (selectionScore is < 0 or > 100 || double.IsNaN(selectionScore) || double.IsInfinity(selectionScore))
        {
            throw new ArgumentOutOfRangeException(nameof(selectionScore));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(selectionReason);
        Source = source;
        IsPrimary = isPrimary;
        SelectionScore = selectionScore;
        SelectionReason = selectionReason.Trim();
    }

    public ConsensusScenario Source { get; }
    public bool IsPrimary { get; }
    public double SelectionScore { get; }
    public string SelectionReason { get; }
}

public sealed record EntryProposal
{
    public EntryProposal(
        Instrument instrument,
        Timeframe timeframe,
        Price price,
        AgentMarketLevelType sourceType,
        IReadOnlyCollection<AgentRunId> sourceRuns,
        IReadOnlyCollection<ContextSourceReference> references,
        string rationale)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(timeframe);
        if (sourceType != AgentMarketLevelType.Entry)
        {
            throw new ArgumentException("An entry proposal must originate from an Entry level.", nameof(sourceType));
        }

        ArgumentNullException.ThrowIfNull(sourceRuns);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);
        Instrument = instrument;
        Timeframe = timeframe;
        Price = price;
        SourceType = sourceType;
        SourceRuns = CopyRuns(sourceRuns);
        References = Array.AsReadOnly(references.ToArray());
        Rationale = rationale.Trim();
    }

    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public Price Price { get; }
    public AgentMarketLevelType SourceType { get; }
    public IReadOnlyList<AgentRunId> SourceRuns { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
    public string Rationale { get; }

    private static IReadOnlyList<AgentRunId> CopyRuns(IEnumerable<AgentRunId> values) =>
        Array.AsReadOnly(values.OrderBy(value => value.Value, StringComparer.Ordinal).ToArray());
}

public sealed record StopProposal
{
    public StopProposal(
        Instrument instrument,
        Timeframe timeframe,
        Price price,
        AgentMarketLevelType sourceType,
        IReadOnlyCollection<AgentRunId> sourceRuns,
        IReadOnlyCollection<ContextSourceReference> references,
        string rationale)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(timeframe);
        if (sourceType is not (AgentMarketLevelType.Stop or AgentMarketLevelType.Invalidation))
        {
            throw new ArgumentException("A stop proposal must originate from a Stop or Invalidation level.", nameof(sourceType));
        }

        ArgumentNullException.ThrowIfNull(sourceRuns);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);
        Instrument = instrument;
        Timeframe = timeframe;
        Price = price;
        SourceType = sourceType;
        SourceRuns = Array.AsReadOnly(sourceRuns.OrderBy(value => value.Value, StringComparer.Ordinal).ToArray());
        References = Array.AsReadOnly(references.ToArray());
        Rationale = rationale.Trim();
    }

    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public Price Price { get; }
    public AgentMarketLevelType SourceType { get; }
    public IReadOnlyList<AgentRunId> SourceRuns { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
    public string Rationale { get; }
}

public sealed record TargetProposal
{
    public TargetProposal(
        Instrument instrument,
        Timeframe timeframe,
        Price price,
        int ordinal,
        IReadOnlyCollection<AgentRunId> sourceRuns,
        IReadOnlyCollection<ContextSourceReference> references,
        string rationale)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(timeframe);
        if (ordinal <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        ArgumentNullException.ThrowIfNull(sourceRuns);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);
        Instrument = instrument;
        Timeframe = timeframe;
        Price = price;
        Ordinal = ordinal;
        SourceRuns = Array.AsReadOnly(sourceRuns.OrderBy(value => value.Value, StringComparer.Ordinal).ToArray());
        References = Array.AsReadOnly(references.ToArray());
        Rationale = rationale.Trim();
    }

    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public Price Price { get; }
    public int Ordinal { get; }
    public IReadOnlyList<AgentRunId> SourceRuns { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
    public string Rationale { get; }
}

public sealed record DecisionRisk
{
    public DecisionRisk(ConsensusRisk source, string rationale)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);
        Source = source;
        Rationale = rationale.Trim();
    }

    public ConsensusRisk Source { get; }
    public string Rationale { get; }
    public bool IsCritical => Source.Severity == ConsensusRiskSeverity.Critical;
}

public sealed record DecisionInvalidation
{
    public DecisionInvalidation(
        ConsensusInvalidation source,
        AgentDirectionalBias direction,
        bool isCoherent,
        string rationale)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);
        Source = source;
        Direction = direction;
        IsCoherent = isCoherent;
        Rationale = rationale.Trim();
    }

    public ConsensusInvalidation Source { get; }
    public AgentDirectionalBias Direction { get; }
    public bool IsCoherent { get; }
    public string Rationale { get; }
}

public sealed record DecisionTraceReference
{
    public DecisionTraceReference(
        AgentRunId agentRunId,
        AgentId agentId,
        AgentVersion agentVersion,
        ContextSourceReference reference,
        DecisionTraceRole role)
    {
        ArgumentNullException.ThrowIfNull(agentRunId);
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(agentVersion);
        ArgumentNullException.ThrowIfNull(reference);
        AgentRunId = agentRunId;
        AgentId = agentId;
        AgentVersion = agentVersion;
        Reference = reference;
        Role = role;
    }

    public AgentRunId AgentRunId { get; }
    public AgentId AgentId { get; }
    public AgentVersion AgentVersion { get; }
    public ContextSourceReference Reference { get; }
    public DecisionTraceRole Role { get; }
}

public sealed record TradingDecisionWarning
{
    public TradingDecisionWarning(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
    }

    public string Code { get; }
    public string Message { get; }
}

public sealed record TradingDecisionError
{
    public TradingDecisionError(string code, string message, bool fatal = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
        Fatal = fatal;
    }

    public string Code { get; }
    public string Message { get; }
    public bool Fatal { get; }
}

public sealed record TradingDecisionRequest
{
    public const int CurrentVersion = 1;

    public TradingDecisionRequest(
        TradingDecisionId decisionId,
        ConsensusId consensusId,
        ConsensusResult consensus,
        MarketContextId marketContextId,
        Instrument instrument,
        Timeframe timeframe,
        string userId,
        string sessionId,
        TradingDecisionStrategy strategy = TradingDecisionStrategy.Conservative,
        TimeSpan? timeout = null,
        int version = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(decisionId);
        ArgumentNullException.ThrowIfNull(consensusId);
        ArgumentNullException.ThrowIfNull(consensus);
        ArgumentNullException.ThrowIfNull(marketContextId);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(timeframe);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        if (timeout is { } value && value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        Version = version;
        DecisionId = decisionId;
        ConsensusId = consensusId;
        Consensus = consensus;
        MarketContextId = marketContextId;
        Instrument = instrument;
        Timeframe = timeframe;
        UserId = userId.Trim();
        SessionId = sessionId.Trim();
        Strategy = strategy;
        Timeout = timeout;
    }

    public TradingDecisionRequest(
        TradingDecisionId decisionId,
        ConsensusResult consensus,
        MarketContextId marketContextId,
        Instrument instrument,
        Timeframe timeframe,
        string userId,
        string sessionId,
        TradingDecisionStrategy strategy = TradingDecisionStrategy.Conservative,
        TimeSpan? timeout = null,
        int version = CurrentVersion)
        : this(
            decisionId,
            consensus?.ConsensusId ?? throw new ArgumentNullException(nameof(consensus)),
            consensus,
            marketContextId,
            instrument,
            timeframe,
            userId,
            sessionId,
            strategy,
            timeout,
            version)
    {
    }

    public int Version { get; }
    public TradingDecisionId DecisionId { get; }
    public ConsensusId ConsensusId { get; }
    public ConsensusResult Consensus { get; }
    public MarketContextId MarketContextId { get; }
    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public string UserId { get; }
    public string SessionId { get; }
    public TradingDecisionStrategy Strategy { get; }
    public TimeSpan? Timeout { get; }
}

public sealed record TradingDecisionResult
{
    public const int CurrentSchemaVersion = 1;

    public TradingDecisionResult(
        TradingDecisionId decisionId,
        ConsensusId consensusId,
        MarketContextId marketContextId,
        Instrument instrument,
        Timeframe timeframe,
        TradingDecisionStrategy strategy,
        TradingDecisionStatus status,
        TradingDecisionType type,
        TradingDecisionConfidence confidence,
        DecisionScenario? primaryScenario,
        IReadOnlyCollection<DecisionScenario> alternativeScenarios,
        EntryProposal? entry,
        StopProposal? stop,
        IReadOnlyCollection<TargetProposal> targets,
        IReadOnlyCollection<DecisionRisk> risks,
        IReadOnlyCollection<DecisionInvalidation> invalidations,
        IReadOnlyCollection<ConsensusConflict> conflicts,
        IReadOnlyCollection<DecisionTraceReference> traces,
        IReadOnlyCollection<TradingDecisionWarning> warnings,
        IReadOnlyCollection<TradingDecisionError> errors,
        DateTimeOffset createdAtUtc,
        DateTimeOffset completedAtUtc,
        int schemaVersion = CurrentSchemaVersion,
        string? userId = null,
        string? sessionId = null)
    {
        ArgumentNullException.ThrowIfNull(decisionId);
        ArgumentNullException.ThrowIfNull(consensusId);
        ArgumentNullException.ThrowIfNull(marketContextId);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(timeframe);
        ArgumentNullException.ThrowIfNull(confidence);
        ArgumentNullException.ThrowIfNull(alternativeScenarios);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(risks);
        ArgumentNullException.ThrowIfNull(invalidations);
        ArgumentNullException.ThrowIfNull(conflicts);
        ArgumentNullException.ThrowIfNull(traces);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(errors);
        if (completedAtUtc < createdAtUtc)
        {
            throw new ArgumentException("Decision completion cannot precede creation.", nameof(completedAtUtc));
        }

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        DecisionId = decisionId;
        ConsensusId = consensusId;
        MarketContextId = marketContextId;
        Instrument = instrument;
        Timeframe = timeframe;
        Strategy = strategy;
        Status = status;
        Type = type;
        Confidence = confidence;
        PrimaryScenario = primaryScenario;
        AlternativeScenarios = Array.AsReadOnly(alternativeScenarios.ToArray());
        Entry = entry;
        Stop = stop;
        Targets = Array.AsReadOnly(targets.ToArray());
        Risks = Array.AsReadOnly(risks.ToArray());
        Invalidations = Array.AsReadOnly(invalidations.ToArray());
        Conflicts = Array.AsReadOnly(conflicts.ToArray());
        Traces = Array.AsReadOnly(traces.ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
        Errors = Array.AsReadOnly(errors.ToArray());
        CreatedAtUtc = createdAtUtc;
        CompletedAtUtc = completedAtUtc;
        SchemaVersion = schemaVersion;
        UserId = userId?.Trim();
        SessionId = sessionId?.Trim();
    }

    public TradingDecisionId DecisionId { get; }
    public ConsensusId ConsensusId { get; }
    public MarketContextId MarketContextId { get; }
    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public TradingDecisionStrategy Strategy { get; }
    public TradingDecisionStatus Status { get; }
    public TradingDecisionType Type { get; }
    public TradingDecisionConfidence Confidence { get; }
    public DecisionScenario? PrimaryScenario { get; }
    public IReadOnlyList<DecisionScenario> AlternativeScenarios { get; }
    public EntryProposal? Entry { get; }
    public StopProposal? Stop { get; }
    public IReadOnlyList<TargetProposal> Targets { get; }
    public IReadOnlyList<DecisionRisk> Risks { get; }
    public IReadOnlyList<DecisionInvalidation> Invalidations { get; }
    public IReadOnlyList<ConsensusConflict> Conflicts { get; }
    public IReadOnlyList<DecisionTraceReference> Traces { get; }
    public IReadOnlyList<TradingDecisionWarning> Warnings { get; }
    public IReadOnlyList<TradingDecisionError> Errors { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public int SchemaVersion { get; }
    public string? UserId { get; }
    public string? SessionId { get; }
}
