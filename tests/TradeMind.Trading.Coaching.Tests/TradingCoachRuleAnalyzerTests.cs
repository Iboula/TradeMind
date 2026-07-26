using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Coaching.Tests;

public sealed class TradingCoachRuleAnalyzerTests
{
    [Fact]
    public void Case029_DetectsExcessiveRisk()
    {
        var request = TradingCoachTestData.CompleteRequest(plannedRiskPercentage: 1, actualRiskPercentage: 2);
        AssertCode(Analyze(request), "ACTUAL_RISK_ABOVE_PLANNED");
    }

    [Fact]
    public void Case030_DetectsMissingStop()
    {
        AssertCode(Analyze(new TradingJournalAnalysisRequest(planBeforeTrade: "plan")), "STOP_MISSING");
    }

    [Fact]
    public void Case031_DetectsMissingPlan()
    {
        AssertCode(Analyze(new TradingJournalAnalysisRequest(stopLoss: 95)), "PLAN_MISSING");
    }

    [Fact]
    public void Case032_DetectsMissingEntryReason()
    {
        AssertCode(Analyze(new TradingJournalAnalysisRequest(stopLoss: 95, planBeforeTrade: "plan")), "ENTRY_REASON_MISSING");
    }

    [Fact]
    public void Case033_DetectsUndocumentedExit()
    {
        AssertCode(Analyze(new TradingJournalAnalysisRequest(exitPrice: 110)), "EXIT_REASON_MISSING");
    }

    [Fact]
    public void Case034_DetectsRulesNotRespected()
    {
        AssertCode(Analyze(new TradingJournalAnalysisRequest(rulesRespected: ["Plan not followed"])), "RULES_NOT_RESPECTED");
    }

    [Fact]
    public void Case035_DetectsIncompleteJournal()
    {
        AssertCode(Analyze(new TradingJournalAnalysisRequest()), "JOURNAL_INCOMPLETE");
    }

    [Fact]
    public void Case036_DetectsFomo()
    {
        AssertCode(Analyze(new TradingJournalAnalysisRequest(executionNotes: "I entered because of FOMO.")), "FOMO");
    }

    [Fact]
    public void Case037_DetectsRevengeTrading()
    {
        AssertCode(Analyze(new TradingJournalAnalysisRequest(executionNotes: "This was revenge trading after a loss.")), "REVENGE_TRADING");
    }

    [Fact]
    public void Case038_DetectsOvertrading()
    {
        AssertCode(Analyze(new TradingJournalAnalysisRequest(executionNotes: "I noticed overtrading.")), "OVERTRADING");
    }

    [Fact]
    public void Case039_DetectsMovedStop()
    {
        AssertCode(Analyze(new TradingJournalAnalysisRequest(executionNotes: "J'ai déplacé le stop.")), "MOVED_STOP");
    }

    [Fact]
    public void Case040_DetectsEarlyExit()
    {
        AssertCode(Analyze(new TradingJournalAnalysisRequest(exitReason: "Sortie trop tôt par hésitation.")), "EARLY_EXIT");
    }

    [Fact]
    public void Case041_DetectsOversizedPosition()
    {
        AssertCode(Analyze(new TradingJournalAnalysisRequest(mistakes: ["Position trop grosse."])), "OVERSIZED_POSITION");
    }

    [Fact]
    public void Case042_DistinguishesPositiveResultAndWeakProcess()
    {
        AssertCode(Analyze(new TradingJournalAnalysisRequest(resultAmount: 100)), "POSITIVE_RESULT_WEAK_PROCESS");
    }

    [Fact]
    public void Case043_DistinguishesNegativeResultAndGoodProcess()
    {
        var request = TradingCoachTestData.CompleteRequest(resultAmount: -100, resultRMultiple: -1);
        AssertCode(Analyze(request), "NEGATIVE_RESULT_GOOD_PROCESS");
    }

    [Fact]
    public void Case044_ProducesDeterministicFindings()
    {
        Assert.All(Analyze(new TradingJournalAnalysisRequest()).Findings, finding => Assert.True(finding.IsRuleBased));
    }

    [Fact]
    public void Case045_DoesNotProduceSignal()
    {
        var text = string.Join(' ', Analyze(new TradingJournalAnalysisRequest()).Findings.Select(finding => finding.Message));
        Assert.DoesNotContain("buy now", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sell now", text, StringComparison.OrdinalIgnoreCase);
    }

    private static TradingCoachRuleAnalysisResult Analyze(
        TradingJournalAnalysisRequest request,
        TradingCoachProfile? profile = null)
    {
        var normalization = TradingCoachTestData.Normalize(request);
        var metrics = new TradeMetricsCalculator().Calculate(normalization);
        return new TradingCoachRuleAnalyzer(
            new TradingBehaviorPatternDetector(),
            new TradingCoachScoreCalculator()).Analyze(
                normalization,
                metrics,
                profile ?? new TradingCoachProfile());
    }

    private static void AssertCode(TradingCoachRuleAnalysisResult result, string code) =>
        Assert.Contains(result.Findings, finding => finding.Code == code);
}
