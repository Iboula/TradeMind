namespace TradeMind.Market.Abstractions;

public enum SnapshotFreshness
{
    Live,
    Recent,
    Stale,
    Expired
}

public sealed record SnapshotWarning
{
    public SnapshotWarning(string code, string message)
    {
        Code = MarketValueObject.Normalize(code, nameof(code));
        Message = MarketValueObject.Normalize(message, nameof(message));
    }

    public string Code { get; }
    public string Message { get; }
}

public sealed record SnapshotQuality
{
    public SnapshotQuality(
        SnapshotFreshness freshness,
        ConnectorCapabilities availableCapabilities,
        ConnectorCapabilities missingCapabilities,
        IReadOnlyCollection<SnapshotWarning>? warnings = null)
    {
        ConnectorCapabilityValidation.ThrowIfInvalid(availableCapabilities, nameof(availableCapabilities));
        ConnectorCapabilityValidation.ThrowIfInvalid(missingCapabilities, nameof(missingCapabilities));
        if ((availableCapabilities & missingCapabilities) != ConnectorCapabilities.None)
        {
            throw new ArgumentException("Available and missing capabilities cannot overlap.", nameof(missingCapabilities));
        }

        Freshness = freshness;
        AvailableCapabilities = availableCapabilities;
        MissingCapabilities = missingCapabilities;
        Warnings = MarketCollections.CopyList(warnings);
    }

    public SnapshotFreshness Freshness { get; }
    public ConnectorCapabilities AvailableCapabilities { get; }
    public ConnectorCapabilities MissingCapabilities { get; }
    public IReadOnlyList<SnapshotWarning> Warnings { get; }
    public bool IsComplete => MissingCapabilities == ConnectorCapabilities.None;
}

public sealed record SnapshotFreshnessPolicy
{
    public SnapshotFreshnessPolicy(
        TimeSpan liveMaximumAge,
        TimeSpan recentMaximumAge,
        TimeSpan staleMaximumAge)
    {
        if (liveMaximumAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(liveMaximumAge), "Live threshold cannot be negative.");
        }

        if (recentMaximumAge <= liveMaximumAge)
        {
            throw new ArgumentException("Recent threshold must be greater than the live threshold.", nameof(recentMaximumAge));
        }

        if (staleMaximumAge <= recentMaximumAge)
        {
            throw new ArgumentException("Stale threshold must be greater than the recent threshold.", nameof(staleMaximumAge));
        }

        LiveMaximumAge = liveMaximumAge;
        RecentMaximumAge = recentMaximumAge;
        StaleMaximumAge = staleMaximumAge;
    }

    public TimeSpan LiveMaximumAge { get; }
    public TimeSpan RecentMaximumAge { get; }
    public TimeSpan StaleMaximumAge { get; }
}

public static class SnapshotFreshnessClassifier
{
    public static SnapshotFreshness Classify(
        DateTimeOffset capturedAt,
        DateTimeOffset referenceTime,
        SnapshotFreshnessPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (referenceTime < capturedAt)
        {
            throw new ArgumentException("Reference time cannot precede capture time.", nameof(referenceTime));
        }

        var age = referenceTime - capturedAt;
        if (age <= policy.LiveMaximumAge)
        {
            return SnapshotFreshness.Live;
        }

        if (age <= policy.RecentMaximumAge)
        {
            return SnapshotFreshness.Recent;
        }

        return age <= policy.StaleMaximumAge
            ? SnapshotFreshness.Stale
            : SnapshotFreshness.Expired;
    }
}
