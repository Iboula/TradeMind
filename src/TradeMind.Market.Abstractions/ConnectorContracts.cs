namespace TradeMind.Market.Abstractions;

[Flags]
public enum ConnectorCapabilities
{
    None = 0,
    Candles = 1 << 0,
    Quotes = 1 << 1,
    Positions = 1 << 2,
    PendingOrders = 1 << 3,
    Indicators = 1 << 4,
    Drawings = 1 << 5,
    AccountInformation = 1 << 6,
    HistoricalData = 1 << 7,
    Streaming = 1 << 8,
    All = Candles | Quotes | Positions | PendingOrders | Indicators | Drawings
        | AccountInformation | HistoricalData | Streaming
}

public enum ConnectorAcquisitionMode
{
    Push,
    Pull,
    Hybrid
}

public sealed record ConnectorDescriptor
{
    public ConnectorDescriptor(
        ConnectorId id,
        string displayName,
        string version,
        ConnectorCapabilities capabilities,
        ConnectorAcquisitionMode acquisitionMode)
    {
        ArgumentNullException.ThrowIfNull(id);
        ConnectorCapabilityValidation.ThrowIfInvalid(capabilities, nameof(capabilities));
        Id = id;
        DisplayName = MarketValueObject.Normalize(displayName, nameof(displayName));
        Version = MarketValueObject.Normalize(version, nameof(version));
        Capabilities = capabilities;
        AcquisitionMode = acquisitionMode;
    }

    public ConnectorId Id { get; }
    public string DisplayName { get; }
    public string Version { get; }
    public ConnectorCapabilities Capabilities { get; }
    public ConnectorAcquisitionMode AcquisitionMode { get; }
}

public sealed record MarketSnapshotRequest
{
    public MarketSnapshotRequest(
        ConnectorId connectorId,
        ExternalAccountReference? account,
        Instrument instrument,
        Timeframe timeframe,
        TimeSpan? maximumAge = null)
    {
        ArgumentNullException.ThrowIfNull(connectorId);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(timeframe);
        if (maximumAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAge), "Maximum age cannot be negative.");
        }

        ConnectorId = connectorId;
        Account = account;
        Instrument = instrument;
        Timeframe = timeframe;
        MaximumAge = maximumAge;
    }

    public ConnectorId ConnectorId { get; }
    public ExternalAccountReference? Account { get; }
    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public TimeSpan? MaximumAge { get; }
}

public interface IMarketConnector
{
    ConnectorDescriptor Descriptor { get; }

    Task<MarketSnapshot?> GetLatestSnapshotAsync(
        MarketSnapshotRequest request,
        CancellationToken cancellationToken);
}

public interface IMarketConnectorRegistry
{
    bool TryGet(ConnectorId connectorId, out IMarketConnector connector);

    IReadOnlyCollection<ConnectorDescriptor> GetAvailableConnectors();
}

internal static class ConnectorCapabilityValidation
{
    public static void ThrowIfInvalid(ConnectorCapabilities capabilities, string parameterName)
    {
        if ((capabilities & ~ConnectorCapabilities.All) != ConnectorCapabilities.None)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Connector capabilities contain an undefined flag.");
        }
    }
}
