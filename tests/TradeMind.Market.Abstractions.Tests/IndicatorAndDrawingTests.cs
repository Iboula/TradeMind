using TradeMind.Market.Abstractions;

namespace TradeMind.Market.Abstractions.Tests;

public sealed class IndicatorAndDrawingTests
{
    [Fact]
    public void Indicator_CopiesParametersSeriesAndPoints()
    {
        var parameters = new Dictionary<string, string> { ["period"] = "14" };
        var points = new List<IndicatorPoint> { new(MarketTestData.Now, 42m) };
        var series = new List<IndicatorSeries> { new("main", points) };
        var indicator = new ChartIndicator(
            new IndicatorName("Oscillator"),
            new IndicatorInstanceId("instance-1"),
            parameters,
            series);

        parameters["period"] = "20";
        points.Add(new IndicatorPoint(MarketTestData.Now.AddMinutes(1), 43m));
        series.Add(new IndicatorSeries("signal", []));

        Assert.Equal("14", indicator.Parameters["period"]);
        Assert.Single(indicator.Series);
        Assert.Single(indicator.Series[0].Points);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)indicator.Parameters).Add("shift", "1"));
        Assert.Throws<NotSupportedException>(() => ((ICollection<IndicatorSeries>)indicator.Series).Add(new IndicatorSeries("extra", [])));
    }

    [Fact]
    public void Indicator_RequiresAtLeastOneSeries()
    {
        Assert.Throws<ArgumentException>(() => new ChartIndicator(
            new IndicatorName("Oscillator"),
            new IndicatorInstanceId("instance-1"),
            null,
            []));
    }

    [Fact]
    public void IndicatorPoint_AllowsMissingValue()
    {
        Assert.Null(new IndicatorPoint(MarketTestData.Now, null).Value);
    }

    [Fact]
    public void HorizontalAndVerticalLines_ExposeTypedCoordinates()
    {
        var horizontal = new HorizontalLineDrawing(new DrawingId("h-1"), new Price(10m));
        var vertical = new VerticalLineDrawing(new DrawingId("v-1"), MarketTestData.Now);

        Assert.Equal(ChartDrawingType.HorizontalLine, horizontal.Type);
        Assert.Equal(10m, horizontal.Price.Value);
        Assert.Equal(ChartDrawingType.VerticalLine, vertical.Type);
        Assert.Equal(MarketTestData.Now, vertical.Time);
    }

    [Fact]
    public void TrendLine_AcceptsOrderedPeriod()
    {
        var drawing = new TrendLineDrawing(
            new DrawingId("trend-1"),
            MarketTestData.Now,
            new Price(10m),
            MarketTestData.Now.AddHours(1),
            new Price(11m));

        Assert.Equal(ChartDrawingType.TrendLine, drawing.Type);
    }

    [Fact]
    public void TrendLine_RejectsReversedPeriod()
    {
        Assert.Throws<ArgumentException>(() => new TrendLineDrawing(
            new DrawingId("trend-1"),
            MarketTestData.Now,
            new Price(10m),
            MarketTestData.Now.AddTicks(-1),
            new Price(11m)));
    }

    [Fact]
    public void Rectangle_AcceptsOrderedCoordinates()
    {
        var drawing = new RectangleDrawing(
            new DrawingId("rectangle-1"),
            MarketTestData.Now,
            MarketTestData.Now.AddHours(1),
            new Price(9m),
            new Price(11m));

        Assert.Equal(9m, drawing.LowerPrice.Value);
        Assert.Equal(11m, drawing.UpperPrice.Value);
    }

    [Fact]
    public void Rectangle_RejectsUpperPriceBelowLowerPrice()
    {
        Assert.Throws<ArgumentException>(() => new RectangleDrawing(
            new DrawingId("rectangle-1"),
            MarketTestData.Now,
            MarketTestData.Now.AddHours(1),
            new Price(11m),
            new Price(9m)));
    }

    [Fact]
    public void Rectangle_RejectsReversedPeriod()
    {
        Assert.Throws<ArgumentException>(() => new RectangleDrawing(
            new DrawingId("rectangle-1"),
            MarketTestData.Now,
            MarketTestData.Now.AddTicks(-1),
            new Price(9m),
            new Price(11m)));
    }

    [Fact]
    public void TextDrawing_RequiresText()
    {
        Assert.Throws<ArgumentException>(() => new TextDrawing(
            new DrawingId("text-1"),
            MarketTestData.Now,
            null,
            " "));
    }

    [Fact]
    public void RiskRewardBox_AcceptsLongGeometry()
    {
        var drawing = CreateRiskReward(MarketDirection.Long, 10m, 9m, 12m);

        Assert.Equal(MarketDirection.Long, drawing.Direction);
    }

    [Fact]
    public void RiskRewardBox_AcceptsShortGeometry()
    {
        var drawing = CreateRiskReward(MarketDirection.Short, 10m, 11m, 8m);

        Assert.Equal(MarketDirection.Short, drawing.Direction);
    }

    [Theory]
    [InlineData(MarketDirection.Long, 10, 11, 12)]
    [InlineData(MarketDirection.Long, 10, 9, 8)]
    [InlineData(MarketDirection.Short, 10, 9, 8)]
    [InlineData(MarketDirection.Short, 10, 11, 12)]
    public void RiskRewardBox_RejectsIncoherentGeometry(
        MarketDirection direction,
        int entry,
        int stop,
        int target)
    {
        Assert.Throws<ArgumentException>(() => CreateRiskReward(direction, entry, stop, target));
    }

    private static RiskRewardBoxDrawing CreateRiskReward(
        MarketDirection direction,
        decimal entry,
        decimal stop,
        decimal target) => new(
            new DrawingId("risk-reward-1"),
            direction,
            MarketTestData.Now,
            MarketTestData.Now.AddHours(1),
            new Price(entry),
            new Price(stop),
            new Price(target));
}
