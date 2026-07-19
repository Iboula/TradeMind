namespace TradeMind.Trading.Analytics;

public sealed class TradingSetupAnalyzer : ITradingSetupAnalyzer
{
    private readonly ITradingStatisticsCalculator _statistics;

    public TradingSetupAnalyzer(ITradingStatisticsCalculator statistics)
    {
        _statistics = statistics;
    }

    public IReadOnlyList<TradingSetupAnalytics> Analyze(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        TradingJournalAnalyticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(options);
        var setups = trades
            .Where(trade => !string.IsNullOrWhiteSpace(trade.Trade.SetupName))
            .GroupBy(trade => Normalize(trade.Trade.SetupName!), StringComparer.OrdinalIgnoreCase)
            .Select(group => Create(group.Key, group.ToArray(), options.MinimumTradesPerGroup))
            .OrderByDescending(setup => setup.TradeCount)
            .ThenBy(setup => setup.SetupName, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaximumSetups)
            .ToArray();
        return Array.AsReadOnly(setups);
    }

    private TradingSetupAnalytics Create(
        string setupName,
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        int minimumTrades)
    {
        var results = trades.Select(TradingAnalyticsFacts.ResultR).Where(value => value is not null).Select(value => value!.Value).ToArray();
        var risks = trades.Select(TradingAnalyticsFacts.RiskPercentage).Where(value => value is not null).Select(value => value!.Value).ToArray();
        var mistakes = trades.SelectMany(trade => trade.Trade.Mistakes)
            .Where(value => !value.Contains("no process mistake", StringComparison.OrdinalIgnoreCase))
            .GroupBy(Normalize, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(group => group.Key)
            .ToArray();
        var sufficient = trades.Count >= minimumTrades;

        return new TradingSetupAnalytics(
            setupName,
            trades.Count,
            _statistics.Mean(results),
            _statistics.Median(results),
            _statistics.Rate(trades.Count(TradingAnalyticsFacts.AdheredToPlan), trades.Count),
            _statistics.Rate(trades.Count(TradingAnalyticsFacts.HasRuleViolation), trades.Count),
            _statistics.Mean(risks),
            _statistics.Mean(trades.Select(trade => (decimal)trade.Scores.OverallProcessQuality)) ?? 0,
            mistakes,
            _statistics.Mean(trades.Select(trade => trade.Completeness)) ?? 0,
            sufficient,
            sufficient ? [] : ["The setup sample is too small for a reliable comparative interpretation."]);
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
}
