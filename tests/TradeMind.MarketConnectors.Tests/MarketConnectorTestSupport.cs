using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Application;
using TradeMind.MarketConnectors.Infrastructure;

namespace TradeMind.MarketConnectors.Tests;

internal static class MarketConnectorTestData
{
    public static readonly DateTimeOffset Now = new(2026, 7, 19, 21, 0, 0, TimeSpan.Zero);

    public static MarketSnapshot Snapshot(
        Guid? id = null,
        string connectorId = "sample-connector",
        string account = "Account-A/42",
        string instrument = "EURUSD.a",
        Timeframe? timeframe = null,
        DateTimeOffset? capturedAt = null,
        DateTimeOffset? receivedAt = null,
        decimal close = 1.11m,
        IReadOnlyDictionary<string, string>? metadata = null,
        IReadOnlyDictionary<string, string>? indicatorParameters = null) => new(
            new SnapshotId(id ?? Guid.Parse("9fab4d93-c9b3-4d31-8158-7b30e8058174")),
            new ConnectorId(connectorId),
            new ExternalAccountReference(account),
            new Instrument(instrument),
            timeframe ?? Timeframe.M15,
            capturedAt ?? Now.AddSeconds(-3),
            receivedAt ?? Now.AddSeconds(-2),
            [new MarketCandle(
                Now.AddMinutes(-15),
                new Price(1.10m),
                new Price(1.12m),
                new Price(1.09m),
                new Price(close),
                100,
                80,
                true)],
            new MarketQuote(new Price(1.10m), new Price(1.1002m), new Price(1.1001m), Now.AddSeconds(-2)),
            [new TradingPosition(
                new ExternalPositionId("Position:A-1"),
                new Instrument(instrument),
                MarketDirection.Long,
                0.1m,
                new Price(1.10m),
                new Price(1.09m),
                new Price(1.12m),
                Now.AddHours(-1),
                12.5m)],
            [new PendingOrder(
                new ExternalOrderId("Order:A-1"),
                new Instrument(instrument),
                PendingOrderType.Limit,
                MarketDirection.Long,
                0.1m,
                new Price(1.09m),
                new Price(1.08m),
                new Price(1.12m),
                Now.AddMinutes(-30),
                Now.AddDays(1))],
            [new ChartIndicator(
                new IndicatorName("Moving Average"),
                new IndicatorInstanceId("indicator-1"),
                indicatorParameters ?? new Dictionary<string, string> { ["period"] = "20", ["shift"] = "0" },
                [new IndicatorSeries("main", [new IndicatorPoint(Now.AddMinutes(-1), 1.105m)])])],
            Drawings(),
            new SnapshotQuality(
                SnapshotFreshness.Live,
                ConnectorCapabilities.Candles
                    | ConnectorCapabilities.Quotes
                    | ConnectorCapabilities.Positions
                    | ConnectorCapabilities.PendingOrders
                    | ConnectorCapabilities.Indicators
                    | ConnectorCapabilities.Drawings,
                ConnectorCapabilities.None,
                [new SnapshotWarning("complete", "All requested data is available.")]),
            metadata ?? new Dictionary<string, string> { ["feed"] = "demo", ["region"] = "global" });

    public static SerializedMarketSnapshot Serialize(MarketSnapshot snapshot) =>
        new CanonicalMarketSnapshotSerializer().Serialize(snapshot);

    public static PublishMarketSnapshotCommandHandler IngestionHandler(
        FakeMarketSnapshotRepository repository,
        MarketConnectorCoreOptions? options = null,
        IMarketSnapshotSerializer? serializer = null,
        TimeProvider? timeProvider = null) => new(
            repository,
            serializer ?? new CanonicalMarketSnapshotSerializer(),
            new Sha256MarketSnapshotHasher(),
            Options.Create(options ?? new MarketConnectorCoreOptions()),
            timeProvider ?? new FixedTimeProvider(Now),
            NullLogger<PublishMarketSnapshotCommandHandler>.Instance);

    private static IReadOnlyCollection<ChartDrawing> Drawings() =>
    [
        new HorizontalLineDrawing(new DrawingId("horizontal-1"), new Price(1.09m), "Support"),
        new VerticalLineDrawing(new DrawingId("vertical-1"), Now.AddHours(-2)),
        new TrendLineDrawing(
            new DrawingId("trend-1"),
            Now.AddHours(-2),
            new Price(1.08m),
            Now,
            new Price(1.11m)),
        new RectangleDrawing(
            new DrawingId("rectangle-1"),
            Now.AddHours(-2),
            Now,
            new Price(1.09m),
            new Price(1.12m)),
        new TextDrawing(new DrawingId("text-1"), Now, new Price(1.11m), "Breakout"),
        new RiskRewardBoxDrawing(
            new DrawingId("risk-1"),
            MarketDirection.Long,
            Now.AddHours(-1),
            Now,
            new Price(1.10m),
            new Price(1.09m),
            new Price(1.12m))
    ];
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

internal sealed class FakeMarketSnapshotRepository : IMarketSnapshotRepository
{
    public StoredMarketSnapshotIdentity? Existing { get; set; }
    public MarketSnapshotAtomicInsertResult? Insertion { get; set; }
    public StoredMarketSnapshotPayload? Latest { get; set; }
    public DateTimeOffset? LastReceivedAt { get; set; }
    public MarketSnapshotLookup? LastLookup { get; private set; }
    public MarketSnapshotPersistenceRequest? LastInsertionRequest { get; private set; }

    public Task<StoredMarketSnapshotIdentity?> FindByKeyAsync(
        ConnectorId connectorId,
        SnapshotId snapshotId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Existing);
    }

    public Task<MarketSnapshotAtomicInsertResult> InsertAsync(
        MarketSnapshotPersistenceRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastInsertionRequest = request;
        return Task.FromResult(Insertion ?? new MarketSnapshotAtomicInsertResult(
            MarketSnapshotAtomicInsertStatus.Inserted,
            new StoredMarketSnapshotIdentity(
                request.Snapshot.Id,
                request.Snapshot.ConnectorId,
                request.ContentHash,
                request.Snapshot.ReceivedAt)));
    }

    public Task<StoredMarketSnapshotPayload?> GetLatestAsync(
        MarketSnapshotLookup lookup,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastLookup = lookup;
        return Task.FromResult(Latest);
    }

    public Task<DateTimeOffset?> GetLatestReceivedAtAsync(
        ConnectorId connectorId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LastReceivedAt);
    }
}

internal sealed class FakeConnector(string id) : IMarketConnector
{
    public ConnectorDescriptor Descriptor { get; } = new(
        new ConnectorId(id),
        $"Connector {id}",
        "1.0.0",
        ConnectorCapabilities.Candles,
        ConnectorAcquisitionMode.Pull);

    public Task<MarketSnapshot?> GetLatestSnapshotAsync(
        MarketSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<MarketSnapshot?>(null);
    }
}
