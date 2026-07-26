namespace TradeMind.Trading.Analytics;

public sealed class TradingJournalDataQualityAnalyzer : ITradingJournalDataQualityAnalyzer
{
    public TradingJournalDataQuality Analyze(
        TradingJournalAnalyticsValidationResult validation,
        TradingJournalCollectionNormalizationResult normalization,
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        TradingJournalAnalyticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(normalization);
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(options);

        var missing = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var trade in trades)
        {
            CountMissing(trade.Trade.SetupName, nameof(trade.Trade.SetupName), missing);
            CountMissing(trade.Trade.PlanBeforeTrade, nameof(trade.Trade.PlanBeforeTrade), missing);
            CountMissing(trade.Trade.EntryReason, nameof(trade.Trade.EntryReason), missing);
            CountMissing(trade.Trade.ExitReason, nameof(trade.Trade.ExitReason), missing);
            CountMissing(trade.Trade.ExecutionNotes, nameof(trade.Trade.ExecutionNotes), missing);
            CountMissing(trade.Trade.StopLoss, nameof(trade.Trade.StopLoss), missing);
            CountMissing(trade.Trade.ActualRiskPercentage, nameof(trade.Trade.ActualRiskPercentage), missing);
            CountMissing(trade.Trade.ResultRMultiple, nameof(trade.Trade.ResultRMultiple), missing);
            CountMissing(trade.Trade.OpenedAtUtc, nameof(trade.Trade.OpenedAtUtc), missing);
            CountMissing(trade.Trade.ClosedAtUtc, nameof(trade.Trade.ClosedAtUtc), missing);
        }

        var timestamps = trades.Select(trade => trade.Trade.OpenedAtUtc ?? trade.Trade.ClosedAtUtc)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .OrderBy(value => value)
            .ToArray();
        var coverage = new TradingDateRange(timestamps.FirstOrDefaultNullable(), timestamps.LastOrDefaultNullable());
        var completeness = trades.Count == 0 ? 0 : Math.Round(trades.Average(trade => trade.Completeness), 6, MidpointRounding.AwayFromZero);
        var consistencyRate = validation.TotalTradeCount == 0
            ? 0
            : 1m - (validation.InvalidTradeIndexes.Count + normalization.DuplicateCount) / (decimal)validation.TotalTradeCount;
        var coverageDays = coverage.FromUtc is null || coverage.ToUtc is null
            ? 0
            : (coverage.ToUtc.Value - coverage.FromUtc.Value).TotalDays;
        var confidenceScore = Math.Min(1m, trades.Count / (decimal)Math.Max(options.MinimumTradesForTrend, 1)) * 0.35m
            + completeness / 100m * 0.35m
            + Math.Clamp(consistencyRate, 0, 1) * 0.2m
            + (coverageDays >= 7 ? 0.1m : coverageDays > 0 ? 0.05m : 0);
        var confidence = confidenceScore switch
        {
            >= 0.8m => TradingDataConfidenceLevel.High,
            >= 0.55m => TradingDataConfidenceLevel.Moderate,
            _ => TradingDataConfidenceLevel.Low
        };

        var limitations = new List<string>();
        if (trades.Count < options.MinimumTradesForTrend)
        {
            limitations.Add("The supplied trade count is below the configured trend threshold.");
        }

        if (completeness < options.MinimumDataCompleteness)
        {
            limitations.Add("Overall journal completeness is below the configured threshold.");
        }

        if (normalization.DuplicateCount > 0)
        {
            limitations.Add("Duplicate records reduced the effective sample size.");
        }

        if (timestamps.Length < trades.Count)
        {
            limitations.Add("Some trades lack a timestamp and cannot contribute to period analysis.");
        }

        return new TradingJournalDataQuality(
            completeness,
            validation.TotalTradeCount - validation.InvalidTradeIndexes.Count,
            validation.InvalidTradeIndexes.Count,
            validation.IncompleteTradeIndexes.Count,
            missing,
            normalization.DuplicateCount,
            coverage,
            confidence,
            limitations);
    }

    private static void CountMissing<T>(T? value, string field, IDictionary<string, int> missing)
    {
        if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
        {
            missing[field] = missing.TryGetValue(field, out var count) ? count + 1 : 1;
        }
    }
}
