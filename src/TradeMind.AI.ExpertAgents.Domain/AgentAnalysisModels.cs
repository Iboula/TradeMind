using TradeMind.AI.Context.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.ExpertAgents.Domain;

public sealed record AgentConfidence
{
    public AgentConfidence(
        double score,
        AgentConfidenceBand band,
        IReadOnlyCollection<string>? positiveFactors = null,
        IReadOnlyCollection<string>? negativeFactors = null,
        double dataCoverage = 0,
        double contextFreshness = 0,
        IReadOnlyCollection<string>? limitations = null,
        string method = "not-specified")
    {
        ValidateScore(score, nameof(score));
        ValidateScore(dataCoverage, nameof(dataCoverage));
        ValidateScore(contextFreshness, nameof(contextFreshness));
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        Score = score;
        Band = band;
        PositiveFactors = AgentCollections.CopyStrings(positiveFactors);
        NegativeFactors = AgentCollections.CopyStrings(negativeFactors);
        DataCoverage = dataCoverage;
        ContextFreshness = contextFreshness;
        Limitations = AgentCollections.CopyStrings(limitations);
        Method = method.Trim();
    }

    public double Score { get; }
    public AgentConfidenceBand Band { get; }
    public IReadOnlyList<string> PositiveFactors { get; }
    public IReadOnlyList<string> NegativeFactors { get; }
    public double DataCoverage { get; }
    public double ContextFreshness { get; }
    public IReadOnlyList<string> Limitations { get; }
    public string Method { get; }

    public static AgentConfidence Unavailable { get; } = new(
        0,
        AgentConfidenceBand.VeryLow,
        limitations: ["Insufficient data for analysis."],
        method: "not-calculated");

    public static AgentConfidence FromScore(
        double score,
        double dataCoverage,
        double contextFreshness,
        IReadOnlyCollection<string>? positiveFactors = null,
        IReadOnlyCollection<string>? negativeFactors = null,
        IReadOnlyCollection<string>? limitations = null,
        string method = "normalized-score") =>
        new(score, BandFor(score), positiveFactors, negativeFactors, dataCoverage, contextFreshness, limitations, method);

    private static AgentConfidenceBand BandFor(double score) => score switch
    {
        < 20 => AgentConfidenceBand.VeryLow,
        < 40 => AgentConfidenceBand.Low,
        < 70 => AgentConfidenceBand.Medium,
        < 90 => AgentConfidenceBand.High,
        _ => AgentConfidenceBand.VeryHigh
    };

    private static void ValidateScore(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Confidence values must be normalized between 0 and 100.");
        }
    }
}

public sealed record AgentObservation
{
    public AgentObservation(
        string type,
        AgentObservationImportance importance,
        string description,
        Instrument? instrument = null,
        Timeframe? timeframe = null,
        DateTimeOffset? intervalStartUtc = null,
        DateTimeOffset? intervalEndUtc = null,
        IReadOnlyCollection<ContextSourceReference>? references = null,
        IReadOnlyCollection<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (intervalEndUtc < intervalStartUtc)
        {
            throw new ArgumentException("Observation interval end cannot precede its start.", nameof(intervalEndUtc));
        }

        Type = type.Trim();
        Importance = importance;
        Description = description.Trim();
        Instrument = instrument;
        Timeframe = timeframe;
        IntervalStartUtc = intervalStartUtc;
        IntervalEndUtc = intervalEndUtc;
        References = AgentCollections.CopyList(references);
        Tags = AgentCollections.CopyStrings(tags, StringComparer.OrdinalIgnoreCase);
    }

    public string Type { get; }
    public AgentObservationImportance Importance { get; }
    public string Description { get; }
    public Instrument? Instrument { get; }
    public Timeframe? Timeframe { get; }
    public DateTimeOffset? IntervalStartUtc { get; }
    public DateTimeOffset? IntervalEndUtc { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
    public IReadOnlyList<string> Tags { get; }
}

public sealed record AgentEvidence
{
    public AgentEvidence(
        ContextProviderCategory sourceCategory,
        ContextSourceReference sourceReference,
        string explanation,
        double weight = 100,
        ContextFreshness freshness = ContextFreshness.Unknown,
        DateTimeOffset? sourceTimestampUtc = null)
    {
        ArgumentNullException.ThrowIfNull(sourceReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        if (weight is < 0 or > 100 || double.IsNaN(weight) || double.IsInfinity(weight))
        {
            throw new ArgumentOutOfRangeException(nameof(weight));
        }

        SourceCategory = sourceCategory;
        SourceReference = sourceReference;
        Explanation = explanation.Trim();
        Weight = weight;
        Freshness = freshness;
        SourceTimestampUtc = sourceTimestampUtc;
    }

    public ContextProviderCategory SourceCategory { get; }
    public ContextSourceReference SourceReference { get; }
    public string Explanation { get; }
    public double Weight { get; }
    public ContextFreshness Freshness { get; }
    public DateTimeOffset? SourceTimestampUtc { get; }
}

public sealed record AgentMarketLevel
{
    public AgentMarketLevel(
        string id,
        AgentMarketLevelType type,
        Price price,
        Timeframe timeframe,
        AgentObservationImportance importance,
        string reason,
        Price? lowerBound = null,
        Price? upperBound = null,
        IReadOnlyCollection<ContextSourceReference>? references = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(timeframe);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (lowerBound is not null && upperBound is not null && upperBound.Value.Value < lowerBound.Value.Value)
        {
            throw new ArgumentException("A market level upper bound cannot be below its lower bound.", nameof(upperBound));
        }

        Id = id.Trim();
        Type = type;
        Price = price;
        Timeframe = timeframe;
        Importance = importance;
        Reason = reason.Trim();
        LowerBound = lowerBound;
        UpperBound = upperBound;
        References = AgentCollections.CopyList(references);
    }

    public string Id { get; }
    public AgentMarketLevelType Type { get; }
    public Price Price { get; }
    public Timeframe Timeframe { get; }
    public AgentObservationImportance Importance { get; }
    public string Reason { get; }
    public Price? LowerBound { get; }
    public Price? UpperBound { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
}

public sealed record AgentScenario
{
    public AgentScenario(
        string id,
        string title,
        string description,
        AgentDirectionalBias direction,
        IReadOnlyCollection<string>? activationConditions = null,
        IReadOnlyCollection<string>? invalidationConditions = null,
        IReadOnlyCollection<string>? levelIds = null,
        IReadOnlyCollection<string>? risks = null,
        TimeSpan? horizon = null,
        AgentConfidence? confidence = null,
        IReadOnlyCollection<ContextSourceReference>? references = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (horizon is { } duration && duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(horizon));
        }

        Id = id.Trim();
        Title = title.Trim();
        Description = description.Trim();
        Direction = direction;
        ActivationConditions = AgentCollections.CopyStrings(activationConditions);
        InvalidationConditions = AgentCollections.CopyStrings(invalidationConditions);
        LevelIds = AgentCollections.CopyStrings(levelIds);
        Risks = AgentCollections.CopyStrings(risks);
        Horizon = horizon;
        Confidence = confidence ?? AgentConfidence.Unavailable;
        References = AgentCollections.CopyList(references);
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
    public AgentConfidence Confidence { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
}

public sealed record AgentWarning
{
    public AgentWarning(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
    }

    public string Code { get; }
    public string Message { get; }
}

public sealed record AgentError
{
    public AgentError(AgentErrorCode code, string message, bool fatal = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message.Trim();
        Fatal = fatal;
    }

    public AgentErrorCode Code { get; }
    public string Message { get; }
    public bool Fatal { get; }
}

public interface IExpertAgent
{
    AgentDescriptor Descriptor { get; }

    Task<AgentAnalysisResult> AnalyzeAsync(
        MarketContext context,
        AgentExecutionRequest request,
        CancellationToken cancellationToken);
}
