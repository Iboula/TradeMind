using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics;

public sealed class TradingJournalAnalyticsMerger : ITradingJournalAnalyticsMerger
{
    public TradingJournalAnalyticsReport Merge(
        TradingJournalAnalyticsReport deterministicReport,
        TradingJournalRuleAnalysisResult rules,
        TradingJournalAIInterpretation? aiInterpretation,
        TradingJournalAnalyticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(deterministicReport);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(options);

        var findingCodes = rules.Findings.Select(finding => finding.Code).ToHashSet(StringComparer.Ordinal);
        var recommendations = rules.Recommendations
            .Concat(aiInterpretation?.Recommendations ?? [])
            .Where(recommendation => recommendation.RelatedFindingCodes.Count > 0
                && recommendation.RelatedFindingCodes.Any(findingCodes.Contains)
                && !TradingCoachSafetyPolicy.ContainsProhibitedContent(recommendation.Text))
            .GroupBy(recommendation => Normalize(recommendation.Text), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(recommendation => recommendation.Code, StringComparer.Ordinal)
            .Take(options.MaximumRecommendations)
            .ToArray();
        var checklist = rules.Checklist
            .Concat(aiInterpretation?.NextReviewChecklist ?? [])
            .Where(value => !TradingCoachSafetyPolicy.ContainsProhibitedContent(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(options.MaximumChecklistItems)
            .ToArray();
        var explanations = deterministicReport.Explanations
            .Concat(aiInterpretation?.Explanations ?? [])
            .Where(value => !TradingCoachSafetyPolicy.ContainsProhibitedContent(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        return new TradingJournalAnalyticsReport(
            deterministicReport.AnalysisId,
            aiInterpretation?.Summary ?? deterministicReport.Summary,
            explanations,
            deterministicReport.DateRange,
            deterministicReport.DataQuality,
            deterministicReport.AggregateMetrics,
            deterministicReport.HistoricalDrawdown,
            deterministicReport.Streaks,
            deterministicReport.Periods,
            deterministicReport.SetupAnalytics,
            deterministicReport.BehaviorTrends,
            deterministicReport.RiskDrift,
            deterministicReport.PostOutcomeObservations,
            deterministicReport.ScoreEvolution,
            rules.Strengths,
            rules.Findings,
            recommendations,
            checklist,
            deterministicReport.Disclaimer,
            deterministicReport.GeneratedAtUtc,
            aiInterpretation is null ? null : TradingJournalAnalyticsConstants.AgentVersion,
            aiInterpretation is not null);
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
