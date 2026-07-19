namespace TradeMind.Trading.Analytics;

public sealed class TradingBehaviorTrendAnalyzer : ITradingBehaviorTrendAnalyzer
{
    private static readonly string[] Behaviors =
    [
        "FOMO", "REVENGE_TRADING", "OVERTRADING", "HESITATION", "IMPULSIVE_ENTRY",
        "MOVED_STOP", "EARLY_EXIT", "IGNORED_PLAN", "OVERSIZED_POSITION"
    ];

    public IReadOnlyList<TradingBehaviorTrend> Analyze(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        int minimumTradesForTrend,
        int maximumPatterns)
    {
        ArgumentNullException.ThrowIfNull(trades);
        if (minimumTradesForTrend < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumTradesForTrend));
        }

        if (maximumPatterns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPatterns));
        }

        var trends = Behaviors.Take(maximumPatterns).Select(behavior =>
        {
            var affected = trades
                .Where(trade => trade.Behaviors.Contains(behavior, StringComparer.Ordinal))
                .ToArray();
            var sufficient = trades.Count >= minimumTradesForTrend;
            var split = trades.Count / 2;
            var firstCount = trades.Take(split).Count(trade => trade.Behaviors.Contains(behavior, StringComparer.Ordinal));
            var recentCount = trades.Skip(split).Count(trade => trade.Behaviors.Contains(behavior, StringComparer.Ordinal));
            var firstRate = split == 0 ? 0 : firstCount / (decimal)split;
            var recentSize = trades.Count - split;
            var recentRate = recentSize == 0 ? 0 : recentCount / (decimal)recentSize;
            var direction = sufficient ? Direction(firstRate, recentRate) : TradingTrendDirection.InsufficientData;
            var dates = affected.Select(Timestamp).Where(value => value is not null).Select(value => value!.Value).OrderBy(value => value).ToArray();
            var confidence = trades.Count == 0
                ? 0
                : Math.Clamp(trades.Count / (decimal)Math.Max(minimumTradesForTrend, 1) * 0.7m
                    + affected.Length / (decimal)trades.Count * 0.3m, 0, 1);

            return new TradingBehaviorTrend(
                behavior,
                affected.Length,
                affected.Length,
                dates.FirstOrDefaultNullable(),
                dates.LastOrDefaultNullable(),
                direction,
                Round(confidence),
                affected.Select(trade => trade.OriginalIndex).ToArray(),
                [Observation(behavior, direction, affected.Length)]);
        }).ToArray();

        return Array.AsReadOnly(trends);
    }

    private static TradingTrendDirection Direction(decimal firstRate, decimal recentRate)
    {
        var change = recentRate - firstRate;
        return change switch
        {
            > 0.1m => TradingTrendDirection.Worsening,
            < -0.1m => TradingTrendDirection.Improving,
            _ => TradingTrendDirection.Stable
        };
    }

    private static string Observation(string behavior, TradingTrendDirection direction, int count) =>
        $"{behavior} was documented in {count} supplied trade(s); the descriptive direction is {direction}.";

    private static DateTimeOffset? Timestamp(TradingJournalTradeAnalysis trade) =>
        trade.Trade.OpenedAtUtc ?? trade.Trade.ClosedAtUtc;

    private static decimal Round(decimal value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);
}
