using TradeMind.Market.Abstractions;

namespace TradeMind.Market.Abstractions.Tests;

public sealed class TradingStateTests
{
    [Fact]
    public void Position_AcceptsProviderAgnosticState()
    {
        var position = MarketTestData.Position();

        Assert.Equal(MarketDirection.Long, position.Direction);
        Assert.Equal(0.10m, position.Volume);
        Assert.Equal(12.50m, position.UnrealizedProfitLoss);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    public void Position_RejectsNonPositiveVolume(string rawVolume)
    {
        var volume = decimal.Parse(rawVolume, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPosition(
            new ExternalPositionId("position-1"),
            new Instrument("EURUSD"),
            MarketDirection.Short,
            volume,
            new Price(1.10m),
            null,
            null,
            MarketTestData.Now,
            null));
    }

    [Fact]
    public void PendingOrder_AcceptsProviderAgnosticState()
    {
        var order = MarketTestData.PendingOrder();

        Assert.Equal(PendingOrderType.Limit, order.Type);
        Assert.Equal(MarketDirection.Long, order.Direction);
        Assert.Equal(MarketTestData.Now.AddDays(1), order.ExpiresAt);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void PendingOrder_RejectsNonPositiveVolume(string rawVolume)
    {
        var volume = decimal.Parse(rawVolume, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingOrder(
            new ExternalOrderId("order-1"),
            new Instrument("EURUSD"),
            PendingOrderType.Stop,
            MarketDirection.Long,
            volume,
            new Price(1.10m),
            null,
            null,
            MarketTestData.Now,
            null));
    }

    [Fact]
    public void PendingOrder_RejectsExpirationBeforeCreation()
    {
        Assert.Throws<ArgumentException>(() => new PendingOrder(
            new ExternalOrderId("order-1"),
            new Instrument("EURUSD"),
            PendingOrderType.StopLimit,
            MarketDirection.Short,
            1m,
            new Price(1.10m),
            null,
            null,
            MarketTestData.Now,
            MarketTestData.Now.AddTicks(-1)));
    }
}
