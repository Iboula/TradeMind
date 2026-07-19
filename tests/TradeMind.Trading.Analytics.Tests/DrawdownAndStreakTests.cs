namespace TradeMind.Trading.Analytics.Tests;

public sealed class DrawdownAndStreakTests
{
    private readonly TradingStatisticsCalculator _calculator = new();

    [Fact]
    public void Case031_CalculatesHistoricalDrawdown()
    {
        var value = Drawdown(2, -1, -2, 1);
        Assert.Equal(3m, value.MaximumDrawdownR);
    }

    [Fact]
    public void Case032_IdentifiesPeakAndTrough()
    {
        var value = Drawdown(2, -1, -2);
        Assert.Equal(2m, value.PeakCumulativeR);
        Assert.Equal(-1m, value.TroughCumulativeR);
        Assert.Equal(0, value.StartTradeIndex);
        Assert.Equal(2, value.EndTradeIndex);
    }

    [Fact]
    public void Case033_IdentifiesRecovery()
    {
        var value = Drawdown(2, -1, -1, 2);
        Assert.True(value.IsRecovered);
        Assert.Equal(3, value.RecoveryTradeIndex);
    }

    [Fact]
    public void Case034_HandlesUnrecoveredDrawdown()
    {
        var value = Drawdown(2, -1, -1);
        Assert.False(value.IsRecovered);
        Assert.Null(value.RecoveryTradeIndex);
    }

    [Fact]
    public void Case035_CalculatesWinningStreak()
    {
        var streaks = Streaks(1, 2, -1, 1);
        Assert.Equal(2, streaks.LongestWinningStreak);
    }

    [Fact]
    public void Case036_CalculatesLosingStreak()
    {
        var streaks = Streaks(1, -1, -2, 1);
        Assert.Equal(2, streaks.LongestLosingStreak);
    }

    [Fact]
    public void Case037_CalculatesCurrentStreak()
    {
        var winning = Streaks(-1, 1, 2);
        var losing = Streaks(1, -1, -2);
        Assert.Equal(2, winning.CurrentWinningStreak);
        Assert.Equal(2, losing.CurrentLosingStreak);
    }

    [Fact]
    public void Case038_CalculatesConsecutiveViolations()
    {
        var trades = TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0, ruleViolation: true),
            TradingAnalyticsTestData.Trade(1, ruleViolation: true),
            TradingAnalyticsTestData.Trade(2));
        Assert.Equal(2, _calculator.CalculateStreaks(trades, TradingAnalyticsTestData.Profile(), 50).MaximumConsecutiveRuleViolations);
    }

    [Fact]
    public void Case039_CalculatesConsecutiveOversizedTrades()
    {
        var trades = TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0, risk: 3),
            TradingAnalyticsTestData.Trade(1, risk: 4),
            TradingAnalyticsTestData.Trade(2, risk: 1));
        Assert.Equal(2, _calculator.CalculateStreaks(trades, TradingAnalyticsTestData.Profile(), 50).MaximumConsecutiveOversizedTrades);
    }

    [Fact]
    public void Case040_CalculatesConsecutiveIncompleteJournals()
    {
        var trades = TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0, complete: false),
            TradingAnalyticsTestData.Trade(1, complete: false),
            TradingAnalyticsTestData.Trade(2));
        Assert.Equal(2, _calculator.CalculateStreaks(trades, TradingAnalyticsTestData.Profile(), 50).MaximumConsecutiveIncompleteJournals);
    }

    private HistoricalRDrawdown Drawdown(params decimal[] results) => _calculator.CalculateHistoricalDrawdown(
        TradingAnalyticsTestData.Analyzed(results.Select((result, index) => TradingAnalyticsTestData.Trade(index, resultR: result)).ToArray()));

    private TradingStreakMetrics Streaks(params decimal[] results)
    {
        var trades = TradingAnalyticsTestData.Analyzed(results.Select((result, index) => TradingAnalyticsTestData.Trade(index, resultR: result)).ToArray());
        return _calculator.CalculateStreaks(trades, TradingAnalyticsTestData.Profile(), 50);
    }
}
