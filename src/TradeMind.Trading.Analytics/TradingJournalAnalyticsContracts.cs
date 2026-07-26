using System.Collections.ObjectModel;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics;

public static class TradingJournalAnalyticsConstants
{
    public const string AgentId = "journal-analysis";
    public const string AgentVersion = "1.0.0";
    public const string Scenario = "TradingJournalAnalytics";
    public const string Disclaimer = "Educational historical process analysis only. This report is descriptive, is not financial advice or a prediction, and is not an instruction to trade.";
}

public enum TradingAnalyticsGroupingPeriod
{
    None,
    Day,
    Week,
    Month
}

public enum TradingTrendDirection
{
    Improving,
    Stable,
    Worsening,
    InsufficientData
}

public enum TradingAnalyticsSeverity
{
    Information,
    Warning,
    Critical
}

public enum TradingDataConfidenceLevel
{
    Low,
    Moderate,
    High
}

public sealed record TradingDateRange
{
    public TradingDateRange(DateTimeOffset? fromUtc, DateTimeOffset? toUtc)
    {
        ValidateUtc(fromUtc, nameof(fromUtc));
        ValidateUtc(toUtc, nameof(toUtc));
        if (fromUtc is not null && toUtc is not null && toUtc < fromUtc)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(toUtc));
        }

        FromUtc = fromUtc;
        ToUtc = toUtc;
    }

    public DateTimeOffset? FromUtc { get; }
    public DateTimeOffset? ToUtc { get; }

    private static void ValidateUtc(DateTimeOffset? value, string parameterName)
    {
        if (value is not null && value.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Date values must use the UTC offset.", parameterName);
        }
    }
}

public sealed record TradingJournalAnalyticsRequest
{
    public TradingJournalAnalyticsRequest(
        IReadOnlyCollection<TradingJournalAnalysisRequest> trades,
        TradingCoachProfile profile,
        DateTimeOffset? dateFromUtc = null,
        DateTimeOffset? dateToUtc = null,
        TradingAnalyticsGroupingPeriod groupingPeriod = TradingAnalyticsGroupingPeriod.Week,
        int minimumTradesForTrend = 5,
        bool includePerSetupAnalysis = true,
        bool includeBehaviorAnalysis = true,
        bool includeScoreEvolution = true,
        string requestedLanguage = "en",
        string? correlationId = null,
        string? sessionId = null,
        string? tenantId = null,
        string? userId = null)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedLanguage);
        if (trades.Count == 0)
        {
            throw new ArgumentException("At least one trade is required.", nameof(trades));
        }

        if (trades.Any(trade => trade is null))
        {
            throw new ArgumentException("Trades cannot contain null values.", nameof(trades));
        }

        if (minimumTradesForTrend < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumTradesForTrend), "At least two trades are required for a trend.");
        }

        DateRange = new TradingDateRange(dateFromUtc, dateToUtc);
        Trades = Array.AsReadOnly(trades.ToArray());
        Profile = profile;
        GroupingPeriod = groupingPeriod;
        MinimumTradesForTrend = minimumTradesForTrend;
        IncludePerSetupAnalysis = includePerSetupAnalysis;
        IncludeBehaviorAnalysis = includeBehaviorAnalysis;
        IncludeScoreEvolution = includeScoreEvolution;
        RequestedLanguage = requestedLanguage.Trim();
        CorrelationId = AnalyticsCollections.Normalize(correlationId);
        SessionId = AnalyticsCollections.Normalize(sessionId);
        TenantId = AnalyticsCollections.Normalize(tenantId);
        UserId = AnalyticsCollections.Normalize(userId);
    }

    public IReadOnlyList<TradingJournalAnalysisRequest> Trades { get; }
    public TradingCoachProfile Profile { get; }
    public DateTimeOffset? DateFromUtc => DateRange.FromUtc;
    public DateTimeOffset? DateToUtc => DateRange.ToUtc;
    public TradingDateRange DateRange { get; }
    public TradingAnalyticsGroupingPeriod GroupingPeriod { get; }
    public int MinimumTradesForTrend { get; }
    public bool IncludePerSetupAnalysis { get; }
    public bool IncludeBehaviorAnalysis { get; }
    public bool IncludeScoreEvolution { get; }
    public string RequestedLanguage { get; }
    public string? CorrelationId { get; }
    public string? SessionId { get; }
    public string? TenantId { get; }
    public string? UserId { get; }
}

public sealed record ValidatedTradingJournalTrade(
    int OriginalIndex,
    TradingJournalAnalysisRequest Trade,
    decimal Completeness);

public sealed record TradingJournalAnalyticsValidationResult
{
    public TradingJournalAnalyticsValidationResult(
        IReadOnlyCollection<ValidatedTradingJournalTrade> validTrades,
        IReadOnlyCollection<int> invalidTradeIndexes,
        IReadOnlyCollection<int> incompleteTradeIndexes,
        IReadOnlyCollection<string> warnings,
        TradingDateRange effectiveDateRange,
        decimal dataCompleteness,
        int totalTradeCount)
    {
        ArgumentNullException.ThrowIfNull(effectiveDateRange);
        if (dataCompleteness is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(dataCompleteness));
        }

        if (totalTradeCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTradeCount));
        }

        ValidTrades = Array.AsReadOnly(validTrades?.ToArray() ?? []);
        InvalidTradeIndexes = AnalyticsCollections.CopyOrderedIndexes(invalidTradeIndexes);
        IncompleteTradeIndexes = AnalyticsCollections.CopyOrderedIndexes(incompleteTradeIndexes);
        Warnings = AnalyticsCollections.CopyStrings(warnings);
        EffectiveDateRange = effectiveDateRange;
        DataCompleteness = dataCompleteness;
        TotalTradeCount = totalTradeCount;
    }

    public IReadOnlyList<ValidatedTradingJournalTrade> ValidTrades { get; }
    public IReadOnlyList<int> InvalidTradeIndexes { get; }
    public IReadOnlyList<int> IncompleteTradeIndexes { get; }
    public IReadOnlyList<string> Warnings { get; }
    public TradingDateRange EffectiveDateRange { get; }
    public decimal DataCompleteness { get; }
    public int TotalTradeCount { get; }
}

public sealed record NormalizedTradingJournalTrade(
    int OriginalIndex,
    TradingJournalNormalizationResult Normalization,
    string DeduplicationKey);

public sealed record TradingJournalCollectionNormalizationResult
{
    public TradingJournalCollectionNormalizationResult(
        IReadOnlyCollection<NormalizedTradingJournalTrade> trades,
        int duplicateCount,
        IReadOnlyCollection<int>? conflictingDuplicateIndexes,
        IReadOnlyCollection<string>? warnings,
        int derivedFieldCount)
    {
        if (duplicateCount < 0 || derivedFieldCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(duplicateCount));
        }

        Trades = Array.AsReadOnly(trades?.ToArray() ?? []);
        DuplicateCount = duplicateCount;
        ConflictingDuplicateIndexes = AnalyticsCollections.CopyOrderedIndexes(conflictingDuplicateIndexes);
        Warnings = AnalyticsCollections.CopyStrings(warnings);
        DerivedFieldCount = derivedFieldCount;
    }

    public IReadOnlyList<NormalizedTradingJournalTrade> Trades { get; }
    public int DuplicateCount { get; }
    public IReadOnlyList<int> ConflictingDuplicateIndexes { get; }
    public IReadOnlyList<string> Warnings { get; }
    public int DerivedFieldCount { get; }
}

public sealed record TradingJournalTradeAnalysis
{
    public TradingJournalTradeAnalysis(
        NormalizedTradingJournalTrade normalizedTrade,
        TradeMetricsResult metrics,
        TradingCoachRuleAnalysisResult coachingAnalysis,
        IReadOnlyCollection<string>? behaviors)
    {
        ArgumentNullException.ThrowIfNull(normalizedTrade);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(coachingAnalysis);
        NormalizedTrade = normalizedTrade;
        Metrics = metrics;
        CoachingAnalysis = coachingAnalysis;
        Behaviors = AnalyticsCollections.CopyStrings(behaviors);
    }

    public NormalizedTradingJournalTrade NormalizedTrade { get; }
    public int OriginalIndex => NormalizedTrade.OriginalIndex;
    public TradingJournalAnalysisRequest Trade => NormalizedTrade.Normalization.NormalizedRequest;
    public decimal Completeness => NormalizedTrade.Normalization.CompletenessScore;
    public TradeMetricsResult Metrics { get; }
    public TradingCoachRuleAnalysisResult CoachingAnalysis { get; }
    public TradingCoachScores Scores => CoachingAnalysis.Scores;
    public IReadOnlyList<string> Behaviors { get; }
}

internal static class AnalyticsCollections
{
    public static IReadOnlyList<string> CopyStrings(IEnumerable<string>? values) => Array.AsReadOnly(
        values?.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray() ?? []);

    public static IReadOnlyList<int> CopyOrderedIndexes(IEnumerable<int>? values) => Array.AsReadOnly(
        values?.Distinct().OrderBy(value => value).ToArray() ?? []);

    public static IReadOnlyDictionary<string, int> CopyDictionary(IReadOnlyDictionary<string, int>? values) =>
        new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(values ?? new Dictionary<string, int>(), StringComparer.Ordinal));

    public static IReadOnlyDictionary<string, decimal> CopyDictionary(IReadOnlyDictionary<string, decimal>? values) =>
        new ReadOnlyDictionary<string, decimal>(new Dictionary<string, decimal>(values ?? new Dictionary<string, decimal>(), StringComparer.Ordinal));

    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
