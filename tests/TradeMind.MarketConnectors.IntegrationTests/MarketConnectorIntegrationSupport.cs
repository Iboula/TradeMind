using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Application;
using TradeMind.MarketConnectors.Infrastructure;

namespace TradeMind.MarketConnectors.IntegrationTests;

internal static class MarketConnectorIntegrationData
{
    public static readonly DateTimeOffset Now = new(2026, 7, 19, 21, 30, 0, TimeSpan.Zero);

    public static MarketSnapshot Snapshot(
        string connectorId,
        Guid? snapshotId = null,
        string account = "Account-A/42",
        string instrument = "EURUSD.a",
        Timeframe? timeframe = null,
        DateTimeOffset? capturedAt = null,
        DateTimeOffset? receivedAt = null,
        decimal close = 1.11m) => new(
            new SnapshotId(snapshotId ?? Guid.NewGuid()),
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
            new MarketQuote(new Price(1.10m), new Price(1.1002m), new Price(1.1001m), Now),
            [new TradingPosition(
                new ExternalPositionId("Position:A-1"),
                new Instrument(instrument),
                MarketDirection.Long,
                0.1m,
                new Price(1.10m),
                null,
                new Price(1.12m),
                Now.AddHours(-1),
                12.5m)],
            [],
            [new ChartIndicator(
                new IndicatorName("Moving Average"),
                new IndicatorInstanceId("indicator-1"),
                new Dictionary<string, string> { ["period"] = "20" },
                [new IndicatorSeries("main", [new IndicatorPoint(Now, 1.105m)])])],
            [new HorizontalLineDrawing(new DrawingId("support-1"), new Price(1.09m))],
            new SnapshotQuality(
                SnapshotFreshness.Live,
                ConnectorCapabilities.Candles
                    | ConnectorCapabilities.Quotes
                    | ConnectorCapabilities.Positions
                    | ConnectorCapabilities.Indicators
                    | ConnectorCapabilities.Drawings,
                ConnectorCapabilities.PendingOrders,
                [new SnapshotWarning("pending-orders-missing", "Pending orders were not supplied.")]),
            new Dictionary<string, string> { ["feed"] = "integration", ["region"] = "global" });

    public static PublishMarketSnapshotCommandHandler Handler(
        IMarketSnapshotRepository repository,
        DateTimeOffset? now = null) => new(
            repository,
            new CanonicalMarketSnapshotSerializer(),
            new Sha256MarketSnapshotHasher(),
            Options.Create(new MarketConnectorCoreOptions()),
            new FixedIntegrationTimeProvider(now ?? Now),
            NullLogger<PublishMarketSnapshotCommandHandler>.Instance);

    public static GetLatestMarketSnapshotQueryHandler LatestHandler(
        IMarketSnapshotRepository repository,
        DateTimeOffset? now = null) => new(
            repository,
            new CanonicalMarketSnapshotSerializer(),
            Options.Create(new MarketConnectorCoreOptions()),
            new FixedIntegrationTimeProvider(now ?? Now));

    public static MarketSnapshotPersistenceRequest PersistenceRequest(
        MarketSnapshot snapshot,
        int? schemaVersion = null)
    {
        var serializer = new CanonicalMarketSnapshotSerializer();
        var serialized = serializer.Serialize(snapshot);
        var hash = new Sha256MarketSnapshotHasher().ComputeHash(serialized.CanonicalContent);
        return new MarketSnapshotPersistenceRequest(
            snapshot,
            hash,
            serialized.CanonicalPayload,
            schemaVersion ?? serialized.SchemaVersion,
            serialized.SizeInBytes,
            Now);
    }
}

internal sealed class FixedIntegrationTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
