using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics.Tests;

public sealed class RuleAnalyzerTests
{
    private readonly TradingJournalRuleAnalyzer _analyzer = new();

    [Fact]
    public void Case095_DetectsHighAverageRisk()
    {
        var result = Analyze(aggregates: TradingJournalAggregateMetrics.Empty with { AverageRiskPercentage = 3m });
        Assert.Contains(result.Findings, finding => finding.Code == "HIGH_AVERAGE_RISK");
    }

    [Fact]
    public void Case096_DetectsRepeatedExceedance()
    {
        var drift = new RiskDriftAnalysis(true, TradingTrendDirection.Worsening, TradingAnalyticsSeverity.Critical,
            [1, 2], 1, 3, ["The supplied coaching-profile risk maximum was exceeded repeatedly."], 1, true);
        Assert.Contains(Analyze(riskDrift: drift).Findings, finding => finding.Code == "REPEATED_RISK_EXCEEDANCE");
    }

    [Fact]
    public void Case097_DetectsDecliningDiscipline()
    {
        Assert.Contains(Analyze(scores: [Evolution("PlanAdherence", TradingTrendDirection.Worsening)]).Findings,
            finding => finding.Code == "DECLINING_DISCIPLINE");
    }

    [Fact]
    public void Case098_DetectsGrowingViolations()
    {
        var first = Period(TradingJournalAggregateMetrics.Empty with { TradeCount = 10, RuleViolationTradeCount = 0 }, 0);
        var second = Period(TradingJournalAggregateMetrics.Empty with { TradeCount = 10, RuleViolationTradeCount = 5 }, 1);
        Assert.Contains(Analyze(periods: [first, second]).Findings, finding => finding.Code == "GROWING_RULE_VIOLATIONS");
    }

    [Fact]
    public void Case099_DetectsPoorJournaling()
    {
        Assert.Contains(Analyze(dataQuality: TradingAnalyticsTestData.DataQuality(completeness: 40)).Findings,
            finding => finding.Code == "WEAK_JOURNALING");
    }

    [Fact]
    public void Case100_DetectsHigherRiskAfterLosses()
    {
        var streaks = TradingStreakMetrics.Empty with { LongestLosingStreak = 2 };
        var drift = TradingAnalyticsTestData.RiskDrift(true, TradingTrendDirection.Worsening);
        Assert.Contains(Analyze(streaks: streaks, riskDrift: drift).Findings,
            finding => finding.Code == "LOSS_STREAK_RISK_INCREASE");
    }

    [Fact]
    public void Case101_DetectsPositiveResultWithWeakProcess()
    {
        var aggregates = TradingJournalAggregateMetrics.Empty with { NegativeProcessPositiveOutcomeCount = 1 };
        Assert.Contains(Analyze(aggregates: aggregates).Findings, finding => finding.Code == "POSITIVE_RESULT_WEAK_PROCESS");
    }

    [Fact]
    public void Case102_DetectsNegativeResultWithImprovingProcess()
    {
        var aggregates = TradingJournalAggregateMetrics.Empty with { TotalResultR = -2 };
        Assert.Contains(Analyze(aggregates: aggregates, scores: [Evolution("OverallProcessQuality", TradingTrendDirection.Improving)]).Findings,
            finding => finding.Code == "NEGATIVE_RESULT_IMPROVING_PROCESS");
    }

    [Fact]
    public void Case103_DetectsRecurringBehavior()
    {
        var behavior = new TradingBehaviorTrend("FOMO", 2, 2, TradingAnalyticsTestData.Now,
            TradingAnalyticsTestData.Now.AddDays(1), TradingTrendDirection.Stable, 0.8m, [0, 1], ["Observed pattern."]);
        Assert.Contains(Analyze(behaviors: [behavior]).Findings, finding => finding.Code == "RECURRING_BEHAVIOR");
    }

    [Fact]
    public void Case104_DetectsObservableImprovement()
    {
        Assert.Contains(Analyze(scores: [Evolution("OverallProcessQuality", TradingTrendDirection.Improving)]).Findings,
            finding => finding.Code == "OBSERVABLE_IMPROVEMENT");
    }

    [Fact]
    public void Case105_DoesNotProduceSignal()
    {
        var result = Analyze(aggregates: TradingJournalAggregateMetrics.Empty with { AverageRiskPercentage = 3m });
        var text = result.Findings.Select(finding => finding.Message)
            .Concat(result.Recommendations.Select(recommendation => recommendation.Text));
        Assert.DoesNotContain(text, TradingCoachSafetyPolicy.ContainsProhibitedContent);
    }

    private TradingJournalRuleAnalysisResult Analyze(
        TradingJournalAggregateMetrics? aggregates = null,
        TradingStreakMetrics? streaks = null,
        IReadOnlyList<TradingPeriodAggregate>? periods = null,
        IReadOnlyList<TradingSetupAnalytics>? setups = null,
        IReadOnlyList<TradingBehaviorTrend>? behaviors = null,
        RiskDriftAnalysis? riskDrift = null,
        IReadOnlyList<TradingScoreEvolution>? scores = null,
        TradingJournalDataQuality? dataQuality = null) => _analyzer.Analyze(
            aggregates ?? TradingJournalAggregateMetrics.Empty,
            streaks ?? TradingStreakMetrics.Empty,
            periods ?? [],
            setups ?? [],
            behaviors ?? [],
            riskDrift ?? TradingAnalyticsTestData.RiskDrift(),
            scores ?? [],
            dataQuality ?? TradingAnalyticsTestData.DataQuality(),
            TradingAnalyticsTestData.Profile());

    private static TradingScoreEvolution Evolution(string name, TradingTrendDirection direction) => new(
        name, 50, direction == TradingTrendDirection.Improving ? 70 : 30,
        direction == TradingTrendDirection.Improving ? 20 : -20, direction, [], true);

    private static TradingPeriodAggregate Period(TradingJournalAggregateMetrics metrics, int index) => new(
        TradingAnalyticsTestData.Now.AddDays(index * 7), TradingAnalyticsTestData.Now.AddDays((index + 1) * 7),
        metrics.TradeCount, metrics, TradingCoachScores.Empty, new Dictionary<string, int>(), 90, true);
}
