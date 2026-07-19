namespace TradeMind.Trading.Coaching;

public sealed class TradingCoachScoreCalculator : ITradingCoachScoreCalculator
{
    public TradingCoachScores Calculate(
        TradingJournalNormalizationResult normalizationResult,
        TradeMetricsResult metrics,
        IReadOnlyCollection<TradingCoachFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(normalizationResult);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(findings);

        var codes = findings.Select(finding => finding.Code).ToHashSet(StringComparer.Ordinal);
        var plan = Score(100, codes,
            ("PLAN_MISSING", 35), ("RULES_NOT_RESPECTED", 35), ("IGNORED_PLAN", 30));
        var risk = Score(100, codes,
            ("ACTUAL_RISK_ABOVE_PLANNED", 35), ("RISK_ABOVE_PROFILE_MAXIMUM", 45),
            ("STOP_MISSING", 20), ("OVERSIZED_POSITION", 30));
        var execution = Score(100, codes,
            ("ENTRY_REASON_MISSING", 20), ("EXIT_REASON_MISSING", 15), ("MOVED_STOP", 30),
            ("EARLY_EXIT", 20), ("IMPULSIVE_ENTRY", 25), ("HESITATION", 10));
        var emotional = Score(100, codes,
            ("STRONG_EMOTIONS", 30), ("FOMO", 35), ("REVENGE_TRADING", 45),
            ("OVERTRADING", 25), ("HESITATION", 10));
        var journal = Math.Clamp((int)Math.Round(normalizationResult.CompletenessScore, MidpointRounding.AwayFromZero), 0, 100);
        var overall = (int)Math.Round((plan + risk + execution + emotional + journal) / 5m, MidpointRounding.AwayFromZero);

        var explanations = new[]
        {
            Explain("PlanAdherence", plan, codes, ["PLAN_MISSING", "RULES_NOT_RESPECTED", "IGNORED_PLAN"], 0.95m),
            Explain("RiskDiscipline", risk, codes, ["ACTUAL_RISK_ABOVE_PLANNED", "RISK_ABOVE_PROFILE_MAXIMUM", "STOP_MISSING", "OVERSIZED_POSITION"], 0.95m),
            Explain("ExecutionQuality", execution, codes, ["ENTRY_REASON_MISSING", "EXIT_REASON_MISSING", "MOVED_STOP", "EARLY_EXIT", "IMPULSIVE_ENTRY", "HESITATION"], 0.85m),
            Explain("EmotionalControl", emotional, codes, ["STRONG_EMOTIONS", "FOMO", "REVENGE_TRADING", "OVERTRADING", "HESITATION"], 0.75m),
            new TradingCoachScoreExplanation("JournalCompleteness", journal, ["Normalized field completeness"], 1m, true),
            new TradingCoachScoreExplanation("OverallProcessQuality", overall, ["Equal-weight deterministic average of process scores"], 0.9m, true)
        };

        return new TradingCoachScores(plan, risk, execution, emotional, journal, overall, explanations);
    }

    private static int Score(int initial, IReadOnlySet<string> codes, params (string Code, int Penalty)[] penalties)
    {
        var value = penalties.Where(item => codes.Contains(item.Code)).Aggregate(initial, (current, item) => current - item.Penalty);
        return Math.Clamp(value, 0, 100);
    }

    private static TradingCoachScoreExplanation Explain(
        string name,
        int value,
        IReadOnlySet<string> codes,
        IReadOnlyCollection<string> relevantCodes,
        decimal confidence)
    {
        var factors = relevantCodes.Where(codes.Contains).ToArray();
        return new TradingCoachScoreExplanation(
            name,
            value,
            factors.Length == 0 ? ["No deterministic penalty detected"] : factors,
            confidence,
            true);
    }
}
