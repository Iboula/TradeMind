using System.Globalization;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Coaching.Tests;

public sealed class TradeMetricsCalculatorTests
{
    private readonly TradeMetricsCalculator _calculator = new();

    [Fact]
    public void Case017_CalculatesRiskPercentage()
    {
        Assert.Equal(1m, Calculate().ComputedMetrics[TradeMetricNames.RiskPercentage]);
    }

    [Fact]
    public void Case018_CalculatesRewardToRisk()
    {
        Assert.Equal(2m, Calculate().ComputedMetrics[TradeMetricNames.PlannedRewardToRisk]);
    }

    [Fact]
    public void Case019_CalculatesRMultiple()
    {
        Assert.Equal(2m, Calculate().ComputedMetrics[TradeMetricNames.RealizedRMultiple]);
    }

    [Fact]
    public void Case020_CalculatesDuration()
    {
        Assert.Equal(120m, Calculate().ComputedMetrics[TradeMetricNames.DurationMinutes]);
    }

    [Fact]
    public void Case021_CalculatesStopDistance()
    {
        Assert.Equal(5m, Calculate().ComputedMetrics[TradeMetricNames.StopDistance]);
    }

    [Fact]
    public void Case022_CalculatesTargetDistance()
    {
        Assert.Equal(10m, Calculate().ComputedMetrics[TradeMetricNames.TargetDistance]);
    }

    [Fact]
    public void Case023_CalculatesResultRelativeToBalance()
    {
        Assert.Equal(2m, Calculate().ComputedMetrics[TradeMetricNames.ResultPercentageOfBalance]);
    }

    [Fact]
    public void Case024_CalculatesPlannedActualRiskDelta()
    {
        Assert.Equal(0m, Calculate().ComputedMetrics[TradeMetricNames.PlannedActualRiskDelta]);
    }

    [Fact]
    public void Case025_ReportsUnavailableMetric()
    {
        var metrics = _calculator.Calculate(TradingCoachTestData.Normalize(new TradingJournalAnalysisRequest()));
        Assert.Contains(TradeMetricNames.RiskPercentage, metrics.UnavailableMetrics);
    }

    [Fact]
    public void Case026_DoesNotDivideByZero()
    {
        var metrics = _calculator.Calculate(TradingCoachTestData.Normalize(new TradingJournalAnalysisRequest(
            accountBalance: 0, riskAmount: 100, resultAmount: 10)));
        Assert.Contains(TradeMetricNames.ResultPercentageOfBalance, metrics.UnavailableMetrics);
    }

    [Fact]
    public void Case027_DoesNotReplaceContradictoryExplicitMetric()
    {
        var request = TradingCoachTestData.CompleteRequest(resultAmount: 100, resultRMultiple: 5);
        var metrics = _calculator.Calculate(TradingCoachTestData.Normalize(request));
        Assert.Equal(5m, metrics.ComputedMetrics[TradeMetricNames.RealizedRMultiple]);
        Assert.Contains(metrics.Warnings, warning => warning.Contains("inconsistent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Case028_CalculationsAreCultureInvariant()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(2m, Calculate().ComputedMetrics[TradeMetricNames.PlannedRewardToRisk]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private TradeMetricsResult Calculate() => _calculator.Calculate(TradingCoachTestData.Normalize());
}
