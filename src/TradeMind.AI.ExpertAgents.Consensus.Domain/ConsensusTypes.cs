using System.Collections.ObjectModel;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.ExpertAgents.Consensus.Domain;

public sealed record ConsensusId
{
    public ConsensusId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Consensus id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ConsensusId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public enum ConsensusStrategy
{
    WeightedTransparent,
    Conservative,
    Robust
}

public enum ConsensusStatus
{
    Succeeded,
    PartiallySucceeded,
    NoEligibleAnalyses,
    Failed,
    TimedOut,
    Cancelled
}

public enum ConsensusLevel
{
    None,
    Weak,
    Moderate,
    Strong,
    Conflicted
}

public enum ConsensusConfidenceBand
{
    VeryLow,
    Low,
    Medium,
    High,
    VeryHigh
}

public enum ConsensusConflictKind
{
    Directional,
    Level,
    Scenario,
    RiskDirection,
    Invalidation
}

public enum ConsensusConflictSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum ConsensusRejectionCode
{
    InvalidResultStatus,
    InsufficientConfidence,
    InsufficientFreshness,
    DuplicateRun,
    ContextMismatch,
    InvalidResult,
    UnsupportedSchema,
    Unknown
}

public enum ConsensusRiskSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public sealed record ConsensusScore
{
    public ConsensusScore(double score, IReadOnlyCollection<string>? factors = null)
    {
        Validate(score, nameof(score));
        Score = score;
        Factors = CopyStrings(factors);
    }

    public double Score { get; }
    public IReadOnlyList<string> Factors { get; }

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

public sealed record ConsensusConfidence
{
    public ConsensusConfidence(
        double score,
        ConsensusConfidenceBand band,
        double coverage,
        double agreement,
        double disagreement,
        double uncertainty,
        IReadOnlyCollection<string>? factors = null,
        IReadOnlyCollection<string>? limitations = null)
    {
        Validate(score, nameof(score));
        Validate(coverage, nameof(coverage));
        Validate(agreement, nameof(agreement));
        Validate(disagreement, nameof(disagreement));
        Validate(uncertainty, nameof(uncertainty));
        Score = score;
        Band = band;
        Coverage = coverage;
        Agreement = agreement;
        Disagreement = disagreement;
        Uncertainty = uncertainty;
        Factors = CopyStrings(factors);
        Limitations = CopyStrings(limitations);
    }

    public double Score { get; }
    public ConsensusConfidenceBand Band { get; }
    public double Coverage { get; }
    public double Agreement { get; }
    public double Disagreement { get; }
    public double Uncertainty { get; }
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

public sealed record ConsensusWeight
{
    public ConsensusWeight(
        AgentRunId agentRunId,
        AgentId agentId,
        double rawWeight,
        double effectiveShare,
        bool capped,
        IReadOnlyCollection<string>? factors = null)
    {
        ArgumentNullException.ThrowIfNull(agentRunId);
        ArgumentNullException.ThrowIfNull(agentId);
        Validate(rawWeight, nameof(rawWeight));
        if (double.IsNaN(effectiveShare) || double.IsInfinity(effectiveShare) || effectiveShare is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(effectiveShare));
        }

        AgentRunId = agentRunId;
        AgentId = agentId;
        RawWeight = rawWeight;
        EffectiveShare = effectiveShare;
        Capped = capped;
        Factors = Array.AsReadOnly((factors ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray());
    }

    public AgentRunId AgentRunId { get; }
    public AgentId AgentId { get; }
    public double RawWeight { get; }
    public double EffectiveShare { get; }
    public bool Capped { get; }
    public IReadOnlyList<string> Factors { get; }

    private static void Validate(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

public sealed record ConsensusEligibleAnalysis
{
    public ConsensusEligibleAnalysis(
        AgentAnalysisResult result,
        bool contributesOpinion,
        double penalty,
        IReadOnlyCollection<string>? reasons = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (penalty is < 0 or > 1 || double.IsNaN(penalty) || double.IsInfinity(penalty))
        {
            throw new ArgumentOutOfRangeException(nameof(penalty));
        }

        Result = result;
        ContributesOpinion = contributesOpinion;
        Penalty = penalty;
        Reasons = CopyStrings(reasons);
    }

    public AgentAnalysisResult Result { get; }
    public bool ContributesOpinion { get; }
    public double Penalty { get; }
    public IReadOnlyList<string> Reasons { get; }

    private static IReadOnlyList<string> CopyStrings(IEnumerable<string>? values) =>
        Array.AsReadOnly((values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray());
}

public sealed record ConsensusRejectedAnalysis
{
    public ConsensusRejectedAnalysis(
        AgentAnalysisResult result,
        ConsensusRejectionCode code,
        string message)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Result = result;
        Code = code;
        Message = message.Trim();
    }

    public AgentAnalysisResult Result { get; }
    public ConsensusRejectionCode Code { get; }
    public string Message { get; }
}

public sealed record ConsensusMinorityOpinion
{
    public ConsensusMinorityOpinion(
        AgentDirectionalBias bias,
        double weightedShare,
        IReadOnlyCollection<AgentRunId> sourceRuns,
        string explanation)
    {
        if (weightedShare is < 0 or > 1 || double.IsNaN(weightedShare) || double.IsInfinity(weightedShare))
        {
            throw new ArgumentOutOfRangeException(nameof(weightedShare));
        }

        ArgumentNullException.ThrowIfNull(sourceRuns);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        Bias = bias;
        WeightedShare = weightedShare;
        SourceRuns = Array.AsReadOnly(sourceRuns.OrderBy(run => run.Value, StringComparer.Ordinal).ToArray());
        Explanation = explanation.Trim();
    }

    public AgentDirectionalBias Bias { get; }
    public double WeightedShare { get; }
    public IReadOnlyList<AgentRunId> SourceRuns { get; }
    public string Explanation { get; }
}

public sealed record ConsensusConflict
{
    public ConsensusConflict(
        ConsensusConflictKind kind,
        ConsensusConflictSeverity severity,
        string summary,
        IReadOnlyCollection<AgentRunId> sourceRuns,
        IReadOnlyCollection<ContextSourceReference>? references = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(sourceRuns);
        Kind = kind;
        Severity = severity;
        Summary = summary.Trim();
        SourceRuns = Array.AsReadOnly(sourceRuns.OrderBy(run => run.Value, StringComparer.Ordinal).ToArray());
        References = Array.AsReadOnly((references ?? []).ToArray());
    }

    public ConsensusConflictKind Kind { get; }
    public ConsensusConflictSeverity Severity { get; }
    public string Summary { get; }
    public IReadOnlyList<AgentRunId> SourceRuns { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
}

public sealed record ConsensusSourceTrace
{
    public ConsensusSourceTrace(
        AgentRunId agentRunId,
        AgentId agentId,
        AgentVersion agentVersion,
        IReadOnlyCollection<ContextSourceReference> references)
    {
        ArgumentNullException.ThrowIfNull(agentRunId);
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(agentVersion);
        ArgumentNullException.ThrowIfNull(references);
        AgentRunId = agentRunId;
        AgentId = agentId;
        AgentVersion = agentVersion;
        References = Array.AsReadOnly(references.ToArray());
    }

    public AgentRunId AgentRunId { get; }
    public AgentId AgentId { get; }
    public AgentVersion AgentVersion { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
}

public sealed record ConsensusMarketLevel
{
    public ConsensusMarketLevel(
        AgentMarketLevelType type,
        Price price,
        Timeframe timeframe,
        AgentObservationImportance importance,
        string reason,
        IReadOnlyCollection<AgentRunId> sourceRuns,
        Price? lowerBound = null,
        Price? upperBound = null,
        IReadOnlyCollection<ContextSourceReference>? references = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(sourceRuns);
        if (lowerBound is not null && upperBound is not null && upperBound.Value.Value < lowerBound.Value.Value)
        {
            throw new ArgumentException("A consolidated level upper bound cannot be below its lower bound.", nameof(upperBound));
        }

        Type = type;
        Price = price;
        Timeframe = timeframe ?? throw new ArgumentNullException(nameof(timeframe));
        Importance = importance;
        Reason = reason.Trim();
        SourceRuns = Array.AsReadOnly(sourceRuns.OrderBy(run => run.Value, StringComparer.Ordinal).ToArray());
        LowerBound = lowerBound;
        UpperBound = upperBound;
        References = Array.AsReadOnly((references ?? []).ToArray());
    }

    public AgentMarketLevelType Type { get; }
    public Price Price { get; }
    public Timeframe Timeframe { get; }
    public AgentObservationImportance Importance { get; }
    public string Reason { get; }
    public IReadOnlyList<AgentRunId> SourceRuns { get; }
    public Price? LowerBound { get; }
    public Price? UpperBound { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
}

public sealed record ConsensusScenario
{
    public ConsensusScenario(
        string id,
        string title,
        string description,
        AgentDirectionalBias direction,
        IReadOnlyCollection<string> activationConditions,
        IReadOnlyCollection<string> invalidationConditions,
        IReadOnlyCollection<string> levelIds,
        IReadOnlyCollection<string> risks,
        TimeSpan? horizon,
        double confidence,
        IReadOnlyCollection<AgentRunId> sourceRuns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(activationConditions);
        ArgumentNullException.ThrowIfNull(invalidationConditions);
        ArgumentNullException.ThrowIfNull(levelIds);
        ArgumentNullException.ThrowIfNull(risks);
        ArgumentNullException.ThrowIfNull(sourceRuns);
        if (confidence is < 0 or > 100 || double.IsNaN(confidence) || double.IsInfinity(confidence))
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        Id = id.Trim();
        Title = title.Trim();
        Description = description.Trim();
        Direction = direction;
        ActivationConditions = CopyStrings(activationConditions);
        InvalidationConditions = CopyStrings(invalidationConditions);
        LevelIds = CopyStrings(levelIds);
        Risks = CopyStrings(risks);
        Horizon = horizon;
        Confidence = confidence;
        SourceRuns = Array.AsReadOnly(sourceRuns.OrderBy(run => run.Value, StringComparer.Ordinal).ToArray());
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public AgentDirectionalBias Direction { get; }
    public IReadOnlyList<string> ActivationConditions { get; }
    public IReadOnlyList<string> InvalidationConditions { get; }
    public IReadOnlyList<string> LevelIds { get; }
    public IReadOnlyList<string> Risks { get; }
    public TimeSpan? Horizon { get; }
    public double Confidence { get; }
    public IReadOnlyList<AgentRunId> SourceRuns { get; }

    private static IReadOnlyList<string> CopyStrings(IEnumerable<string> values) =>
        Array.AsReadOnly(values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray());
}

public sealed record ConsensusRisk
{
    public ConsensusRisk(
        string description,
        ConsensusRiskSeverity severity,
        IReadOnlyCollection<AgentRunId> sourceRuns,
        IReadOnlyCollection<ContextSourceReference>? references = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(sourceRuns);
        Description = description.Trim();
        Severity = severity;
        SourceRuns = Array.AsReadOnly(sourceRuns.OrderBy(run => run.Value, StringComparer.Ordinal).ToArray());
        References = Array.AsReadOnly((references ?? []).ToArray());
    }

    public string Description { get; }
    public ConsensusRiskSeverity Severity { get; }
    public IReadOnlyList<AgentRunId> SourceRuns { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
}

public sealed record ConsensusInvalidation
{
    public ConsensusInvalidation(
        string description,
        IReadOnlyCollection<AgentRunId> sourceRuns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(sourceRuns);
        Description = description.Trim();
        SourceRuns = Array.AsReadOnly(sourceRuns.OrderBy(run => run.Value, StringComparer.Ordinal).ToArray());
    }

    public string Description { get; }
    public IReadOnlyList<AgentRunId> SourceRuns { get; }
}

public sealed record ConsensusConclusion
{
    public ConsensusConclusion(string summary, AgentDirectionalBias bias, ConsensusLevel level)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        Summary = summary.Trim();
        Bias = bias;
        Level = level;
    }

    public string Summary { get; }
    public AgentDirectionalBias Bias { get; }
    public ConsensusLevel Level { get; }
}

public sealed record ConsensusError
{
    public ConsensusError(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
    }

    public string Code { get; }
    public string Message { get; }
}

public sealed record ConsensusRequest
{
    public const int CurrentVersion = 1;

    public ConsensusRequest(
        ConsensusId consensusId,
        MarketContextId marketContextId,
        IReadOnlyCollection<AgentAnalysisResult> analyses,
        ConsensusStrategy strategy = ConsensusStrategy.WeightedTransparent,
        TimeSpan? timeout = null,
        int version = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(consensusId);
        ArgumentNullException.ThrowIfNull(marketContextId);
        ArgumentNullException.ThrowIfNull(analyses);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        if (timeout is { } value && value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        Version = version;
        ConsensusId = consensusId;
        MarketContextId = marketContextId;
        Analyses = Array.AsReadOnly(analyses.ToArray());
        Strategy = strategy;
        Timeout = timeout;
    }

    public int Version { get; }
    public ConsensusId ConsensusId { get; }
    public MarketContextId MarketContextId { get; }
    public IReadOnlyList<AgentAnalysisResult> Analyses { get; }
    public ConsensusStrategy Strategy { get; }
    public TimeSpan? Timeout { get; }
}

public sealed record ConsensusResult
{
    public const int CurrentSchemaVersion = 1;

    public ConsensusResult(
        ConsensusId consensusId,
        MarketContextId marketContextId,
        ConsensusStrategy strategy,
        ConsensusStatus status,
        ConsensusLevel level,
        AgentDirectionalBias consolidatedBias,
        ConsensusConfidence confidence,
        ConsensusScore agreement,
        ConsensusScore disagreement,
        ConsensusConclusion conclusion,
        IReadOnlyCollection<ConsensusEligibleAnalysis> eligibleAnalyses,
        IReadOnlyCollection<ConsensusWeight> weights,
        IReadOnlyCollection<ConsensusRejectedAnalysis> rejectedAnalyses,
        IReadOnlyCollection<ConsensusMinorityOpinion> minorityOpinions,
        IReadOnlyCollection<ConsensusConflict> conflicts,
        IReadOnlyCollection<ConsensusMarketLevel> marketLevels,
        IReadOnlyCollection<ConsensusScenario> scenarios,
        IReadOnlyCollection<ConsensusRisk> risks,
        IReadOnlyCollection<ConsensusInvalidation> invalidations,
        IReadOnlyCollection<ConsensusSourceTrace> sources,
        IReadOnlyCollection<string> warnings,
        IReadOnlyCollection<ConsensusError> errors,
        DateTimeOffset createdAtUtc,
        DateTimeOffset completedAtUtc,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(consensusId);
        ArgumentNullException.ThrowIfNull(marketContextId);
        ArgumentNullException.ThrowIfNull(confidence);
        ArgumentNullException.ThrowIfNull(agreement);
        ArgumentNullException.ThrowIfNull(disagreement);
        ArgumentNullException.ThrowIfNull(conclusion);
        ArgumentNullException.ThrowIfNull(eligibleAnalyses);
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(rejectedAnalyses);
        ArgumentNullException.ThrowIfNull(minorityOpinions);
        ArgumentNullException.ThrowIfNull(conflicts);
        ArgumentNullException.ThrowIfNull(marketLevels);
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(risks);
        ArgumentNullException.ThrowIfNull(invalidations);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(errors);
        if (completedAtUtc < createdAtUtc)
        {
            throw new ArgumentException("Consensus completion cannot precede creation.", nameof(completedAtUtc));
        }

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        ConsensusId = consensusId;
        MarketContextId = marketContextId;
        Strategy = strategy;
        Status = status;
        Level = level;
        ConsolidatedBias = consolidatedBias;
        Confidence = confidence;
        Agreement = agreement;
        Disagreement = disagreement;
        Conclusion = conclusion;
        EligibleAnalyses = Array.AsReadOnly(eligibleAnalyses.ToArray());
        Weights = Array.AsReadOnly(weights.ToArray());
        RejectedAnalyses = Array.AsReadOnly(rejectedAnalyses.ToArray());
        MinorityOpinions = Array.AsReadOnly(minorityOpinions.ToArray());
        Conflicts = Array.AsReadOnly(conflicts.ToArray());
        MarketLevels = Array.AsReadOnly(marketLevels.ToArray());
        Scenarios = Array.AsReadOnly(scenarios.ToArray());
        Risks = Array.AsReadOnly(risks.ToArray());
        Invalidations = Array.AsReadOnly(invalidations.ToArray());
        Sources = Array.AsReadOnly(sources.ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
        Errors = Array.AsReadOnly(errors.ToArray());
        CreatedAtUtc = createdAtUtc;
        CompletedAtUtc = completedAtUtc;
        SchemaVersion = schemaVersion;
    }

    public ConsensusId ConsensusId { get; }
    public MarketContextId MarketContextId { get; }
    public ConsensusStrategy Strategy { get; }
    public ConsensusStatus Status { get; }
    public ConsensusLevel Level { get; }
    public AgentDirectionalBias ConsolidatedBias { get; }
    public ConsensusConfidence Confidence { get; }
    public ConsensusScore Agreement { get; }
    public ConsensusScore Disagreement { get; }
    public ConsensusConclusion Conclusion { get; }
    public IReadOnlyList<ConsensusEligibleAnalysis> EligibleAnalyses { get; }
    public IReadOnlyList<ConsensusWeight> Weights { get; }
    public IReadOnlyList<ConsensusRejectedAnalysis> RejectedAnalyses { get; }
    public IReadOnlyList<ConsensusMinorityOpinion> MinorityOpinions { get; }
    public IReadOnlyList<ConsensusConflict> Conflicts { get; }
    public IReadOnlyList<ConsensusMarketLevel> MarketLevels { get; }
    public IReadOnlyList<ConsensusScenario> Scenarios { get; }
    public IReadOnlyList<ConsensusRisk> Risks { get; }
    public IReadOnlyList<ConsensusInvalidation> Invalidations { get; }
    public IReadOnlyList<ConsensusSourceTrace> Sources { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<ConsensusError> Errors { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public int SchemaVersion { get; }
}

internal static class ConsensusCollections
{
    public static IReadOnlyList<T> CopyList<T>(IEnumerable<T>? values) =>
        Array.AsReadOnly(values?.ToArray() ?? []);

    public static IReadOnlyList<string> CopyStrings(IEnumerable<string>? values) =>
        Array.AsReadOnly((values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray());

    public static IReadOnlyDictionary<TKey, TValue> CopyDictionary<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> values)
        where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>(values));
}
