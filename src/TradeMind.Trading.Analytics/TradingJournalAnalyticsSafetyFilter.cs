using Microsoft.Extensions.Options;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics;

public sealed class TradingJournalAnalyticsSafetyFilter : ITradingJournalAnalyticsSafetyFilter
{
    private readonly TradingJournalAnalyticsSafetyOptions _options;

    public TradingJournalAnalyticsSafetyFilter(IOptions<TradingJournalAnalyticsSafetyOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public TradingJournalAnalyticsReport Validate(TradingJournalAnalyticsReport report, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (!AllText(report).Any(TradingCoachSafetyPolicy.ContainsProhibitedContent))
        {
            return report;
        }

        if (_options.FailClosed)
        {
            throw new TradingJournalAnalyticsSafetyException(report.AnalysisId, correlationId);
        }

        return Clean(report);
    }

    private static TradingJournalAnalyticsReport Clean(TradingJournalAnalyticsReport report) => new(
        report.AnalysisId,
        SafeOrRemoved(report.Summary),
        Safe(report.Explanations),
        report.DateRange,
        report.DataQuality,
        report.AggregateMetrics,
        report.HistoricalDrawdown,
        report.Streaks,
        report.Periods,
        report.SetupAnalytics,
        report.BehaviorTrends,
        report.RiskDrift,
        report.PostOutcomeObservations,
        report.ScoreEvolution,
        Safe(report.Strengths),
        report.PriorityIssues.Where(finding => IsSafe(finding.Message)).ToArray(),
        report.Recommendations.Where(recommendation => IsSafe(recommendation.Text)).ToArray(),
        Safe(report.NextReviewChecklist),
        SafeOrRemoved(report.Disclaimer),
        report.GeneratedAtUtc,
        report.AgentVersion,
        report.AIInterpretationUsed);

    private static IEnumerable<string> AllText(TradingJournalAnalyticsReport report)
    {
        yield return report.Summary;
        yield return report.Disclaimer;
        foreach (var value in report.Explanations
            .Concat(report.Strengths)
            .Concat(report.NextReviewChecklist)
            .Concat(report.DataQuality.Limitations)
            .Concat(report.RiskDrift.Observations)
            .Concat(report.BehaviorTrends.SelectMany(trend => trend.Observations))
            .Concat(report.PostOutcomeObservations.Select(observation => observation.Observation)))
        {
            yield return value;
        }

        foreach (var finding in report.PriorityIssues)
        {
            yield return finding.Message;
        }

        foreach (var recommendation in report.Recommendations)
        {
            yield return recommendation.Text;
        }
    }

    private static IReadOnlyCollection<string> Safe(IEnumerable<string> values) =>
        values.Where(value => IsSafe(value)).ToArray();

    private static bool IsSafe(string value) => !TradingCoachSafetyPolicy.ContainsProhibitedContent(value);

    private static string SafeOrRemoved(string value) => IsSafe(value)
        ? value
        : "Content removed by safety policy.";
}
