using TradeMind.AI.Agents;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics;

public interface ITradingJournalAnalyticsValidator
{
    TradingJournalAnalyticsValidationResult Validate(
        TradingJournalAnalyticsRequest request,
        TradingJournalAnalyticsOptions options,
        Guid analysisId);
}

public interface ITradingJournalCollectionNormalizer
{
    TradingJournalCollectionNormalizationResult Normalize(TradingJournalAnalyticsValidationResult validationResult);
}

public interface ITradingStatisticsCalculator
{
    decimal? Mean(IEnumerable<decimal> values);
    decimal? Median(IEnumerable<decimal> values);
    decimal? Minimum(IEnumerable<decimal> values);
    decimal? Maximum(IEnumerable<decimal> values);
    decimal Sum(IEnumerable<decimal> values);
    decimal Rate(int numerator, int denominator);
    TimeSpan? AverageDuration(IEnumerable<TimeSpan> values);
    TradingJournalAggregateMetrics CalculateAggregates(IReadOnlyList<TradingJournalTradeAnalysis> trades);
    HistoricalRDrawdown CalculateHistoricalDrawdown(IReadOnlyList<TradingJournalTradeAnalysis> trades);
    TradingStreakMetrics CalculateStreaks(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        TradingCoachProfile profile,
        decimal incompleteThreshold);
    IReadOnlyList<TradingScoreEvolution> CalculateScoreEvolution(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        IReadOnlyList<TradingPeriodAggregate> periods,
        int minimumTradesForTrend);
}

public interface ITradingPeriodAggregator
{
    IReadOnlyList<TradingPeriodAggregate> Group(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        TradingAnalyticsGroupingPeriod groupingPeriod,
        TradingJournalAnalyticsOptions options);
}

public interface ITradingSetupAnalyzer
{
    IReadOnlyList<TradingSetupAnalytics> Analyze(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        TradingJournalAnalyticsOptions options);
}

public interface ITradingBehaviorTrendAnalyzer
{
    IReadOnlyList<TradingBehaviorTrend> Analyze(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        int minimumTradesForTrend,
        int maximumPatterns);
}

public interface IRiskDriftAnalyzer
{
    RiskDriftAnalysis Analyze(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        TradingCoachProfile profile,
        int minimumTradesForTrend);
}

public interface IPostOutcomeBehaviorAnalyzer
{
    IReadOnlyList<PostOutcomeBehaviorObservation> Analyze(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        int minimumTradesForTrend,
        string requestedLanguage);
}

public interface ITradingJournalDataQualityAnalyzer
{
    TradingJournalDataQuality Analyze(
        TradingJournalAnalyticsValidationResult validation,
        TradingJournalCollectionNormalizationResult normalization,
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        TradingJournalAnalyticsOptions options);
}

public interface ITradingJournalRuleAnalyzer
{
    TradingJournalRuleAnalysisResult Analyze(
        TradingJournalAggregateMetrics aggregates,
        TradingStreakMetrics streaks,
        IReadOnlyList<TradingPeriodAggregate> periods,
        IReadOnlyList<TradingSetupAnalytics> setups,
        IReadOnlyList<TradingBehaviorTrend> behaviors,
        RiskDriftAnalysis riskDrift,
        IReadOnlyList<TradingScoreEvolution> scoreEvolution,
        TradingJournalDataQuality dataQuality,
        TradingCoachProfile profile);
}

public interface ITradingJournalAIInterpreter
{
    Task<TradingJournalAIInterpretation> InterpretAsync(
        TradingJournalAggregateMetrics aggregates,
        TradingJournalDataQuality dataQuality,
        IReadOnlyList<TradingBehaviorTrend> behaviors,
        RiskDriftAnalysis riskDrift,
        IReadOnlyList<TradingScoreEvolution> scoreEvolution,
        IReadOnlyList<TradingSetupAnalytics> setups,
        TradingJournalRuleAnalysisResult rules,
        TradingJournalAnalyticsRequest request,
        TradingJournalAnalyticsOptions options,
        Guid analysisId,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken);
}

public interface ITradingJournalAnalyticsResponseParser
{
    TradingJournalAIInterpretation Parse(string response, string requestedLanguage, Guid analysisId, string? correlationId = null);
}

public interface ITradingJournalAnalyticsMerger
{
    TradingJournalAnalyticsReport Merge(
        TradingJournalAnalyticsReport deterministicReport,
        TradingJournalRuleAnalysisResult rules,
        TradingJournalAIInterpretation? aiInterpretation,
        TradingJournalAnalyticsOptions options);
}

public interface ITradingJournalAnalyticsSafetyFilter
{
    TradingJournalAnalyticsReport Validate(TradingJournalAnalyticsReport report, string? correlationId = null);
}

public interface ITradingJournalAnalyticsService
{
    Task<TradingJournalAnalyticsReport> AnalyzeAsync(
        TradingJournalAnalyticsRequest request,
        CancellationToken cancellationToken);
}

public interface ITradingJournalAgentRequestFactory
{
    AIAgentExecutionRequest Create(
        TradingJournalAggregateMetrics aggregates,
        TradingJournalDataQuality dataQuality,
        IReadOnlyList<TradingBehaviorTrend> behaviors,
        RiskDriftAnalysis riskDrift,
        IReadOnlyList<TradingScoreEvolution> scoreEvolution,
        IReadOnlyList<TradingSetupAnalytics> setups,
        TradingJournalRuleAnalysisResult rules,
        TradingJournalAnalyticsRequest request,
        TradingJournalAnalyticsOptions options,
        Guid analysisId,
        DateTimeOffset requestedAtUtc);
}
