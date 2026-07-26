namespace TradeMind.Market.Abstractions;

public sealed record MarketSnapshot
{
    public MarketSnapshot(
        SnapshotId id,
        ConnectorId connectorId,
        ExternalAccountReference account,
        Instrument instrument,
        Timeframe timeframe,
        DateTimeOffset capturedAt,
        DateTimeOffset receivedAt,
        IReadOnlyCollection<MarketCandle>? candles,
        MarketQuote? quote,
        IReadOnlyCollection<TradingPosition>? positions,
        IReadOnlyCollection<PendingOrder>? pendingOrders,
        IReadOnlyCollection<ChartIndicator>? indicators,
        IReadOnlyCollection<ChartDrawing>? drawings,
        SnapshotQuality quality,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(connectorId);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(timeframe);
        ArgumentNullException.ThrowIfNull(quality);

        Id = id;
        ConnectorId = connectorId;
        Account = account;
        Instrument = instrument;
        Timeframe = timeframe;
        CapturedAt = capturedAt;
        ReceivedAt = receivedAt;
        Candles = MarketCollections.CopyList(candles);
        Quote = quote;
        Positions = MarketCollections.CopyList(positions);
        PendingOrders = MarketCollections.CopyList(pendingOrders);
        Indicators = MarketCollections.CopyList(indicators);
        Drawings = MarketCollections.CopyList(drawings);
        Quality = quality;
        Metadata = MarketCollections.CopyDictionary(metadata);
    }

    public SnapshotId Id { get; }
    public ConnectorId ConnectorId { get; }
    public ExternalAccountReference Account { get; }
    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public DateTimeOffset CapturedAt { get; }
    public DateTimeOffset ReceivedAt { get; }
    public IReadOnlyList<MarketCandle> Candles { get; }
    public MarketQuote? Quote { get; }
    public IReadOnlyList<TradingPosition> Positions { get; }
    public IReadOnlyList<PendingOrder> PendingOrders { get; }
    public IReadOnlyList<ChartIndicator> Indicators { get; }
    public IReadOnlyList<ChartDrawing> Drawings { get; }
    public SnapshotQuality Quality { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
