namespace TradeMind.Trading.Analytics.Tests;

public sealed class RiskDriftTests
{
    private readonly RiskDriftAnalyzer _analyzer = new(new TradingStatisticsCalculator());

    [Fact]
    public void Case070_DetectsProgressiveIncrease()
    {
        var value = Analyze(TradingAnalyticsTestData.Profile(5),
            TradingAnalyticsTestData.Trade(0, risk: 1),
            TradingAnalyticsTestData.Trade(1, risk: 1.2m),
            TradingAnalyticsTestData.Trade(2, risk: 1.5m),
            TradingAnalyticsTestData.Trade(3, risk: 1.8m));
        Assert.True(value.DriftDetected);
        Assert.Equal(TradingTrendDirection.Worsening, value.Direction);
    }

    [Fact]
    public void Case071_DetectsDecrease()
    {
        var value = Analyze(TradingAnalyticsTestData.Profile(5),
            TradingAnalyticsTestData.Trade(0, risk: 2),
            TradingAnalyticsTestData.Trade(1, risk: 1.8m),
            TradingAnalyticsTestData.Trade(2, risk: 1.2m),
            TradingAnalyticsTestData.Trade(3, risk: 1));
        Assert.Equal(TradingTrendDirection.Improving, value.Direction);
    }

    [Fact]
    public void Case072_DetectsHigherRiskAfterLoss()
    {
        var value = Analyze(TradingAnalyticsTestData.Profile(5),
            TradingAnalyticsTestData.Trade(0, resultR: -1, risk: 1),
            TradingAnalyticsTestData.Trade(1, risk: 2),
            TradingAnalyticsTestData.Trade(2, risk: 2));
        Assert.Contains(value.Observations, observation => observation.Contains("preceding loss", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Case073_DetectsHigherRiskAfterLossStreak()
    {
        var value = Analyze(TradingAnalyticsTestData.Profile(5),
            TradingAnalyticsTestData.Trade(0, resultR: -1, risk: 1),
            TradingAnalyticsTestData.Trade(1, resultR: -1, risk: 1),
            TradingAnalyticsTestData.Trade(2, risk: 2));
        Assert.Contains(value.Observations, observation => observation.Contains("two preceding losses", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Case074_DetectsRepeatedProfileExceedance()
    {
        var value = Analyze(TradingAnalyticsTestData.Profile(1),
            TradingAnalyticsTestData.Trade(0, risk: 2),
            TradingAnalyticsTestData.Trade(1, risk: 2),
            TradingAnalyticsTestData.Trade(2, risk: 2));
        Assert.Contains(value.Observations, observation => observation.Contains("exceeded repeatedly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Case075_HandlesSmallSample()
    {
        var value = _analyzer.Analyze(
            [TradingAnalyticsTestData.Analyzed(TradingAnalyticsTestData.Trade(0))],
            TradingAnalyticsTestData.Profile(), 5);
        Assert.False(value.IsSampleSizeSufficient);
        Assert.Equal(TradingTrendDirection.InsufficientData, value.Direction);
    }

    [Fact]
    public void Case076_BoundsConfidence()
    {
        Assert.InRange(Analyze(TradingAnalyticsTestData.Profile(),
            TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1), TradingAnalyticsTestData.Trade(2)).Confidence, 0m, 1m);
    }

    [Fact]
    public void Case077_ProducesSupportingTradeIndexes()
    {
        var value = Analyze(TradingAnalyticsTestData.Profile(1),
            TradingAnalyticsTestData.Trade(0, risk: 1),
            TradingAnalyticsTestData.Trade(1, risk: 2),
            TradingAnalyticsTestData.Trade(2, risk: 3));
        Assert.Contains(2, value.SupportingTradeIndexes);
    }

    [Fact]
    public void Case078_DoesNotClaimCausality()
    {
        var value = Analyze(TradingAnalyticsTestData.Profile(5),
            TradingAnalyticsTestData.Trade(0, resultR: -1, risk: 1),
            TradingAnalyticsTestData.Trade(1, risk: 2),
            TradingAnalyticsTestData.Trade(2, risk: 2));
        Assert.DoesNotContain(value.Observations, observation => observation.Contains("causes", StringComparison.OrdinalIgnoreCase));
    }

    private RiskDriftAnalysis Analyze(
        TradeMind.Trading.Coaching.TradingCoachProfile profile,
        params TradeMind.Trading.Coaching.TradingJournalAnalysisRequest[] trades) =>
        _analyzer.Analyze(TradingAnalyticsTestData.Analyzed(trades), profile, Math.Min(3, Math.Max(2, trades.Length)));
}
