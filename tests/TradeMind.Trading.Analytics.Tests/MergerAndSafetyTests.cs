using Microsoft.Extensions.Options;

namespace TradeMind.Trading.Analytics.Tests;

public sealed class MergerAndSafetyTests
{
    private readonly TradingJournalAnalyticsMerger _merger = new();

    [Fact]
    public void Case119_PreservesAggregates()
    {
        var aggregates = TradingJournalAggregateMetrics.Empty with { TradeCount = 4, TotalResultR = 2 };
        Assert.Equal(aggregates, Merge(TradingAnalyticsTestData.Report(aggregates: aggregates)).AggregateMetrics);
    }

    [Fact]
    public void Case120_PreservesDrawdown()
    {
        var drawdown = new HistoricalRDrawdown(3, 2, -1, 0, 2, null, false);
        Assert.Equal(drawdown, Merge(TradingAnalyticsTestData.Report(drawdown: drawdown)).HistoricalDrawdown);
    }

    [Fact]
    public void Case121_PreservesStreaks()
    {
        var streaks = TradingStreakMetrics.Empty with { LongestLosingStreak = 3 };
        Assert.Equal(streaks, Merge(TradingAnalyticsTestData.Report(streaks: streaks)).Streaks);
    }

    [Fact]
    public void Case122_PreservesTrends()
    {
        var trend = new TradingBehaviorTrend("FOMO", 1, 1, TradingAnalyticsTestData.Now,
            TradingAnalyticsTestData.Now, TradingTrendDirection.Worsening, 1, [0], ["Observed pattern."]);
        Assert.Equal(trend, Assert.Single(Merge(TradingAnalyticsTestData.Report(behaviors: [trend])).BehaviorTrends));
    }

    [Fact]
    public void Case123_PreservesScores()
    {
        var score = new TradingScoreEvolution("PlanAdherence", 50, 70, 20,
            TradingTrendDirection.Improving, [], true);
        Assert.Equal(score, Assert.Single(Merge(TradingAnalyticsTestData.Report(scores: [score])).ScoreEvolution));
    }

    [Fact]
    public void Case124_PreservesRuleBasedFindings()
    {
        Assert.Contains(Merge(TradingAnalyticsTestData.Report()).PriorityIssues, finding => finding.Code == "FINDING");
    }

    [Fact]
    public void Case125_DeduplicatesRecommendations()
    {
        var rules = TradingAnalyticsTestData.Rules();
        var interpretation = TradingAnalyticsTestData.Interpretation(recommendation: "Review the documented process.");
        var result = _merger.Merge(TradingAnalyticsTestData.Report(), rules, interpretation, TradingAnalyticsTestData.Options());
        Assert.Single(result.Recommendations);
    }

    [Fact]
    public void Case126_RespectsCollectionLimits()
    {
        var options = TradingAnalyticsTestData.Options();
        options.MaximumRecommendations = 1;
        options.MaximumChecklistItems = 1;
        var rules = TradingAnalyticsTestData.Rules();
        var interpretation = new TradingJournalAIInterpretation(
            "Descriptive review.", [],
            [new TradingJournalAnalyticsRecommendation("AI", "Review one measured process behavior.", ["FINDING"])],
            ["First item.", "Second item."], TradingJournalAnalyticsConstants.Disclaimer, "en");
        var result = _merger.Merge(TradingAnalyticsTestData.Report(), rules, interpretation, options);
        Assert.Single(result.Recommendations);
        Assert.Single(result.NextReviewChecklist);
    }

    [Fact]
    public void Case127_RejectsSignal()
    {
        var report = TradingAnalyticsTestData.Report(summary: "Buy EURUSD now.");
        Assert.Throws<TradingJournalAnalyticsSafetyException>(() => Safety().Validate(report));
    }

    [Fact]
    public void Case128_RejectsPrediction()
    {
        var report = TradingAnalyticsTestData.Report(summary: "EURUSD will rise tomorrow.");
        Assert.Throws<TradingJournalAnalyticsSafetyException>(() => Safety().Validate(report));
    }

    [Fact]
    public void Case129_DoesNotMutateInputs()
    {
        var report = TradingAnalyticsTestData.Report();
        var originalSummary = report.Summary;
        var originalRecommendations = report.Recommendations.ToArray();
        _ = Merge(report);
        Assert.Equal(originalSummary, report.Summary);
        Assert.Equal(originalRecommendations, report.Recommendations);
    }

    private TradingJournalAnalyticsReport Merge(TradingJournalAnalyticsReport report) =>
        _merger.Merge(report, TradingAnalyticsTestData.Rules(), TradingAnalyticsTestData.Interpretation(), TradingAnalyticsTestData.Options());

    private static TradingJournalAnalyticsSafetyFilter Safety() => new(
        Options.Create(new TradingJournalAnalyticsSafetyOptions()));
}
