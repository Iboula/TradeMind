using System.Collections.ObjectModel;

namespace TradeMind.AI.Context.Domain;

public enum MarketContextBuildStatus
{
    Succeeded,
    PartiallySucceeded,
    Failed
}

public enum ContextProviderCategory
{
    MarketSnapshot,
    Knowledge,
    Memory,
    TraderProfile,
    Workspace,
    News,
    EconomicCalendar
}

public enum ContextRequirement
{
    Required,
    Preferred,
    Optional
}

public enum ContextProviderExecutionStatus
{
    Succeeded,
    Unavailable,
    NotConfigured,
    Failed,
    TimedOut,
    Skipped
}

public enum ContextFreshness
{
    Fresh,
    Aging,
    Stale,
    Unknown,
    NotApplicable
}

public enum ContextQualityBand
{
    Excellent,
    Good,
    Limited,
    Insufficient
}

public enum ContextBuildErrorCode
{
    ProviderUnavailable,
    ProviderTimeout,
    GlobalTimeout,
    InvalidProviderResult,
    MissingRequiredSource,
    UnsupportedContextVersion,
    InvalidBuildRequest,
    DependencyCycle,
    UnknownDependency
}

public sealed record MarketContextId
{
    public MarketContextId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Market context id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static MarketContextId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record ContextProviderId
{
    public ContextProviderId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim().ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record ContextBuildError(
    ContextBuildErrorCode Code,
    string Message,
    ContextProviderId? ProviderId = null);

public sealed record ContextBuildWarning(
    string Code,
    string Message,
    ContextProviderId? ProviderId = null);

public sealed record ContextFreshnessThresholds
{
    public ContextFreshnessThresholds(TimeSpan freshMaximumAge, TimeSpan agingMaximumAge)
    {
        if (freshMaximumAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(freshMaximumAge));
        }

        if (agingMaximumAge <= freshMaximumAge)
        {
            throw new ArgumentException(
                "Aging threshold must be greater than the fresh threshold.",
                nameof(agingMaximumAge));
        }

        FreshMaximumAge = freshMaximumAge;
        AgingMaximumAge = agingMaximumAge;
    }

    public TimeSpan FreshMaximumAge { get; }

    public TimeSpan AgingMaximumAge { get; }
}

public sealed record ContextFreshnessAssessment(
    ContextFreshness Classification,
    TimeSpan? Age,
    ContextFreshnessThresholds? Thresholds);

public sealed record ContextSourceReference
{
    public ContextSourceReference(string kind, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Kind = kind.Trim();
        Value = value.Trim();
    }

    public string Kind { get; }

    public string Value { get; }
}

public sealed record ContextSourceTrace
{
    public ContextSourceTrace(
        ContextProviderId providerId,
        ContextProviderCategory category,
        ContextRequirement requirement,
        ContextProviderExecutionStatus status,
        DateTimeOffset requestedAtUtc,
        DateTimeOffset completedAtUtc,
        TimeSpan duration,
        DateTimeOffset? sourceTimestampUtc,
        ContextFreshness freshness,
        int itemCount,
        string providerVersion,
        IReadOnlyCollection<ContextSourceReference>? references = null,
        ContextBuildError? error = null,
        TimeSpan? sourceAge = null,
        ContextFreshnessThresholds? freshnessThresholds = null)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerVersion);
        if (completedAtUtc < requestedAtUtc)
        {
            throw new ArgumentException("Completion cannot precede request time.", nameof(completedAtUtc));
        }

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot be negative.");
        }

        if (itemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemCount), "Item count cannot be negative.");
        }

        if (sourceAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceAge), "Source age cannot be negative.");
        }

        ProviderId = providerId;
        Category = category;
        Requirement = requirement;
        Status = status;
        RequestedAtUtc = requestedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Duration = duration;
        SourceTimestampUtc = sourceTimestampUtc;
        Freshness = freshness;
        ItemCount = itemCount;
        ProviderVersion = providerVersion.Trim();
        References = ContextCollections.CopyList(references);
        Error = error;
        SourceAge = sourceAge;
        FreshnessThresholds = freshnessThresholds;
    }

    public ContextProviderId ProviderId { get; }
    public ContextProviderCategory Category { get; }
    public ContextRequirement Requirement { get; }
    public ContextProviderExecutionStatus Status { get; }
    public DateTimeOffset RequestedAtUtc { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public TimeSpan Duration { get; }
    public DateTimeOffset? SourceTimestampUtc { get; }
    public ContextFreshness Freshness { get; }
    public int ItemCount { get; }
    public string ProviderVersion { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
    public ContextBuildError? Error { get; }
    public TimeSpan? SourceAge { get; }
    public ContextFreshnessThresholds? FreshnessThresholds { get; }
}

public sealed record ContextQuality
{
    public ContextQuality(
        double score,
        double completeness,
        double freshness,
        double reliability,
        ContextQualityBand band)
    {
        Score = Validate(score, nameof(score));
        Completeness = Validate(completeness, nameof(completeness));
        Freshness = Validate(freshness, nameof(freshness));
        Reliability = Validate(reliability, nameof(reliability));
        Band = band;
    }

    public double Score { get; }
    public double Completeness { get; }
    public double Freshness { get; }
    public double Reliability { get; }
    public ContextQualityBand Band { get; }

    private static double Validate(double value, string parameterName)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Quality values must be between 0 and 100.");
        }

        return value;
    }
}

internal static class ContextCollections
{
    public static IReadOnlyList<T> CopyList<T>(IEnumerable<T>? values)
    {
        return Array.AsReadOnly(values?.ToArray() ?? []);
    }

    public static IReadOnlyDictionary<string, string> CopyDictionary(
        IReadOnlyDictionary<string, string>? values)
    {
        var copy = values is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
        return new ReadOnlyDictionary<string, string>(copy);
    }
}
