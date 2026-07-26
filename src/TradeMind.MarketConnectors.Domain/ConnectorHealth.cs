using TradeMind.Market.Abstractions;

namespace TradeMind.MarketConnectors.Domain;

public enum ConnectorHealthStatus
{
    Connected,
    Degraded,
    Disconnected,
    NeverConnected
}

public sealed record ConnectorHealthPolicy
{
    public ConnectorHealthPolicy(TimeSpan connectedMaximumAge, TimeSpan degradedMaximumAge)
    {
        if (connectedMaximumAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(connectedMaximumAge),
                "Connected threshold cannot be negative.");
        }

        if (degradedMaximumAge <= connectedMaximumAge)
        {
            throw new ArgumentException(
                "Degraded threshold must be greater than the connected threshold.",
                nameof(degradedMaximumAge));
        }

        ConnectedMaximumAge = connectedMaximumAge;
        DegradedMaximumAge = degradedMaximumAge;
    }

    public TimeSpan ConnectedMaximumAge { get; }

    public TimeSpan DegradedMaximumAge { get; }
}

public sealed record ConnectorHealthSnapshot(
    ConnectorId ConnectorId,
    ConnectorHealthStatus Status,
    DateTimeOffset? LastReceivedAt,
    DateTimeOffset EvaluatedAt,
    TimeSpan? Age);

public static class ConnectorHealthClassifier
{
    public static ConnectorHealthSnapshot Classify(
        ConnectorId connectorId,
        DateTimeOffset? lastReceivedAt,
        DateTimeOffset referenceTime,
        ConnectorHealthPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(connectorId);
        ArgumentNullException.ThrowIfNull(policy);

        if (lastReceivedAt is null)
        {
            return new ConnectorHealthSnapshot(
                connectorId,
                ConnectorHealthStatus.NeverConnected,
                null,
                referenceTime,
                null);
        }

        var age = referenceTime >= lastReceivedAt.Value
            ? referenceTime - lastReceivedAt.Value
            : TimeSpan.Zero;
        var status = age <= policy.ConnectedMaximumAge
            ? ConnectorHealthStatus.Connected
            : age <= policy.DegradedMaximumAge
                ? ConnectorHealthStatus.Degraded
                : ConnectorHealthStatus.Disconnected;

        return new ConnectorHealthSnapshot(
            connectorId,
            status,
            lastReceivedAt,
            referenceTime,
            age);
    }
}
