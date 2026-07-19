using TradeMind.Market.Abstractions;

namespace TradeMind.Market.Abstractions.Tests;

internal static class MarketTestData
{
    public static readonly DateTimeOffset Now = new(2026, 7, 19, 20, 0, 0, TimeSpan.Zero);

    public static MarketCandle Candle(bool isClosed = true) => new(
        Now.AddMinutes(-1),
        new Price(1.10m),
        new Price(1.12m),
        new Price(1.09m),
        new Price(1.11m),
        100,
        80,
        isClosed);

    public static MarketQuote Quote() => new(
        new Price(1.10m),
        new Price(1.1002m),
        new Price(1.1001m),
        Now);

    public static TradingPosition Position() => new(
        new ExternalPositionId("position-1"),
        new Instrument("EURUSD.a"),
        MarketDirection.Long,
        0.10m,
        new Price(1.10m),
        new Price(1.09m),
        new Price(1.12m),
        Now.AddHours(-1),
        12.50m);

    public static PendingOrder PendingOrder() => new(
        new ExternalOrderId("order-1"),
        new Instrument("EURUSD.a"),
        PendingOrderType.Limit,
        MarketDirection.Long,
        0.10m,
        new Price(1.09m),
        new Price(1.08m),
        new Price(1.12m),
        Now.AddMinutes(-30),
        Now.AddDays(1));

    public static ChartIndicator Indicator() => new(
        new IndicatorName("Moving Average"),
        new IndicatorInstanceId("chart-1-indicator-2"),
        new Dictionary<string, string> { ["period"] = "20" },
        [new IndicatorSeries("main", [new IndicatorPoint(Now, 1.10m)])]);

    public static ChartDrawing Drawing() => new HorizontalLineDrawing(
        new DrawingId("support-1"),
        new Price(1.09m),
        "Support");

    public static SnapshotQuality Quality(
        ConnectorCapabilities missingCapabilities = ConnectorCapabilities.None) => new(
        SnapshotFreshness.Live,
        ConnectorCapabilities.Candles | ConnectorCapabilities.Quotes,
        missingCapabilities);

    public static MarketSnapshot Snapshot(
        IReadOnlyCollection<MarketCandle>? candles = null,
        IReadOnlyCollection<TradingPosition>? positions = null,
        IReadOnlyCollection<PendingOrder>? pendingOrders = null,
        IReadOnlyCollection<ChartIndicator>? indicators = null,
        IReadOnlyCollection<ChartDrawing>? drawings = null,
        SnapshotQuality? quality = null,
        IReadOnlyDictionary<string, string>? metadata = null) => new(
        new SnapshotId(Guid.Parse("60bc62de-25f0-4eef-a6ce-79e06ac7f596")),
        new ConnectorId("sample-connector"),
        new ExternalAccountReference("Account-A/42"),
        new Instrument("EURUSD.a"),
        Timeframe.M15,
        Now,
        Now.AddMilliseconds(25),
        candles ?? [Candle()],
        Quote(),
        positions ?? [Position()],
        pendingOrders ?? [PendingOrder()],
        indicators ?? [Indicator()],
        drawings ?? [Drawing()],
        quality ?? Quality(),
        metadata ?? new Dictionary<string, string> { ["source"] = "test" });

    public static MarketSnapshotRequest Request() => new(
        new ConnectorId("sample-connector"),
        new ExternalAccountReference("Account-A/42"),
        new Instrument("EURUSD.a"),
        Timeframe.M15,
        TimeSpan.FromMinutes(2));
}
