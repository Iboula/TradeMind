using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Coaching.Tests;

public sealed class TradingCoachAnalysisMergerTests
{
    private readonly TradingCoachAnalysisMerger _merger = new();

    [Fact]
    public void Case072_PreservesDeterministicMetrics()
    {
        var metrics = TradingCoachTestData.Metrics();
        Assert.Same(metrics, Merge(metrics: metrics).Metrics);
    }

    [Fact]
    public void Case073_PreservesRuleBasedViolations()
    {
        Assert.Contains(Merge().RuleViolations, finding => finding.Code == "PLAN_MISSING" && finding.IsRuleBased);
    }

    [Fact]
    public void Case074_AddsAiExplanations()
    {
        var ai = AiWithFinding("PLAN_MISSING", "Additional educational context.");
        Assert.Contains("Additional educational context.", Merge(ai: ai).ExecutionObservations);
    }

    [Fact]
    public void Case075_DoesNotAllowAiToRemoveDeterministicViolation()
    {
        var ai = AiWithFinding("PLAN_MISSING", "AI replacement text.");
        var finding = Assert.Single(Merge(ai: ai).RuleViolations, item => item.Code == "PLAN_MISSING");
        Assert.Equal("A plan is missing.", finding.Message);
    }

    [Fact]
    public void Case076_PreservesMissingInformation()
    {
        Assert.Contains(nameof(TradingJournalAnalysisRequest.PlanBeforeTrade), Merge().MissingInformation);
    }

    [Fact]
    public void Case077_LinksRecommendationsToFindings()
    {
        var action = new TradingCoachRecommendedAction("AI_ACTION", "Review the documented plan.", ["PLAN_MISSING"]);
        var result = Merge(ai: TradingCoachTestData.Analysis(actions: [action]));
        Assert.Contains(result.RecommendedActions, item => item.Code == "AI_ACTION" && item.RelatedFindingCodes.Contains("PLAN_MISSING"));
    }

    [Fact]
    public void Case078_RemovesSignalRecommendation()
    {
        var action = new TradingCoachRecommendedAction("AI_ACTION", "Buy now.", ["PLAN_MISSING"]);
        Assert.DoesNotContain(Merge(ai: TradingCoachTestData.Analysis(actions: [action])).RecommendedActions, item => item.Code == "AI_ACTION");
    }

    [Fact]
    public void Case079_RespectsMaximumRecommendations()
    {
        Assert.Single(Merge(options: new TradingCoachExecutionOptions(maximumRecommendations: 1)).RecommendedActions);
    }

    [Fact]
    public void Case080_RespectsMaximumChecklistItems()
    {
        Assert.Single(Merge(options: new TradingCoachExecutionOptions(maximumChecklistItems: 1)).NextTradeChecklist);
    }

    [Fact]
    public void Case081_ProducesDeterministicResult()
    {
        var first = Merge();
        var second = Merge();
        Assert.Equal(first.DataQuality, second.DataQuality);
        Assert.Equal(first.RuleViolations.Select(item => item.Code), second.RuleViolations.Select(item => item.Code));
        Assert.Equal(first.RecommendedActions.Select(item => item.Code), second.RecommendedActions.Select(item => item.Code));
    }

    [Fact]
    public void Case082_DoesNotMutateInputs()
    {
        var ai = TradingCoachTestData.Analysis();
        var rules = Rules();
        var aiCount = ai.RuleViolations.Count;
        var ruleCount = rules.Findings.Count;
        Merge(ai, rules);
        Assert.Equal(aiCount, ai.RuleViolations.Count);
        Assert.Equal(ruleCount, rules.Findings.Count);
    }

    private TradingCoachAnalysis Merge(
        TradingCoachAnalysis? ai = null,
        TradingCoachRuleAnalysisResult? rules = null,
        TradeMetricsResult? metrics = null,
        TradingCoachExecutionOptions? options = null)
    {
        var normalization = TradingCoachTestData.Normalize();
        return _merger.Merge(
            ai ?? TradingCoachTestData.Analysis(),
            rules ?? Rules(),
            normalization,
            metrics ?? new TradeMetricsCalculator().Calculate(normalization),
            options ?? new TradingCoachExecutionOptions());
    }

    private static TradingCoachAnalysis AiWithFinding(string code, string message) =>
        TradingCoachTestData.Analysis(violations:
        [
            new TradingCoachFinding(code, "plan", message, TradingCoachFindingSeverity.Warning, false)
        ]);

    private static TradingCoachRuleAnalysisResult Rules() => new(
        [new TradingCoachFinding(
            "PLAN_MISSING", "plan", "A plan is missing.", TradingCoachFindingSeverity.Warning, true,
            [nameof(TradingJournalAnalysisRequest.PlanBeforeTrade)])],
        [],
        [nameof(TradingJournalAnalysisRequest.PlanBeforeTrade)],
        TradingCoachTestData.Scores(isRuleBased: true));
}
