using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics;

public sealed class TradingStatisticsCalculator : ITradingStatisticsCalculator
{
    public decimal? Mean(IEnumerable<decimal> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var data = values.ToArray();
        return data.Length == 0 ? null : Round(data.Sum() / data.Length);
    }

    public decimal? Median(IEnumerable<decimal> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var data = values.OrderBy(value => value).ToArray();
        if (data.Length == 0)
        {
            return null;
        }

        var middle = data.Length / 2;
        return data.Length % 2 == 0
            ? Round((data[middle - 1] + data[middle]) / 2m)
            : data[middle];
    }

    public decimal? Minimum(IEnumerable<decimal> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var data = values.ToArray();
        return data.Length == 0 ? null : data.Min();
    }

    public decimal? Maximum(IEnumerable<decimal> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var data = values.ToArray();
        return data.Length == 0 ? null : data.Max();
    }

    public decimal Sum(IEnumerable<decimal> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Round(values.Sum());
    }

    public decimal Rate(int numerator, int denominator) => denominator <= 0
        ? 0
        : Round(numerator * 100m / denominator);

    public TimeSpan? AverageDuration(IEnumerable<TimeSpan> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var data = values.ToArray();
        return data.Length == 0 ? null : TimeSpan.FromTicks((long)Math.Round(data.Average(value => value.Ticks)));
    }

    public TradingJournalAggregateMetrics CalculateAggregates(IReadOnlyList<TradingJournalTradeAnalysis> trades)
    {
        ArgumentNullException.ThrowIfNull(trades);
        if (trades.Count == 0)
        {
            return TradingJournalAggregateMetrics.Empty;
        }

        var resultValues = trades.Select(TradingAnalyticsFacts.ResultR).Where(value => value is not null).Select(value => value!.Value).ToArray();
        var risks = trades.Select(TradingAnalyticsFacts.RiskPercentage).Where(value => value is not null).Select(value => value!.Value).ToArray();
        var durations = trades.Select(TradingAnalyticsFacts.Duration).Where(value => value is not null).Select(value => value!.Value).ToArray();
        var winners = resultValues.Count(value => value > 0);
        var losers = resultValues.Count(value => value < 0);
        var breakEven = resultValues.Count(value => value == 0);

        return new TradingJournalAggregateMetrics(
            trades.Count,
            winners,
            losers,
            breakEven,
            trades.Count(trade => trade.Completeness >= 60),
            trades.Count(TradingAnalyticsFacts.HasPlan),
            trades.Count(TradingAnalyticsFacts.HasRuleViolation),
            Mean(risks),
            Median(risks),
            Maximum(risks),
            Mean(resultValues),
            Median(resultValues),
            Maximum(resultValues),
            Minimum(resultValues),
            Sum(resultValues),
            AverageDuration(durations),
            Rate(trades.Count(TradingAnalyticsFacts.AdheredToPlan), trades.Count),
            Rate(trades.Count(trade => trade.Trade.StopLoss is not null), trades.Count),
            Mean(trades.Select(trade => trade.Completeness)) ?? 0,
            trades.Count(trade => TradingAnalyticsFacts.ResultR(trade) < 0 && trade.Scores.OverallProcessQuality >= 70),
            trades.Count(trade => TradingAnalyticsFacts.ResultR(trade) > 0 && trade.Scores.OverallProcessQuality < 60));
    }

    public HistoricalRDrawdown CalculateHistoricalDrawdown(IReadOnlyList<TradingJournalTradeAnalysis> trades)
    {
        ArgumentNullException.ThrowIfNull(trades);
        if (trades.Count == 0)
        {
            return HistoricalRDrawdown.Empty;
        }

        var cumulativeValues = new decimal[trades.Count];
        decimal cumulative = 0;
        decimal peak = 0;
        var peakIndex = -1;
        decimal maximumDrawdown = 0;
        decimal drawdownPeak = 0;
        decimal drawdownTrough = 0;
        int? start = null;
        int? end = null;

        for (var index = 0; index < trades.Count; index++)
        {
            cumulative += TradingAnalyticsFacts.ResultR(trades[index]) ?? 0;
            cumulativeValues[index] = cumulative;
            if (cumulative > peak)
            {
                peak = cumulative;
                peakIndex = index;
            }

            var drawdown = peak - cumulative;
            if (drawdown > maximumDrawdown)
            {
                maximumDrawdown = drawdown;
                drawdownPeak = peak;
                drawdownTrough = cumulative;
                start = peakIndex < 0 ? 0 : peakIndex;
                end = index;
            }
        }

        if (end is null)
        {
            return HistoricalRDrawdown.Empty;
        }

        int? recovery = null;
        for (var index = end.Value + 1; index < cumulativeValues.Length; index++)
        {
            if (cumulativeValues[index] >= drawdownPeak)
            {
                recovery = index;
                break;
            }
        }

        return new HistoricalRDrawdown(
            Round(maximumDrawdown),
            Round(drawdownPeak),
            Round(drawdownTrough),
            start,
            end,
            recovery,
            recovery is not null);
    }

    public TradingStreakMetrics CalculateStreaks(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        TradingCoachProfile profile,
        decimal incompleteThreshold)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(profile);
        var outcomes = trades.Select(trade => Math.Sign(TradingAnalyticsFacts.ResultR(trade) ?? 0)).ToArray();
        var longestWinning = Longest(outcomes, value => value > 0);
        var longestLosing = Longest(outcomes, value => value < 0);
        var currentWinning = Current(outcomes, value => value > 0);
        var currentLosing = Current(outcomes, value => value < 0);

        return new TradingStreakMetrics(
            longestWinning,
            longestLosing,
            currentWinning,
            currentLosing,
            Longest(trades, TradingAnalyticsFacts.HasRuleViolation),
            Longest(trades, trade => TradingAnalyticsFacts.IsOversized(trade, profile)),
            Longest(trades, trade => trade.Completeness < incompleteThreshold));
    }

    public IReadOnlyList<TradingScoreEvolution> CalculateScoreEvolution(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        IReadOnlyList<TradingPeriodAggregate> periods,
        int minimumTradesForTrend)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(periods);
        var definitions = new (string Name, Func<TradingCoachScores, int> Select)[]
        {
            ("PlanAdherence", scores => scores.PlanAdherence),
            ("RiskDiscipline", scores => scores.RiskDiscipline),
            ("ExecutionQuality", scores => scores.ExecutionQuality),
            ("EmotionalControl", scores => scores.EmotionalControl),
            ("JournalCompleteness", scores => scores.JournalCompleteness),
            ("OverallProcessQuality", scores => scores.OverallProcessQuality)
        };

        return Array.AsReadOnly(definitions.Select(definition =>
        {
            var values = trades.Select(trade => (decimal)definition.Select(trade.Scores)).ToArray();
            var sufficient = values.Length >= minimumTradesForTrend;
            var split = Math.Max(1, values.Length / 2);
            decimal? first = values.Length == 0 ? null : Mean(values.Take(split));
            decimal? recent = values.Length == 0 ? null : Mean(values.Skip(values.Length - split));
            decimal? change = first is null || recent is null ? null : Round(recent.Value - first.Value);
            var direction = sufficient ? ScoreDirection(change) : TradingTrendDirection.InsufficientData;
            var periodValues = PeriodScoreValues(trades, periods, definition.Select);
            return new TradingScoreEvolution(definition.Name, first, recent, change, direction, periodValues, sufficient);
        }).ToArray());
    }

    internal static TradingCoachScores AverageScores(IReadOnlyList<TradingJournalTradeAnalysis> trades)
    {
        if (trades.Count == 0)
        {
            return TradingCoachScores.Empty;
        }

        return new TradingCoachScores(
            AverageScore(trades, scores => scores.PlanAdherence),
            AverageScore(trades, scores => scores.RiskDiscipline),
            AverageScore(trades, scores => scores.ExecutionQuality),
            AverageScore(trades, scores => scores.EmotionalControl),
            AverageScore(trades, scores => scores.JournalCompleteness),
            AverageScore(trades, scores => scores.OverallProcessQuality),
            []);
    }

    private static IReadOnlyList<TradingPeriodScoreValue> PeriodScoreValues(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        IReadOnlyList<TradingPeriodAggregate> periods,
        Func<TradingCoachScores, int> select)
    {
        if (periods.Count > 0)
        {
            return Array.AsReadOnly(periods.Select(period => new TradingPeriodScoreValue(
                period.PeriodStartUtc,
                period.PeriodEndUtc,
                select(period.AverageScores))).ToArray());
        }

        return Array.AsReadOnly(trades.Select((trade, index) =>
        {
            var start = trade.Trade.OpenedAtUtc ?? trade.Trade.ClosedAtUtc ?? DateTimeOffset.UnixEpoch.AddTicks(index * 2L);
            return new TradingPeriodScoreValue(start, start.AddTicks(1), select(trade.Scores));
        }).OrderBy(value => value.PeriodStartUtc).ToArray());
    }

    private static TradingTrendDirection ScoreDirection(decimal? change) => change switch
    {
        > 2 => TradingTrendDirection.Improving,
        < -2 => TradingTrendDirection.Worsening,
        _ => TradingTrendDirection.Stable
    };

    private static int AverageScore(IReadOnlyList<TradingJournalTradeAnalysis> trades, Func<TradingCoachScores, int> select) =>
        (int)Math.Round(trades.Average(trade => select(trade.Scores)), MidpointRounding.AwayFromZero);

    private static int Longest<T>(IEnumerable<T> values, Func<T, bool> predicate)
    {
        var longest = 0;
        var current = 0;
        foreach (var value in values)
        {
            current = predicate(value) ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }

        return longest;
    }

    private static int Current<T>(IReadOnlyList<T> values, Func<T, bool> predicate)
    {
        var current = 0;
        for (var index = values.Count - 1; index >= 0 && predicate(values[index]); index--)
        {
            current++;
        }

        return current;
    }

    private static decimal Round(decimal value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);
}

internal static class TradingAnalyticsFacts
{
    private static readonly HashSet<string> RuleViolationCodes = new(
        [
            "ACTUAL_RISK_ABOVE_PLANNED", "RISK_ABOVE_PROFILE_MAXIMUM", "RULES_NOT_RESPECTED",
            "FOMO", "REVENGE_TRADING", "OVERTRADING", "IMPULSIVE_ENTRY", "MOVED_STOP",
            "EARLY_EXIT", "IGNORED_PLAN", "OVERSIZED_POSITION"
        ],
        StringComparer.Ordinal);

    public static decimal? ResultR(TradingJournalTradeAnalysis trade) =>
        Metric(trade, TradeMetricNames.RealizedRMultiple) ?? trade.Trade.ResultRMultiple;

    public static decimal? RiskPercentage(TradingJournalTradeAnalysis trade) =>
        Metric(trade, TradeMetricNames.RiskPercentage) ?? trade.Trade.ActualRiskPercentage;

    public static TimeSpan? Duration(TradingJournalTradeAnalysis trade)
    {
        var minutes = Metric(trade, TradeMetricNames.DurationMinutes);
        return minutes is null ? null : TimeSpan.FromMinutes((double)minutes.Value);
    }

    public static bool HasPlan(TradingJournalTradeAnalysis trade) => !string.IsNullOrWhiteSpace(trade.Trade.PlanBeforeTrade);

    public static bool AdheredToPlan(TradingJournalTradeAnalysis trade) => HasPlan(trade)
        && !trade.CoachingAnalysis.Findings.Any(finding => finding.Code is "IGNORED_PLAN" or "RULES_NOT_RESPECTED");

    public static bool HasRuleViolation(TradingJournalTradeAnalysis trade) => trade.CoachingAnalysis.Findings.Any(
        finding => finding.Severity == TradingCoachFindingSeverity.Critical || RuleViolationCodes.Contains(finding.Code));

    public static bool IsOversized(TradingJournalTradeAnalysis trade, TradingCoachProfile profile)
    {
        if (trade.Behaviors.Contains("OVERSIZED_POSITION", StringComparer.Ordinal))
        {
            return true;
        }

        return profile.MaximumRiskPerTrade is { } maximum
            && RiskPercentage(trade) is { } risk
            && risk > maximum;
    }

    private static decimal? Metric(TradingJournalTradeAnalysis trade, string name) =>
        trade.Metrics.ComputedMetrics.TryGetValue(name, out var value) ? value : null;
}
