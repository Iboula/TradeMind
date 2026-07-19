namespace TradeMind.Trading.Analytics.Tests;

public sealed class ScoreEvolutionTests
{
    private readonly TradingStatisticsCalculator _calculator = new();

    [Fact]
    public void Case087_CalculatesFirstAverage()
    {
        var trades = EvolutionTrades();
        var value = Evolution(trades, 3, "PlanAdherence");
        Assert.Equal<decimal?>((decimal)trades.Take(2).Average(trade => trade.Scores.PlanAdherence), value.FirstAverage);
    }

    [Fact]
    public void Case088_CalculatesRecentAverage()
    {
        var trades = EvolutionTrades();
        var value = Evolution(trades, 3, "PlanAdherence");
        Assert.Equal<decimal?>((decimal)trades.TakeLast(2).Average(trade => trade.Scores.PlanAdherence), value.RecentAverage);
    }

    [Fact]
    public void Case089_CalculatesChange()
    {
        var value = Evolution(EvolutionTrades(), 3, "PlanAdherence");
        Assert.Equal(value.RecentAverage - value.FirstAverage, value.Change);
    }

    [Fact]
    public void Case090_ProducesImproving()
    {
        var value = Evolution(TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0, behavior: "ignored plan"),
            TradingAnalyticsTestData.Trade(1, behavior: "ignored plan"),
            TradingAnalyticsTestData.Trade(2),
            TradingAnalyticsTestData.Trade(3)), 3, "PlanAdherence");
        Assert.Equal(TradingTrendDirection.Improving, value.TrendDirection);
    }

    [Fact]
    public void Case091_ProducesWorsening()
    {
        var value = Evolution(TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0),
            TradingAnalyticsTestData.Trade(1),
            TradingAnalyticsTestData.Trade(2, behavior: "ignored plan"),
            TradingAnalyticsTestData.Trade(3, behavior: "ignored plan")), 3, "PlanAdherence");
        Assert.Equal(TradingTrendDirection.Worsening, value.TrendDirection);
    }

    [Fact]
    public void Case092_ProducesStable()
    {
        var value = Evolution(TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1),
            TradingAnalyticsTestData.Trade(2), TradingAnalyticsTestData.Trade(3)), 3, "PlanAdherence");
        Assert.Equal(TradingTrendDirection.Stable, value.TrendDirection);
    }

    [Fact]
    public void Case093_ProducesInsufficientData()
    {
        var value = Evolution(TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1)), 5, "PlanAdherence");
        Assert.Equal(TradingTrendDirection.InsufficientData, value.TrendDirection);
        Assert.False(value.IsSampleSizeSufficient);
    }

    [Fact]
    public void Case094_OrdersPeriodValues()
    {
        var trades = EvolutionTrades();
        var options = TradingAnalyticsTestData.Options();
        var periods = new TradingPeriodAggregator(_calculator).Group(trades, TradingAnalyticsGroupingPeriod.Day, options);
        var value = _calculator.CalculateScoreEvolution(trades, periods, 3).Single(item => item.ScoreName == "PlanAdherence");
        Assert.Equal(value.PeriodValues.OrderBy(item => item.PeriodStartUtc), value.PeriodValues);
    }

    private static IReadOnlyList<TradingJournalTradeAnalysis> EvolutionTrades() => TradingAnalyticsTestData.Analyzed(
        TradingAnalyticsTestData.Trade(0, behavior: "ignored plan"),
        TradingAnalyticsTestData.Trade(1, behavior: "ignored plan"),
        TradingAnalyticsTestData.Trade(2),
        TradingAnalyticsTestData.Trade(3));

    private TradingScoreEvolution Evolution(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        int minimum,
        string scoreName) => _calculator.CalculateScoreEvolution(trades, [], minimum).Single(value => value.ScoreName == scoreName);
}
