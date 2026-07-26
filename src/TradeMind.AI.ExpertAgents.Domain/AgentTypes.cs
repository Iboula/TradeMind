using System.Collections.ObjectModel;

namespace TradeMind.AI.ExpertAgents.Domain;

public sealed record AgentRunId
{
    public const int MaximumLength = 128;

    public AgentRunId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > MaximumLength || normalized.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"Agent run id must be {MaximumLength} characters or fewer and contain no whitespace.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public static AgentRunId New() => new($"run-{Guid.NewGuid():N}");

    public override string ToString() => Value;
}

public sealed record AgentSpecialty
{
    public const int MaximumLength = 64;

    public AgentSpecialty(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > MaximumLength || !IsKebabCase(normalized))
        {
            throw new ArgumentException("Agent specialty must use lowercase-kebab-case.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public static AgentSpecialty Ict { get; } = new("ict");
    public static AgentSpecialty Smc { get; } = new("smc");
    public static AgentSpecialty Wyckoff { get; } = new("wyckoff");
    public static AgentSpecialty Macro { get; } = new("macro");
    public static AgentSpecialty Risk { get; } = new("risk");
    public static AgentSpecialty Psychology { get; } = new("psychology");
    public static AgentSpecialty MarketStructure { get; } = new("market-structure");
    public static AgentSpecialty VolumeProfile { get; } = new("volume-profile");
    public static AgentSpecialty OrderFlow { get; } = new("order-flow");
    public static AgentSpecialty Sentiment { get; } = new("sentiment");
    public static AgentSpecialty Custom { get; } = new("custom");

    public override string ToString() => Value;

    private static bool IsKebabCase(string value) =>
        value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')
        && value[0] != '-'
        && value[^1] != '-'
        && !value.Contains("--", StringComparison.Ordinal);
}

public enum AgentActivationStatus
{
    Enabled,
    Disabled
}

public enum AgentMaturity
{
    Experimental,
    Preview,
    Stable
}

public enum AgentExecutionMode
{
    Synchronous
}

public enum AgentAnalysisMode
{
    QuickAssessment,
    StandardAnalysis,
    DeepAnalysis,
    Validation,
    EducationalExplanation
}

public enum AgentAnalysisDepth
{
    Summary,
    Standard,
    Detailed
}

public enum AgentOutputType
{
    StructuredAnalysis,
    Summary,
    Evidence,
    Scenarios
}

public enum AgentAnalysisStatus
{
    Succeeded,
    PartiallySucceeded,
    Failed,
    Incompatible,
    Unauthorized,
    TimedOut,
    Cancelled,
    Unavailable
}

public enum AgentDirectionalBias
{
    Bullish,
    Bearish,
    Neutral,
    Mixed,
    InsufficientData,
    NotApplicable
}

public enum AgentConfidenceBand
{
    VeryLow,
    Low,
    Medium,
    High,
    VeryHigh
}

public enum AgentObservationImportance
{
    Low,
    Medium,
    High,
    Critical
}

public enum AgentMarketLevelType
{
    Support,
    Resistance,
    Liquidity,
    OrderBlock,
    FairValueGap,
    Entry,
    Stop,
    Target,
    Invalidation,
    Custom
}

public enum AgentErrorCode
{
    InvalidRequest,
    AgentNotFound,
    AgentVersionNotFound,
    AgentDisabled,
    UnauthorizedAgent,
    IncompatibleContext,
    MissingRequiredContext,
    UnsupportedInstrument,
    UnsupportedTimeframe,
    UnsupportedContextVersion,
    InsufficientContextQuality,
    StaleContext,
    UnsupportedAnalysisMode,
    AgentTimeout,
    AgentCancelled,
    AgentUnavailable,
    InvalidAgentResult,
    UnexpectedAgentFailure
}

public sealed record AgentOutputOptions
{
    public AgentOutputOptions(
        bool includeEvidence = true,
        bool includeScenarios = true,
        bool includeWarnings = true,
        bool includeLimitations = true)
    {
        IncludeEvidence = includeEvidence;
        IncludeScenarios = includeScenarios;
        IncludeWarnings = includeWarnings;
        IncludeLimitations = includeLimitations;
    }

    public bool IncludeEvidence { get; }
    public bool IncludeScenarios { get; }
    public bool IncludeWarnings { get; }
    public bool IncludeLimitations { get; }
}

internal static class AgentCollections
{
    public static IReadOnlyList<T> CopyList<T>(IEnumerable<T>? values) =>
        Array.AsReadOnly(values?.ToArray() ?? []);

    public static IReadOnlyDictionary<string, string> CopyDictionary(
        IReadOnlyDictionary<string, string>? values,
        StringComparer comparer = null!)
    {
        var dictionary = new Dictionary<string, string>(comparer ?? StringComparer.Ordinal);
        if (values is not null)
        {
            foreach (var (key, value) in values)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(key);
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
                if (!dictionary.TryAdd(key.Trim(), value.Trim()))
                {
                    throw new ArgumentException("Metadata contains duplicate keys.", nameof(values));
                }
            }
        }

        return new ReadOnlyDictionary<string, string>(dictionary);
    }

    public static IReadOnlyList<string> CopyStrings(
        IEnumerable<string>? values,
        StringComparer comparer = null!)
    {
        var materialized = (values ?? []).Select(value =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            return value.Trim();
        }).Distinct(comparer ?? StringComparer.Ordinal).ToArray();
        return Array.AsReadOnly(materialized);
    }
}
