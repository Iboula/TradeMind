using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Coaching.Tests;

public sealed class TradingCoachScoreTests
{
    [Fact]
    public void Case046_ScoresAreBounded()
    {
        var scores = Analyze(new TradingJournalAnalysisRequest()).Scores;
        Assert.All(Values(scores), value => Assert.InRange(value, 0, 100));
    }

    [Fact]
    public void Case047_PlanAdherenceReflectsRules()
    {
        var good = Analyze(TradingCoachTestData.CompleteRequest()).Scores.PlanAdherence;
        var weak = Analyze(new TradingJournalAnalysisRequest(rulesRespected: ["Plan not followed"])).Scores.PlanAdherence;
        Assert.True(weak < good);
    }

    [Fact]
    public void Case048_RiskDisciplineReflectsRisk()
    {
        var good = Analyze(TradingCoachTestData.CompleteRequest()).Scores.RiskDiscipline;
        var weak = Analyze(TradingCoachTestData.CompleteRequest(plannedRiskPercentage: 1, actualRiskPercentage: 2)).Scores.RiskDiscipline;
        Assert.True(weak < good);
    }

    [Fact]
    public void Case049_JournalCompletenessReflectsFields()
    {
        Assert.True(
            Analyze(new TradingJournalAnalysisRequest()).Scores.JournalCompleteness
            < Analyze(TradingCoachTestData.CompleteRequest()).Scores.JournalCompleteness);
    }

    [Fact]
    public void Case050_FinancialResultAloneDoesNotInflateScore()
    {
        var positive = Analyze(new TradingJournalAnalysisRequest(resultAmount: 100)).Scores.OverallProcessQuality;
        var negative = Analyze(new TradingJournalAnalysisRequest(resultAmount: -100)).Scores.OverallProcessQuality;
        Assert.Equal(negative, positive);
    }

    [Fact]
    public void Case051_ScoreExplainsFactors()
    {
        Assert.All(Analyze(new TradingJournalAnalysisRequest()).Scores.Explanations, explanation => Assert.NotEmpty(explanation.Factors));
    }

    [Fact]
    public void Case052_ConfidenceIsValid()
    {
        Assert.All(Analyze(new TradingJournalAnalysisRequest()).Scores.Explanations, explanation => Assert.InRange(explanation.Confidence, 0, 1));
    }

    [Fact]
    public void Case053_IsRuleBasedIsCorrect()
    {
        Assert.All(Analyze(new TradingJournalAnalysisRequest()).Scores.Explanations, explanation => Assert.True(explanation.IsRuleBased));
    }

    [Fact]
    public void Case054_OverallProcessQualityIsDeterministic()
    {
        var scores = Analyze(TradingCoachTestData.CompleteRequest()).Scores;
        var expected = (int)Math.Round((scores.PlanAdherence + scores.RiskDiscipline + scores.ExecutionQuality
            + scores.EmotionalControl + scores.JournalCompleteness) / 5m, MidpointRounding.AwayFromZero);
        Assert.Equal(expected, scores.OverallProcessQuality);
    }

    [Fact]
    public void Case055_ScoresAreImmutable()
    {
        var explanations = new List<TradingCoachScoreExplanation>
        {
            new("PlanAdherence", 80, ["factor"], 1, true)
        };
        var scores = new TradingCoachScores(80, 80, 80, 80, 80, 80, explanations);
        explanations.Clear();
        Assert.Single(scores.Explanations);
    }

    private static TradingCoachRuleAnalysisResult Analyze(TradingJournalAnalysisRequest request)
    {
        var normalization = TradingCoachTestData.Normalize(request);
        var metrics = new TradeMetricsCalculator().Calculate(normalization);
        return new TradingCoachRuleAnalyzer(new TradingBehaviorPatternDetector(), new TradingCoachScoreCalculator())
            .Analyze(normalization, metrics, new TradingCoachProfile());
    }

    private static IEnumerable<int> Values(TradingCoachScores scores)
    {
        yield return scores.PlanAdherence;
        yield return scores.RiskDiscipline;
        yield return scores.ExecutionQuality;
        yield return scores.EmotionalControl;
        yield return scores.JournalCompleteness;
        yield return scores.OverallProcessQuality;
    }
}
