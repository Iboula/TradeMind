namespace TradeMind.Trading.Analytics;

public sealed class PostOutcomeBehaviorAnalyzer : IPostOutcomeBehaviorAnalyzer
{
    private readonly ITradingStatisticsCalculator _statistics;

    public PostOutcomeBehaviorAnalyzer(ITradingStatisticsCalculator statistics)
    {
        _statistics = statistics;
    }

    public IReadOnlyList<PostOutcomeBehaviorObservation> Analyze(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        int minimumTradesForTrend,
        string requestedLanguage)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedLanguage);
        var contexts = new (string Name, Func<int, bool> Matches)[]
        {
            ("AfterWin", index => TradingAnalyticsFacts.ResultR(trades[index]) > 0),
            ("AfterLoss", index => TradingAnalyticsFacts.ResultR(trades[index]) < 0),
            ("AfterTwoConsecutiveLosses", index => index > 0
                && TradingAnalyticsFacts.ResultR(trades[index - 1]) < 0
                && TradingAnalyticsFacts.ResultR(trades[index]) < 0),
            ("AfterLargeWin", index => TradingAnalyticsFacts.ResultR(trades[index]) >= 2),
            ("AfterRuleViolation", index => TradingAnalyticsFacts.HasRuleViolation(trades[index]))
        };

        return Array.AsReadOnly(contexts.Select(context => Create(
            context.Name,
            Enumerable.Range(0, Math.Max(0, trades.Count - 1)).Where(context.Matches).ToArray(),
            trades,
            Math.Max(2, minimumTradesForTrend / 2),
            requestedLanguage)).ToArray());
    }

    private PostOutcomeBehaviorObservation Create(
        string context,
        IReadOnlyList<int> triggerIndexes,
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        int minimumSamples,
        string language)
    {
        var riskChanges = new List<decimal>();
        var durations = new List<TimeSpan>();
        var violationCount = 0;
        var completeness = new List<decimal>();
        var behaviors = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var index in triggerIndexes)
        {
            var current = trades[index];
            var next = trades[index + 1];
            var currentRisk = TradingAnalyticsFacts.RiskPercentage(current);
            var nextRisk = TradingAnalyticsFacts.RiskPercentage(next);
            if (currentRisk is not null && nextRisk is not null)
            {
                riskChanges.Add(nextRisk.Value - currentRisk.Value);
            }

            violationCount += TradingAnalyticsFacts.HasRuleViolation(next) ? 1 : 0;
            completeness.Add(next.Completeness);
            var currentTime = current.Trade.ClosedAtUtc ?? current.Trade.OpenedAtUtc;
            var nextTime = next.Trade.OpenedAtUtc ?? next.Trade.ClosedAtUtc;
            if (currentTime is not null && nextTime is not null && nextTime >= currentTime)
            {
                durations.Add(nextTime.Value - currentTime.Value);
            }

            foreach (var behavior in next.Behaviors)
            {
                behaviors[behavior] = behaviors.GetValueOrDefault(behavior) + 1;
            }
        }

        var sufficient = triggerIndexes.Count >= minimumSamples;
        var association = language.StartsWith("fr", StringComparison.OrdinalIgnoreCase)
            ? "association observ\u00e9e"
            : "observed association";
        var observation = triggerIndexes.Count == 0
            ? $"No {association} could be calculated for {context} from the supplied history."
            : $"The {association} for {context} is descriptive only and does not establish causality.";

        return new PostOutcomeBehaviorObservation(
            context,
            triggerIndexes.Count,
            _statistics.Mean(riskChanges),
            _statistics.Rate(violationCount, triggerIndexes.Count),
            _statistics.Mean(completeness),
            _statistics.AverageDuration(durations),
            behaviors,
            observation,
            sufficient);
    }
}
