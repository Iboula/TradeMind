namespace TradeMind.Trading.Analytics.Tests;

public sealed class PostOutcomeTests
{
    private readonly PostOutcomeBehaviorAnalyzer _analyzer = new(new TradingStatisticsCalculator());

    [Fact]
    public void Case079_ComparesRiskAfterWin()
    {
        var value = Observation("AfterWin",
            TradingAnalyticsTestData.Trade(0, resultR: 1, risk: 1),
            TradingAnalyticsTestData.Trade(1, resultR: -1, risk: 2));
        Assert.Equal(1m, value.AverageNextRiskChange);
    }

    [Fact]
    public void Case080_ComparesRiskAfterLoss()
    {
        var value = Observation("AfterLoss",
            TradingAnalyticsTestData.Trade(0, resultR: -1, risk: 2),
            TradingAnalyticsTestData.Trade(1, resultR: 1, risk: 1));
        Assert.Equal(-1m, value.AverageNextRiskChange);
    }

    [Fact]
    public void Case081_ComparesNextViolation()
    {
        var value = Observation("AfterWin",
            TradingAnalyticsTestData.Trade(0, resultR: 1),
            TradingAnalyticsTestData.Trade(1, resultR: -1, ruleViolation: true));
        Assert.Equal(100m, value.NextRuleViolationRate);
    }

    [Fact]
    public void Case082_ComparesNextJournalQuality()
    {
        var value = Observation("AfterWin",
            TradingAnalyticsTestData.Trade(0, resultR: 1),
            TradingAnalyticsTestData.Trade(1, resultR: -1, complete: false));
        Assert.InRange(value.AverageNextJournalCompleteness!.Value, 0m, 50m);
    }

    [Fact]
    public void Case083_ComparesTimeToNextTrade()
    {
        var value = Observation("AfterWin",
            TradingAnalyticsTestData.Trade(0, resultR: 1),
            TradingAnalyticsTestData.Trade(1, resultR: -1));
        Assert.Equal(TimeSpan.FromHours(23), value.AverageTimeToNextTrade);
    }

    [Fact]
    public void Case084_FlagsInsufficientData()
    {
        var trades = TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0, resultR: 1), TradingAnalyticsTestData.Trade(1, resultR: -1));
        var value = _analyzer.Analyze(trades, 5, "en").Single(item => item.OutcomeContext == "AfterWin");
        Assert.False(value.IsSampleSizeSufficient);
    }

    [Fact]
    public void Case085_UsesObservedAssociationTerm()
    {
        var trades = TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0, resultR: 1), TradingAnalyticsTestData.Trade(1, resultR: -1));
        var value = _analyzer.Analyze(trades, 2, "fr").Single(item => item.OutcomeContext == "AfterWin");
        Assert.Contains("association observ\u00e9e", value.Observation, StringComparison.Ordinal);
    }

    [Fact]
    public void Case086_DoesNotStateCertainCausality()
    {
        var value = Observation("AfterWin",
            TradingAnalyticsTestData.Trade(0, resultR: 1), TradingAnalyticsTestData.Trade(1, resultR: -1));
        Assert.Contains("does not establish causality", value.Observation, StringComparison.OrdinalIgnoreCase);
    }

    private PostOutcomeBehaviorObservation Observation(
        string context,
        params TradeMind.Trading.Coaching.TradingJournalAnalysisRequest[] trades) =>
        _analyzer.Analyze(TradingAnalyticsTestData.Analyzed(trades), 2, "en").Single(value => value.OutcomeContext == context);
}
