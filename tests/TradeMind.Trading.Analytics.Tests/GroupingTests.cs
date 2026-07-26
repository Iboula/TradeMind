namespace TradeMind.Trading.Analytics.Tests;

public sealed class GroupingTests
{
    private readonly TradingPeriodAggregator _aggregator = new(new TradingStatisticsCalculator());

    [Fact]
    public void Case041_GroupsByDay()
    {
        var periods = Group(TradingAnalyticsGroupingPeriod.Day,
            TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1));
        Assert.Equal(2, periods.Count);
    }

    [Fact]
    public void Case042_GroupsByWeek()
    {
        var periods = Group(TradingAnalyticsGroupingPeriod.Week,
            TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1));
        Assert.Single(periods);
        Assert.Equal(DayOfWeek.Monday, periods[0].PeriodStartUtc.DayOfWeek);
    }

    [Fact]
    public void Case043_GroupsByMonth()
    {
        var periods = Group(TradingAnalyticsGroupingPeriod.Month,
            TradingAnalyticsTestData.Trade(0),
            TradingAnalyticsTestData.Trade(1, openedAtUtc: TradingAnalyticsTestData.Now.AddMonths(1)));
        Assert.Equal(2, periods.Count);
    }

    [Fact]
    public void Case044_UsesUtcBoundaries()
    {
        var period = Assert.Single(Group(TradingAnalyticsGroupingPeriod.Day, TradingAnalyticsTestData.Trade(0)));
        Assert.Equal(TimeSpan.Zero, period.PeriodStartUtc.Offset);
        Assert.Equal(TimeSpan.Zero, period.PeriodEndUtc.Offset);
    }

    [Fact]
    public void Case045_ProducesOrderedPeriods()
    {
        var periods = Group(TradingAnalyticsGroupingPeriod.Day,
            TradingAnalyticsTestData.Trade(2), TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1));
        Assert.Equal(periods.OrderBy(period => period.PeriodStartUtc), periods);
    }

    [Fact]
    public void Case046_RespectsMaximumGroups()
    {
        var options = TradingAnalyticsTestData.Options();
        options.MaximumGroups = 2;
        var periods = _aggregator.Group(TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1), TradingAnalyticsTestData.Trade(2)),
            TradingAnalyticsGroupingPeriod.Day, options);
        Assert.Equal(2, periods.Count);
    }

    [Fact]
    public void Case047_FlagsInsufficientGroups()
    {
        var options = TradingAnalyticsTestData.Options();
        options.MinimumTradesPerGroup = 2;
        var period = Assert.Single(_aggregator.Group(
            [TradingAnalyticsTestData.Analyzed(TradingAnalyticsTestData.Trade(0))],
            TradingAnalyticsGroupingPeriod.Day, options));
        Assert.False(period.IsSampleSizeSufficient);
    }

    [Fact]
    public void Case048_ProducesCorrectPeriodAggregates()
    {
        var period = Assert.Single(Group(TradingAnalyticsGroupingPeriod.Week,
            TradingAnalyticsTestData.Trade(0, resultR: 2), TradingAnalyticsTestData.Trade(1, resultR: -1)));
        Assert.Equal(1m, period.AggregateMetrics.TotalResultR);
        Assert.Equal(2, period.TradeCount);
    }

    private IReadOnlyList<TradingPeriodAggregate> Group(
        TradingAnalyticsGroupingPeriod period,
        params TradeMind.Trading.Coaching.TradingJournalAnalysisRequest[] trades) =>
        _aggregator.Group(TradingAnalyticsTestData.Analyzed(trades), period, TradingAnalyticsTestData.Options());
}
