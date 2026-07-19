using TradeMind.Market.Abstractions;

namespace TradeMind.Market.Abstractions.Tests;

public sealed class MarketDataTests
{
    [Fact]
    public void Candle_AcceptsValidOhlcAndVolumes()
    {
        var candle = MarketTestData.Candle();

        Assert.Equal(1.12m, candle.High.Value);
        Assert.Equal(1.09m, candle.Low.Value);
        Assert.Equal(100, candle.TickVolume);
        Assert.Equal(80, candle.RealVolume);
    }

    [Fact]
    public void Candle_RejectsHighBelowOpenOrClose()
    {
        Assert.Throws<ArgumentException>(() => new MarketCandle(
            MarketTestData.Now,
            new Price(10m),
            new Price(9m),
            new Price(8m),
            new Price(9.5m),
            0,
            null,
            true));
    }

    [Fact]
    public void Candle_RejectsLowAboveOpenOrClose()
    {
        Assert.Throws<ArgumentException>(() => new MarketCandle(
            MarketTestData.Now,
            new Price(10m),
            new Price(12m),
            new Price(11m),
            new Price(11.5m),
            0,
            null,
            true));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Candle_RejectsNegativeVolumes(long tickVolume, long realVolume)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarketCandle(
            MarketTestData.Now,
            new Price(10m),
            new Price(12m),
            new Price(8m),
            new Price(11m),
            tickVolume,
            realVolume,
            true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Candle_ExplicitlyPreservesClosedState(bool isClosed)
    {
        Assert.Equal(isClosed, MarketTestData.Candle(isClosed).IsClosed);
    }

    [Fact]
    public void Quote_CalculatesSpread()
    {
        var quote = MarketTestData.Quote();

        Assert.Equal(0.0002m, quote.Spread.Value);
        Assert.Equal(MarketTestData.Now, quote.Timestamp);
    }

    [Fact]
    public void Quote_RejectsAskBelowBid()
    {
        Assert.Throws<ArgumentException>(() => new MarketQuote(
            new Price(1.20m),
            new Price(1.19m),
            null,
            MarketTestData.Now));
    }

    [Fact]
    public void Quote_AcceptsLockedMarket()
    {
        var quote = new MarketQuote(new Price(10m), new Price(10m), null, MarketTestData.Now);

        Assert.Equal(0m, quote.Spread.Value);
    }
}
