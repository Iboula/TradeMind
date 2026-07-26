namespace TradeMind.Trading.Analytics.Tests;

public sealed class SetupAnalyticsTests
{
    private readonly TradingSetupAnalyzer _analyzer = new(new TradingStatisticsCalculator());

    [Fact]
    public void Case049_GroupsBySetup()
    {
        var setups = Analyze(
            TradingAnalyticsTestData.Trade(0, setup: "A"),
            TradingAnalyticsTestData.Trade(1, setup: "B"),
            TradingAnalyticsTestData.Trade(2, setup: "A"));
        Assert.Equal(2, setups.Count);
        Assert.Equal(2, setups[0].TradeCount);
    }

    [Fact]
    public void Case050_NormalizesSetupName()
    {
        var setup = Assert.Single(Analyze(
            TradingAnalyticsTestData.Trade(0, setup: "Breakout Review"),
            TradingAnalyticsTestData.Trade(1, setup: "  breakout   review ")));
        Assert.Equal(2, setup.TradeCount);
    }

    [Fact]
    public void Case051_CalculatesAverageResultR()
    {
        var setup = Assert.Single(Analyze(
            TradingAnalyticsTestData.Trade(0, resultR: 2), TradingAnalyticsTestData.Trade(1, resultR: 0)));
        Assert.Equal(1m, setup.AverageResultR);
    }

    [Fact]
    public void Case052_CalculatesPlanAdherenceRate()
    {
        var setup = Assert.Single(Analyze(
            TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1, behavior: "ignored plan")));
        Assert.Equal(50m, setup.PlanAdherenceRate);
    }

    [Fact]
    public void Case053_CalculatesRuleViolationRate()
    {
        var setup = Assert.Single(Analyze(
            TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1, ruleViolation: true)));
        Assert.Equal(50m, setup.RuleViolationRate);
    }

    [Fact]
    public void Case054_CalculatesAverageRiskPercentage()
    {
        var setup = Assert.Single(Analyze(
            TradingAnalyticsTestData.Trade(0, risk: 1), TradingAnalyticsTestData.Trade(1, risk: 3)));
        Assert.Equal(2m, setup.AverageRiskPercentage);
    }

    [Fact]
    public void Case055_ProducesCommonMistakes()
    {
        var setup = Assert.Single(Analyze(
            TradingAnalyticsTestData.Trade(0, behavior: "moved stop"),
            TradingAnalyticsTestData.Trade(1, behavior: "moved stop")));
        Assert.Contains("moved stop", setup.CommonMistakes, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Case056_FlagsInsufficientSample()
    {
        var setup = Assert.Single(Analyze(TradingAnalyticsTestData.Trade(0)));
        Assert.False(setup.IsSampleSizeSufficient);
        Assert.NotEmpty(setup.Warnings);
    }

    [Fact]
    public void Case057_RespectsMaximumSetups()
    {
        var options = TradingAnalyticsTestData.Options();
        options.MaximumSetups = 1;
        var setups = _analyzer.Analyze(TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0, setup: "A"), TradingAnalyticsTestData.Trade(1, setup: "B")), options);
        Assert.Single(setups);
    }

    [Fact]
    public void Case058_UsesDeterministicOrder()
    {
        var trades = TradingAnalyticsTestData.Analyzed(
            TradingAnalyticsTestData.Trade(0, setup: "B"), TradingAnalyticsTestData.Trade(1, setup: "A"));
        var first = _analyzer.Analyze(trades, TradingAnalyticsTestData.Options()).Select(setup => setup.SetupName).ToArray();
        var second = _analyzer.Analyze(trades, TradingAnalyticsTestData.Options()).Select(setup => setup.SetupName).ToArray();
        Assert.Equal(first, second);
    }

    private IReadOnlyList<TradingSetupAnalytics> Analyze(params TradeMind.Trading.Coaching.TradingJournalAnalysisRequest[] trades) =>
        _analyzer.Analyze(TradingAnalyticsTestData.Analyzed(trades), TradingAnalyticsTestData.Options());
}
