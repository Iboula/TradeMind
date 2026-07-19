namespace TradeMind.Trading.Analytics;

public sealed class TradingJournalRuleAnalyzer : ITradingJournalRuleAnalyzer
{
    public TradingJournalRuleAnalysisResult Analyze(
        TradingJournalAggregateMetrics aggregates,
        TradingStreakMetrics streaks,
        IReadOnlyList<TradingPeriodAggregate> periods,
        IReadOnlyList<TradingSetupAnalytics> setups,
        IReadOnlyList<TradingBehaviorTrend> behaviors,
        RiskDriftAnalysis riskDrift,
        IReadOnlyList<TradingScoreEvolution> scoreEvolution,
        TradingJournalDataQuality dataQuality,
        TradeMind.Trading.Coaching.TradingCoachProfile profile)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        ArgumentNullException.ThrowIfNull(streaks);
        ArgumentNullException.ThrowIfNull(periods);
        ArgumentNullException.ThrowIfNull(setups);
        ArgumentNullException.ThrowIfNull(behaviors);
        ArgumentNullException.ThrowIfNull(riskDrift);
        ArgumentNullException.ThrowIfNull(scoreEvolution);
        ArgumentNullException.ThrowIfNull(dataQuality);
        ArgumentNullException.ThrowIfNull(profile);
        var findings = new List<TradingJournalAnalyticsFinding>();

        var riskThreshold = profile.MaximumRiskPerTrade ?? 2m;
        if (aggregates.AverageRiskPercentage > riskThreshold)
        {
            findings.Add(Finding("HIGH_AVERAGE_RISK", "risk", "Average documented risk exceeded the supplied review threshold.", TradingAnalyticsSeverity.Warning));
        }

        if (profile.MaximumRiskPerTrade is not null && riskDrift.Observations.Any(value => value.Contains("exceeded repeatedly", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(Finding("REPEATED_RISK_EXCEEDANCE", "risk", "The supplied coaching-profile risk maximum was exceeded repeatedly.", TradingAnalyticsSeverity.Critical, riskDrift.SupportingTradeIndexes));
        }

        if (IsWorsening(scoreEvolution, "PlanAdherence") || IsWorsening(scoreEvolution, "OverallProcessQuality"))
        {
            findings.Add(Finding("DECLINING_DISCIPLINE", "process", "Deterministic process scores declined across the supplied history.", TradingAnalyticsSeverity.Warning));
        }

        if (ViolationRateIncreased(periods))
        {
            findings.Add(Finding("GROWING_RULE_VIOLATIONS", "process", "The observed rule-violation rate was higher in recent periods.", TradingAnalyticsSeverity.Warning));
        }

        if (aggregates.JournalCompletenessAverage < 60 || dataQuality.OverallCompleteness < 60)
        {
            findings.Add(Finding("WEAK_JOURNALING", "data-quality", "Journal completeness is too low for high-confidence historical interpretation.", TradingAnalyticsSeverity.Warning));
        }

        if (streaks.LongestLosingStreak >= 2 && riskDrift.Direction == TradingTrendDirection.Worsening)
        {
            findings.Add(Finding("LOSS_STREAK_RISK_INCREASE", "risk", "A losing streak and higher subsequent documented risk appear in the same supplied history.", TradingAnalyticsSeverity.Critical, riskDrift.SupportingTradeIndexes));
        }

        if (aggregates.NegativeProcessPositiveOutcomeCount > 0)
        {
            findings.Add(Finding("POSITIVE_RESULT_WEAK_PROCESS", "process", "Positive outcomes occurred alongside weak deterministic process scores.", TradingAnalyticsSeverity.Warning));
        }

        if (aggregates.TotalResultR < 0 && IsImproving(scoreEvolution, "OverallProcessQuality"))
        {
            findings.Add(Finding("NEGATIVE_RESULT_IMPROVING_PROCESS", "process", "Historical result R was negative while deterministic process scores improved.", TradingAnalyticsSeverity.Information));
        }

        if (setups.Any(setup => setup.DataCompleteness < 60))
        {
            findings.Add(Finding("UNDERDOCUMENTED_SETUP", "setup", "At least one setup group has insufficient documentation for reliable comparison.", TradingAnalyticsSeverity.Warning));
        }

        var recurring = behaviors.Where(behavior => behavior.AffectedTradeCount >= 2).ToArray();
        if (recurring.Length > 0)
        {
            findings.Add(Finding(
                "RECURRING_BEHAVIOR",
                "behavior",
                "A documented behavior recurred across multiple supplied trades.",
                TradingAnalyticsSeverity.Warning,
                recurring.SelectMany(behavior => behavior.RelatedTradeIndexes)));
        }

        var sufficientEvolution = scoreEvolution.Where(evolution => evolution.IsSampleSizeSufficient).ToArray();
        if (sufficientEvolution.Length > 0 && sufficientEvolution.All(evolution => evolution.TrendDirection != TradingTrendDirection.Improving))
        {
            findings.Add(Finding("NO_OBSERVABLE_PROGRESS", "process", "No improving deterministic score trend was observable in the supplied sample.", TradingAnalyticsSeverity.Information));
        }

        if (sufficientEvolution.Any(evolution => evolution.TrendDirection == TradingTrendDirection.Improving))
        {
            findings.Add(Finding("OBSERVABLE_IMPROVEMENT", "process", "At least one deterministic process score improved in the supplied sample.", TradingAnalyticsSeverity.Information));
        }

        var ordered = findings.OrderByDescending(finding => finding.Severity).ThenBy(finding => finding.Code, StringComparer.Ordinal).ToArray();
        var strengths = Strengths(aggregates, behaviors, riskDrift, scoreEvolution);
        var recommendations = ordered.Select(finding => Recommendation(finding)).ToArray();
        var checklist = ordered.Take(5).Select(finding => Checklist(finding.Code)).Distinct(StringComparer.Ordinal).ToArray();
        return new TradingJournalRuleAnalysisResult(ordered, strengths, recommendations, checklist);
    }

    private static IReadOnlyList<string> Strengths(
        TradingJournalAggregateMetrics aggregates,
        IReadOnlyList<TradingBehaviorTrend> behaviors,
        RiskDriftAnalysis riskDrift,
        IReadOnlyList<TradingScoreEvolution> scoreEvolution)
    {
        var strengths = new List<string>();
        if (aggregates.PlanAdherenceRate >= 80)
        {
            strengths.Add("Plan adherence was documented on most supplied trades.");
        }

        if (aggregates.JournalCompletenessAverage >= 75)
        {
            strengths.Add("The supplied journal history has strong process completeness.");
        }

        if (!riskDrift.DriftDetected)
        {
            strengths.Add("No worsening risk drift was detected in the supplied sample.");
        }

        if (behaviors.Any(behavior => behavior.TrendDirection == TradingTrendDirection.Improving)
            || scoreEvolution.Any(evolution => evolution.TrendDirection == TradingTrendDirection.Improving))
        {
            strengths.Add("At least one documented process indicator improved over time.");
        }

        return Array.AsReadOnly(strengths.ToArray());
    }

    private static bool ViolationRateIncreased(IReadOnlyList<TradingPeriodAggregate> periods)
    {
        if (periods.Count < 2)
        {
            return false;
        }

        var split = Math.Max(1, periods.Count / 2);
        var first = periods.Take(split).Average(period => period.AggregateMetrics.RuleViolationTradeCount / (decimal)Math.Max(period.TradeCount, 1));
        var recent = periods.TakeLast(split).Average(period => period.AggregateMetrics.RuleViolationTradeCount / (decimal)Math.Max(period.TradeCount, 1));
        return recent - first > 0.1m;
    }

    private static bool IsImproving(IEnumerable<TradingScoreEvolution> values, string scoreName) =>
        values.Any(value => value.ScoreName == scoreName && value.TrendDirection == TradingTrendDirection.Improving);

    private static bool IsWorsening(IEnumerable<TradingScoreEvolution> values, string scoreName) =>
        values.Any(value => value.ScoreName == scoreName && value.TrendDirection == TradingTrendDirection.Worsening);

    private static TradingJournalAnalyticsFinding Finding(
        string code,
        string category,
        string message,
        TradingAnalyticsSeverity severity,
        IEnumerable<int>? indexes = null) => new(code, category, message, severity, indexes?.ToArray());

    private static TradingJournalAnalyticsRecommendation Recommendation(TradingJournalAnalyticsFinding finding) => new(
        $"REVIEW_{finding.Code}",
        finding.Code switch
        {
            "HIGH_AVERAGE_RISK" or "REPEATED_RISK_EXCEEDANCE" or "LOSS_STREAK_RISK_INCREASE" => "Review documented risk against the predefined personal limit before the next journal review.",
            "WEAK_JOURNALING" or "UNDERDOCUMENTED_SETUP" => "Complete the missing process fields consistently before comparing historical groups.",
            "RECURRING_BEHAVIOR" => "Label the recurring behavior explicitly and review the conditions recorded around each occurrence.",
            _ => "Review this deterministic finding against the supplied journal entries and define one measurable process action."
        },
        [finding.Code]);

    private static string Checklist(string code) => code switch
    {
        "HIGH_AVERAGE_RISK" or "REPEATED_RISK_EXCEEDANCE" or "LOSS_STREAK_RISK_INCREASE" => "Check documented risk against the personal limit.",
        "WEAK_JOURNALING" or "UNDERDOCUMENTED_SETUP" => "Complete plan, risk, execution, and review fields.",
        "RECURRING_BEHAVIOR" => "Label recurring behavior without inferring causality.",
        _ => "Review the related deterministic finding."
    };
}
