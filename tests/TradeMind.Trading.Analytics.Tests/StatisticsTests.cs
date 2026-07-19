using System.Globalization;

namespace TradeMind.Trading.Analytics.Tests;

public sealed class StatisticsTests
{
    private readonly TradingStatisticsCalculator _calculator = new();

    [Fact]
    public void Case019_CalculatesMean() => Assert.Equal(2m, _calculator.Mean([1m, 2m, 3m]));

    [Fact]
    public void Case020_CalculatesEvenMedian() => Assert.Equal(2.5m, _calculator.Median([1m, 2m, 3m, 4m]));

    [Fact]
    public void Case021_CalculatesOddMedian() => Assert.Equal(2m, _calculator.Median([3m, 1m, 2m]));

    [Fact]
    public void Case022_CalculatesMinimumAndMaximum()
    {
        Assert.Equal(-2m, _calculator.Minimum([4m, -2m, 3m]));
        Assert.Equal(4m, _calculator.Maximum([4m, -2m, 3m]));
    }

    [Fact]
    public void Case023_CalculatesRate() => Assert.Equal(50m, _calculator.Rate(2, 4));

    [Fact]
    public void Case024_HandlesDivisionByZero() => Assert.Equal(0m, _calculator.Rate(2, 0));

    [Fact]
    public void Case025_CalculatesTotalResultR()
    {
        var metrics = _calculator.CalculateAggregates(TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0, resultR: 2),
            TradingAnalyticsTestData.Trade(1, resultR: -1)));
        Assert.Equal(1m, metrics.TotalResultR);
    }

    [Fact]
    public void Case026_CalculatesAverageRisk()
    {
        var metrics = _calculator.CalculateAggregates(TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0, risk: 1),
            TradingAnalyticsTestData.Trade(1, risk: 3)));
        Assert.Equal(2m, metrics.AverageRiskPercentage);
    }

    [Fact]
    public void Case027_CalculatesPlanAdherenceRate()
    {
        var metrics = _calculator.CalculateAggregates(TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0),
            TradingAnalyticsTestData.Trade(1, behavior: "ignored plan")));
        Assert.Equal(50m, metrics.PlanAdherenceRate);
    }

    [Fact]
    public void Case028_CalculatesStopUsageRate()
    {
        var metrics = _calculator.CalculateAggregates(TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0),
            TradingAnalyticsTestData.Trade(1, complete: false)));
        Assert.Equal(50m, metrics.StopUsageRate);
    }

    [Fact]
    public void Case029_CalculatesCompletenessAverage()
    {
        var trades = TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0),
            TradingAnalyticsTestData.Trade(1, complete: false));
        Assert.Equal(trades.Average(trade => trade.Completeness), _calculator.CalculateAggregates(trades).JournalCompletenessAverage);
    }

    [Fact]
    public void Case030_IsCultureInvariant()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(1.5m, _calculator.Mean([1m, 2m]));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
