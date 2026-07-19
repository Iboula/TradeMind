using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics;

public sealed class TradingPeriodAggregator : ITradingPeriodAggregator
{
    private readonly ITradingStatisticsCalculator _statistics;

    public TradingPeriodAggregator(ITradingStatisticsCalculator statistics)
    {
        _statistics = statistics;
    }

    public IReadOnlyList<TradingPeriodAggregate> Group(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        TradingAnalyticsGroupingPeriod groupingPeriod,
        TradingJournalAnalyticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(options);
        if (trades.Count == 0)
        {
            return [];
        }

        if (groupingPeriod == TradingAnalyticsGroupingPeriod.None)
        {
            var start = trades.Select(Timestamp).Where(value => value is not null).Min() ?? DateTimeOffset.UnixEpoch;
            var maximum = trades.Select(EndTimestamp).Where(value => value is not null).Max() ?? start;
            var end = maximum > start ? maximum.AddTicks(1) : start.AddTicks(1);
            return [Create(start, end, trades, options.MinimumTradesPerGroup)];
        }

        var grouped = trades
            .Select(trade => (Trade: trade, Timestamp: Timestamp(trade)))
            .Where(item => item.Timestamp is not null)
            .GroupBy(item => PeriodStart(item.Timestamp!.Value, groupingPeriod))
            .OrderBy(group => group.Key)
            .Select(group => Create(
                group.Key,
                PeriodEnd(group.Key, groupingPeriod),
                group.Select(item => item.Trade).ToArray(),
                options.MinimumTradesPerGroup))
            .ToArray();

        return Array.AsReadOnly(grouped.TakeLast(options.MaximumGroups).ToArray());
    }

    private TradingPeriodAggregate Create(
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        int minimumTrades)
    {
        var behaviorCounts = trades.SelectMany(trade => trade.Behaviors)
            .GroupBy(value => value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return new TradingPeriodAggregate(
            start,
            end,
            trades.Count,
            _statistics.CalculateAggregates(trades),
            TradingStatisticsCalculator.AverageScores(trades),
            behaviorCounts,
            _statistics.Mean(trades.Select(trade => trade.Completeness)) ?? 0,
            trades.Count >= minimumTrades);
    }

    private static DateTimeOffset PeriodStart(DateTimeOffset timestamp, TradingAnalyticsGroupingPeriod period)
    {
        var utc = timestamp.ToUniversalTime();
        var day = new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
        return period switch
        {
            TradingAnalyticsGroupingPeriod.Day => day,
            TradingAnalyticsGroupingPeriod.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
            TradingAnalyticsGroupingPeriod.Month => new DateTimeOffset(day.Year, day.Month, 1, 0, 0, 0, TimeSpan.Zero),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
    }

    private static DateTimeOffset PeriodEnd(DateTimeOffset start, TradingAnalyticsGroupingPeriod period) => period switch
    {
        TradingAnalyticsGroupingPeriod.Day => start.AddDays(1),
        TradingAnalyticsGroupingPeriod.Week => start.AddDays(7),
        TradingAnalyticsGroupingPeriod.Month => start.AddMonths(1),
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    private static DateTimeOffset? Timestamp(TradingJournalTradeAnalysis trade) =>
        trade.Trade.OpenedAtUtc ?? trade.Trade.ClosedAtUtc;

    private static DateTimeOffset? EndTimestamp(TradingJournalTradeAnalysis trade) =>
        trade.Trade.ClosedAtUtc ?? trade.Trade.OpenedAtUtc;
}
