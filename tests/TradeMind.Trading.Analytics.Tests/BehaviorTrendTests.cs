namespace TradeMind.Trading.Analytics.Tests;

public sealed class BehaviorTrendTests
{
    private readonly TradingBehaviorTrendAnalyzer _analyzer = new();

    [Fact]
    public void Case059_CountsFomo()
    {
        Assert.Equal(2, Trend("FOMO",
            TradingAnalyticsTestData.Trade(0, behavior: "FOMO"),
            TradingAnalyticsTestData.Trade(1, behavior: "fear of missing out")).AffectedTradeCount);
    }

    [Fact]
    public void Case060_CountsRevengeTrading()
    {
        Assert.Single(Trend("REVENGE_TRADING", TradingAnalyticsTestData.Trade(0, behavior: "revenge trading")).RelatedTradeIndexes);
    }

    [Fact]
    public void Case061_CountsOvertrading()
    {
        Assert.Equal(1, Trend("OVERTRADING", TradingAnalyticsTestData.Trade(0, behavior: "overtrading")).OccurrenceCount);
    }

    [Fact]
    public void Case062_CountsMovedStop()
    {
        Assert.Equal(1, Trend("MOVED_STOP", TradingAnalyticsTestData.Trade(0, behavior: "moved stop")).AffectedTradeCount);
    }

    [Fact]
    public void Case063_CalculatesFirstAndLastOccurrence()
    {
        var trend = Trend("FOMO",
            TradingAnalyticsTestData.Trade(0, behavior: "FOMO"),
            TradingAnalyticsTestData.Trade(1),
            TradingAnalyticsTestData.Trade(2, behavior: "FOMO"));
        Assert.Equal(TradingAnalyticsTestData.Now, trend.FirstObservedAtUtc);
        Assert.Equal(TradingAnalyticsTestData.Now.AddDays(2), trend.LastObservedAtUtc);
    }

    [Fact]
    public void Case064_ProducesImproving()
    {
        var trend = Trend("FOMO",
            TradingAnalyticsTestData.Trade(0, behavior: "FOMO"),
            TradingAnalyticsTestData.Trade(1, behavior: "FOMO"),
            TradingAnalyticsTestData.Trade(2),
            TradingAnalyticsTestData.Trade(3));
        Assert.Equal(TradingTrendDirection.Improving, trend.TrendDirection);
    }

    [Fact]
    public void Case065_ProducesWorsening()
    {
        var trend = Trend("FOMO",
            TradingAnalyticsTestData.Trade(0),
            TradingAnalyticsTestData.Trade(1),
            TradingAnalyticsTestData.Trade(2, behavior: "FOMO"),
            TradingAnalyticsTestData.Trade(3, behavior: "FOMO"));
        Assert.Equal(TradingTrendDirection.Worsening, trend.TrendDirection);
    }

    [Fact]
    public void Case066_ProducesStable()
    {
        var trend = Trend("FOMO",
            TradingAnalyticsTestData.Trade(0, behavior: "FOMO"),
            TradingAnalyticsTestData.Trade(1),
            TradingAnalyticsTestData.Trade(2, behavior: "FOMO"),
            TradingAnalyticsTestData.Trade(3));
        Assert.Equal(TradingTrendDirection.Stable, trend.TrendDirection);
    }

    [Fact]
    public void Case067_ProducesInsufficientData()
    {
        IReadOnlyList<TradingJournalTradeAnalysis> trades = [TradingAnalyticsTestData.Analyzed(TradingAnalyticsTestData.Trade(0, behavior: "FOMO"))];
        var trend = _analyzer.Analyze(trades, 5, 9).Single(value => value.Behavior == "FOMO");
        Assert.Equal(TradingTrendDirection.InsufficientData, trend.TrendDirection);
    }

    [Fact]
    public void Case068_ProducesCorrectRelatedTradeIndexes()
    {
        var trend = Trend("FOMO",
            TradingAnalyticsTestData.Trade(0),
            TradingAnalyticsTestData.Trade(1, behavior: "FOMO"),
            TradingAnalyticsTestData.Trade(2));
        Assert.Equal([1], trend.RelatedTradeIndexes);
    }

    [Fact]
    public void Case069_BoundsConfidence()
    {
        var trend = Trend("FOMO", TradingAnalyticsTestData.Trade(0, behavior: "FOMO"));
        Assert.InRange(trend.Confidence, 0m, 1m);
    }

    private TradingBehaviorTrend Trend(
        string behavior,
        params TradeMind.Trading.Coaching.TradingJournalAnalysisRequest[] trades) =>
        _analyzer.Analyze(TradingAnalyticsTestData.Analyzed(trades), Math.Min(3, Math.Max(2, trades.Length)), 9)
            .Single(value => value.Behavior == behavior);
}
