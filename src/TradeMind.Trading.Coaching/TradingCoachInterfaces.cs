namespace TradeMind.Trading.Coaching;

public interface ITradingJournalValidator
{
    void Validate(TradingJournalAnalysisRequest request, Guid analysisId, string? correlationId = null);
}

public interface ITradingJournalNormalizer
{
    TradingJournalNormalizationResult Normalize(TradingJournalAnalysisRequest request);
}

public interface ITradeMetricsCalculator
{
    TradeMetricsResult Calculate(TradingJournalNormalizationResult normalizationResult);
}

public interface ITradingBehaviorPatternDetector
{
    IReadOnlyList<string> Detect(TradingJournalAnalysisRequest request);
}

public interface ITradingCoachScoreCalculator
{
    TradingCoachScores Calculate(
        TradingJournalNormalizationResult normalizationResult,
        TradeMetricsResult metrics,
        IReadOnlyCollection<TradingCoachFinding> findings);
}

public interface ITradingCoachRuleAnalyzer
{
    TradingCoachRuleAnalysisResult Analyze(
        TradingJournalNormalizationResult normalizationResult,
        TradeMetricsResult metrics,
        TradingCoachProfile profile);
}

public interface ITradingCoachResponseParser
{
    TradingCoachAnalysis Parse(
        string response,
        Guid analysisId,
        DateTimeOffset generatedAtUtc,
        string agentVersion,
        string? correlationId = null);
}

public interface ITradingCoachAnalysisMerger
{
    TradingCoachAnalysis Merge(
        TradingCoachAnalysis aiAnalysis,
        TradingCoachRuleAnalysisResult ruleAnalysis,
        TradingJournalNormalizationResult normalizationResult,
        TradeMetricsResult metrics,
        TradingCoachExecutionOptions options);
}

public interface ITradingCoachSafetyFilter
{
    TradingCoachAnalysis Validate(
        TradingCoachAnalysis analysis,
        string? correlationId = null);
}

public interface ITradingCoachService
{
    Task<TradingCoachAnalysis> AnalyzeAsync(
        TradingJournalAnalysisRequest request,
        TradingCoachProfile profile,
        TradingCoachExecutionOptions options,
        CancellationToken cancellationToken);
}
