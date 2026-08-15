using System.Globalization;
using TradeMind.Market.Abstractions;

namespace TradeMind.Strategies.Domain;

public sealed record StrategyId
{
    public StrategyId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim().ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record StrategyVersion : IComparable<StrategyVersion>
{
    public StrategyVersion(int major, int minor, int patch, bool experimental = false)
    {
        if (major < 0 || minor < 0 || patch < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(major), "Strategy version components cannot be negative.");
        }

        Major = major;
        Minor = minor;
        Patch = patch;
        Experimental = experimental;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public bool Experimental { get; }
    public bool IsStable => !Experimental;

    public int CompareTo(StrategyVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;
        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;
        result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;
        return Experimental.CompareTo(other.Experimental);
    }

    public override string ToString() =>
        $"{Major.ToString(CultureInfo.InvariantCulture)}.{Minor.ToString(CultureInfo.InvariantCulture)}.{Patch.ToString(CultureInfo.InvariantCulture)}" +
        (Experimental ? "-experimental" : string.Empty);
}

public sealed record StrategyCondition
{
    public StrategyCondition(string code, string description, bool required, int weight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (weight is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(weight));
        }

        Code = code.Trim();
        Description = description.Trim();
        Required = required;
        Weight = weight;
    }

    public string Code { get; }
    public string Description { get; }
    public bool Required { get; }
    public int Weight { get; }
}

public sealed record StrategyDefinition
{
    public StrategyDefinition(
        StrategyId id,
        StrategyVersion version,
        string name,
        string description,
        IReadOnlyCollection<StrategyCondition> conditions,
        IReadOnlyCollection<Timeframe> supportedTimeframes,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(supportedTimeframes);
        if (conditions.Count == 0)
        {
            throw new ArgumentException("A strategy definition must declare at least one condition.", nameof(conditions));
        }

        Id = id;
        Version = version;
        Name = name.Trim();
        Description = description.Trim();
        Conditions = Array.AsReadOnly(conditions.OrderBy(item => item.Code, StringComparer.Ordinal).ToArray());
        SupportedTimeframes = Array.AsReadOnly(supportedTimeframes.Distinct().OrderBy(item => item.Code, StringComparer.Ordinal).ToArray());
        Enabled = enabled;
    }

    public StrategyId Id { get; }
    public StrategyVersion Version { get; }
    public string Name { get; }
    public string Description { get; }
    public IReadOnlyList<StrategyCondition> Conditions { get; }
    public IReadOnlyList<Timeframe> SupportedTimeframes { get; }
    public bool Enabled { get; }

    public bool Supports(Timeframe timeframe) =>
        SupportedTimeframes.Count == 0 || SupportedTimeframes.Any(item => item.Code == timeframe.Code);
}

public enum PriceStructureState
{
    Bullish,
    Bearish,
    Range,
    Unknown
}

public enum StrategyTrendDirection
{
    Bullish,
    Bearish,
    Neutral,
    Unknown
}

public enum VolatilityState
{
    Low,
    Normal,
    High,
    Unknown
}

public enum MomentumState
{
    Bullish,
    Bearish,
    Neutral,
    Unknown
}

public sealed record StrategyFeatureSnapshot
{
    public StrategyFeatureSnapshot(
        Price currentPrice,
        PriceStructureState priceStructure,
        StrategyTrendDirection trendDirection,
        VolatilityState volatilityState,
        MomentumState momentumState,
        Price? support,
        Price? resistance,
        decimal averageRange)
    {
        if (currentPrice.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentPrice), "Current price must be greater than zero.");
        }

        if (support is { } supportValue && supportValue.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(support));
        }

        if (resistance is { } resistanceValue && resistanceValue.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resistance));
        }

        if (averageRange < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(averageRange));
        }

        CurrentPrice = currentPrice;
        PriceStructure = priceStructure;
        TrendDirection = trendDirection;
        Volatility = volatilityState;
        Momentum = momentumState;
        Support = support;
        Resistance = resistance;
        AverageRange = averageRange;
    }

    public Price CurrentPrice { get; }
    public PriceStructureState PriceStructure { get; }
    public StrategyTrendDirection TrendDirection { get; }
    public VolatilityState Volatility { get; }
    public MomentumState Momentum { get; }
    public Price? Support { get; }
    public Price? Resistance { get; }
    public decimal AverageRange { get; }
}

public sealed record EntryZone
{
    public EntryZone(Price lowerBound, Price upperBound)
    {
        Validate(lowerBound, upperBound);
        LowerBound = lowerBound;
        UpperBound = upperBound;
    }

    public Price LowerBound { get; }
    public Price UpperBound { get; }
    public Price Midpoint => new((LowerBound.Value + UpperBound.Value) / 2m);

    private static void Validate(Price lowerBound, Price upperBound)
    {
        if (upperBound.Value < lowerBound.Value)
        {
            throw new ArgumentException("Zone upper bound cannot be below its lower bound.", nameof(upperBound));
        }
    }
}

public sealed record TargetZone
{
    public TargetZone(Price lowerBound, Price upperBound)
    {
        if (upperBound.Value < lowerBound.Value)
        {
            throw new ArgumentException("Zone upper bound cannot be below its lower bound.", nameof(upperBound));
        }

        LowerBound = lowerBound;
        UpperBound = upperBound;
    }

    public Price LowerBound { get; }
    public Price UpperBound { get; }
    public Price Midpoint => new((LowerBound.Value + UpperBound.Value) / 2m);
}

public sealed record InvalidationZone
{
    public InvalidationZone(Price lowerBound, Price upperBound)
    {
        if (upperBound.Value < lowerBound.Value)
        {
            throw new ArgumentException("Zone upper bound cannot be below its lower bound.", nameof(upperBound));
        }

        LowerBound = lowerBound;
        UpperBound = upperBound;
    }

    public Price LowerBound { get; }
    public Price UpperBound { get; }
    public Price Midpoint => new((LowerBound.Value + UpperBound.Value) / 2m);
}

public sealed record SetupEvidence
{
    public SetupEvidence(string code, string description, string source, double strength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (strength is < 0 or > 100 || double.IsNaN(strength) || double.IsInfinity(strength))
        {
            throw new ArgumentOutOfRangeException(nameof(strength));
        }

        Code = code.Trim();
        Description = description.Trim();
        Source = source.Trim();
        Strength = strength;
    }

    public string Code { get; }
    public string Description { get; }
    public string Source { get; }
    public double Strength { get; }
}

public enum SetupConfidenceBand
{
    Low,
    Medium,
    High,
    VeryHigh
}

public sealed record SetupConfidence
{
    public SetupConfidence(double score, SetupConfidenceBand band, IReadOnlyCollection<string>? factors = null)
    {
        if (score is < 0 or > 100 || double.IsNaN(score) || double.IsInfinity(score))
        {
            throw new ArgumentOutOfRangeException(nameof(score));
        }

        Score = score;
        Band = band;
        Factors = Array.AsReadOnly((factors ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
    }

    public double Score { get; }
    public SetupConfidenceBand Band { get; }
    public IReadOnlyList<string> Factors { get; }
}

public sealed record SetupCandidate
{
    public SetupCandidate(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        Instrument instrument,
        Timeframe timeframe,
        MarketDirection direction,
        EntryZone entryZone,
        InvalidationZone invalidationZone,
        TargetZone targetZone,
        double riskRewardRatio,
        SetupConfidence confidence,
        IReadOnlyCollection<SetupEvidence> evidence,
        DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(strategyVersion);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(timeframe);
        ArgumentNullException.ThrowIfNull(entryZone);
        ArgumentNullException.ThrowIfNull(invalidationZone);
        ArgumentNullException.ThrowIfNull(targetZone);
        ArgumentNullException.ThrowIfNull(confidence);
        ArgumentNullException.ThrowIfNull(evidence);
        if (riskRewardRatio <= 0 || double.IsNaN(riskRewardRatio) || double.IsInfinity(riskRewardRatio))
        {
            throw new ArgumentOutOfRangeException(nameof(riskRewardRatio));
        }

        if (evidence.Count == 0)
        {
            throw new ArgumentException("A setup candidate must contain evidence.", nameof(evidence));
        }

        if (generatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Generated timestamp must be UTC.", nameof(generatedAtUtc));
        }

        var coherent = direction switch
        {
            MarketDirection.Long => invalidationZone.UpperBound.Value < entryZone.LowerBound.Value
                && targetZone.LowerBound.Value > entryZone.UpperBound.Value,
            MarketDirection.Short => invalidationZone.LowerBound.Value > entryZone.UpperBound.Value
                && targetZone.UpperBound.Value < entryZone.LowerBound.Value,
            _ => false
        };
        if (!coherent)
        {
            throw new ArgumentException("Setup zones are not coherent with the selected direction.", nameof(direction));
        }

        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        Instrument = instrument;
        Timeframe = timeframe;
        Direction = direction;
        EntryZone = entryZone;
        InvalidationZone = invalidationZone;
        TargetZone = targetZone;
        RiskRewardRatio = riskRewardRatio;
        Confidence = confidence;
        Evidence = Array.AsReadOnly(evidence.OrderBy(item => item.Code, StringComparer.Ordinal).ToArray());
        GeneratedAtUtc = generatedAtUtc;
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public MarketDirection Direction { get; }
    public EntryZone EntryZone { get; }
    public InvalidationZone InvalidationZone { get; }
    public TargetZone TargetZone { get; }
    public double RiskRewardRatio { get; }
    public SetupConfidence Confidence { get; }
    public IReadOnlyList<SetupEvidence> Evidence { get; }
    public DateTimeOffset GeneratedAtUtc { get; }
}

public enum StrategyEvaluationStatus
{
    BuyCandidate,
    SellCandidate,
    NoSetup,
    Failed,
    Cancelled
}

public sealed record StrategyEvaluationWarning
{
    public StrategyEvaluationWarning(string code, string message)
    {
        Code = Normalize(code, nameof(code));
        Message = Normalize(message, nameof(message));
    }

    public string Code { get; }
    public string Message { get; }

    private static string Normalize(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }
}

public sealed record StrategyEvaluationError
{
    public StrategyEvaluationError(string code, string message, bool fatal = false)
    {
        Code = Normalize(code, nameof(code));
        Message = Normalize(message, nameof(message));
        Fatal = fatal;
    }

    public string Code { get; }
    public string Message { get; }
    public bool Fatal { get; }

    private static string Normalize(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }
}

public sealed record StrategyEvaluationResult
{
    public StrategyEvaluationResult(
        StrategyDefinition definition,
        StrategyEvaluationStatus status,
        SetupCandidate? candidate,
        IReadOnlyCollection<StrategyEvaluationWarning> warnings,
        IReadOnlyCollection<StrategyEvaluationError> errors,
        DateTimeOffset evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(errors);
        if (evaluatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Evaluation timestamp must be UTC.", nameof(evaluatedAtUtc));
        }

        var candidateStatus = candidate is null
            ? status is StrategyEvaluationStatus.NoSetup or StrategyEvaluationStatus.Failed or StrategyEvaluationStatus.Cancelled
            : status is StrategyEvaluationStatus.BuyCandidate or StrategyEvaluationStatus.SellCandidate;
        if (!candidateStatus)
        {
            throw new ArgumentException("Evaluation status and candidate presence are inconsistent.", nameof(status));
        }

        if (candidate is not null
            && ((status == StrategyEvaluationStatus.BuyCandidate && candidate.Direction != MarketDirection.Long)
                || (status == StrategyEvaluationStatus.SellCandidate && candidate.Direction != MarketDirection.Short)))
        {
            throw new ArgumentException("Candidate direction is inconsistent with the evaluation status.", nameof(candidate));
        }

        Definition = definition;
        Status = status;
        Candidate = candidate;
        Warnings = Array.AsReadOnly(warnings.OrderBy(item => item.Code, StringComparer.Ordinal).ToArray());
        Errors = Array.AsReadOnly(errors.OrderBy(item => item.Code, StringComparer.Ordinal).ToArray());
        EvaluatedAtUtc = evaluatedAtUtc;
    }

    public StrategyDefinition Definition { get; }
    public StrategyEvaluationStatus Status { get; }
    public SetupCandidate? Candidate { get; }
    public IReadOnlyList<StrategyEvaluationWarning> Warnings { get; }
    public IReadOnlyList<StrategyEvaluationError> Errors { get; }
    public DateTimeOffset EvaluatedAtUtc { get; }
}
