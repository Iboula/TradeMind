using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics;

public sealed class TradingJournalAnalyticsValidator : ITradingJournalAnalyticsValidator
{
    private readonly ITradingJournalValidator _tradeValidator;
    private readonly ITradingJournalNormalizer _tradeNormalizer;

    public TradingJournalAnalyticsValidator(
        ITradingJournalValidator tradeValidator,
        ITradingJournalNormalizer tradeNormalizer)
    {
        _tradeValidator = tradeValidator;
        _tradeNormalizer = tradeNormalizer;
    }

    public TradingJournalAnalyticsValidationResult Validate(
        TradingJournalAnalyticsRequest request,
        TradingJournalAnalyticsOptions options,
        Guid analysisId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        if (analysisId == Guid.Empty)
        {
            throw new ArgumentException("AnalysisId cannot be empty.", nameof(analysisId));
        }

        var globalErrors = ValidateGlobal(request, options);
        if (globalErrors.Count > 0)
        {
            throw new TradingJournalAnalyticsValidationException(analysisId, globalErrors, request.CorrelationId);
        }

        var candidates = new List<ValidatedTradingJournalTrade>(request.Trades.Count);
        var invalidIndexes = new List<int>();
        var invalidFields = new List<string>();
        var incompleteIndexes = new List<int>();
        var warnings = new List<string>();
        decimal completenessTotal = 0;

        for (var index = 0; index < request.Trades.Count; index++)
        {
            var trade = request.Trades[index];
            var normalization = _tradeNormalizer.Normalize(trade);
            completenessTotal += normalization.CompletenessScore;
            if (normalization.CompletenessScore < options.MinimumDataCompleteness)
            {
                incompleteIndexes.Add(index);
            }

            var isValid = true;
            try
            {
                _tradeValidator.Validate(trade, analysisId, request.CorrelationId);
            }
            catch (TradingJournalValidationException exception)
            {
                isValid = false;
                invalidIndexes.Add(index);
                invalidFields.AddRange(exception.InvalidFields.Select(field => $"Trades[{index}].{field}"));
            }

            if (!IsInRequestedRange(trade, request.DateRange))
            {
                warnings.Add($"Trades[{index}] was outside the requested date range and was excluded.");
                continue;
            }

            if (isValid || !options.ExcludeInvalidTradesFromAggregates)
            {
                candidates.Add(new ValidatedTradingJournalTrade(index, trade, normalization.CompletenessScore));
            }
        }

        if (invalidIndexes.Count > 0 && options.FailOnInvalidTrade)
        {
            throw new TradingJournalAnalyticsValidationException(analysisId, invalidFields, request.CorrelationId);
        }

        if (candidates.Count == 0)
        {
            throw new TradingJournalAnalyticsValidationException(
                analysisId,
                [nameof(request.Trades)],
                request.CorrelationId);
        }

        if (invalidIndexes.Count > 0)
        {
            warnings.Add($"{invalidIndexes.Count} invalid trade(s) were identified.");
        }

        if (incompleteIndexes.Count > 0)
        {
            warnings.Add($"{incompleteIndexes.Count} incomplete trade(s) were identified.");
        }

        var effectiveRange = EffectiveRange(candidates.Select(candidate => candidate.Trade), request.DateRange);
        return new TradingJournalAnalyticsValidationResult(
            candidates,
            invalidIndexes,
            incompleteIndexes,
            warnings,
            effectiveRange,
            Round(completenessTotal / request.Trades.Count),
            request.Trades.Count);
    }

    private static List<string> ValidateGlobal(
        TradingJournalAnalyticsRequest request,
        TradingJournalAnalyticsOptions options)
    {
        var errors = new List<string>();
        if (request.Trades.Count > options.MaximumTradesPerAnalysis)
        {
            errors.Add(nameof(request.Trades));
        }

        if (request.MinimumTradesForTrend > options.MaximumTradesPerAnalysis)
        {
            errors.Add(nameof(request.MinimumTradesForTrend));
        }

        if (options.IncludeMemory && string.IsNullOrWhiteSpace(request.SessionId))
        {
            errors.Add(nameof(request.SessionId));
        }

        return errors;
    }

    private static bool IsInRequestedRange(TradingJournalAnalysisRequest trade, TradingDateRange requestedRange)
    {
        var timestamp = trade.OpenedAtUtc ?? trade.ClosedAtUtc;
        if (timestamp is null)
        {
            return requestedRange.FromUtc is null && requestedRange.ToUtc is null;
        }

        return (requestedRange.FromUtc is null || timestamp >= requestedRange.FromUtc)
            && (requestedRange.ToUtc is null || timestamp <= requestedRange.ToUtc);
    }

    private static TradingDateRange EffectiveRange(
        IEnumerable<TradingJournalAnalysisRequest> trades,
        TradingDateRange requestedRange)
    {
        var timestamps = trades
            .Select(trade => trade.OpenedAtUtc ?? trade.ClosedAtUtc)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .OrderBy(value => value)
            .ToArray();

        return new TradingDateRange(
            requestedRange.FromUtc ?? timestamps.FirstOrDefaultNullable(),
            requestedRange.ToUtc ?? timestamps.LastOrDefaultNullable());
    }

    private static decimal Round(decimal value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);
}

internal static class AnalyticsEnumerableExtensions
{
    public static T? FirstOrDefaultNullable<T>(this IReadOnlyList<T> values) where T : struct =>
        values.Count == 0 ? null : values[0];

    public static T? LastOrDefaultNullable<T>(this IReadOnlyList<T> values) where T : struct =>
        values.Count == 0 ? null : values[^1];
}
