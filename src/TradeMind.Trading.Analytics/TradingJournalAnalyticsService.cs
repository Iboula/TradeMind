using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.Agents;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics;

public sealed class TradingJournalAnalyticsService : ITradingJournalAnalyticsService
{
    private readonly ITradingJournalAnalyticsValidator _validator;
    private readonly ITradingJournalCollectionNormalizer _normalizer;
    private readonly ITradeMetricsCalculator _tradeMetricsCalculator;
    private readonly ITradingBehaviorPatternDetector _patternDetector;
    private readonly ITradingCoachRuleAnalyzer _coachingRuleAnalyzer;
    private readonly ITradingStatisticsCalculator _statistics;
    private readonly ITradingPeriodAggregator _periodAggregator;
    private readonly ITradingSetupAnalyzer _setupAnalyzer;
    private readonly ITradingBehaviorTrendAnalyzer _behaviorAnalyzer;
    private readonly IRiskDriftAnalyzer _riskDriftAnalyzer;
    private readonly IPostOutcomeBehaviorAnalyzer _postOutcomeAnalyzer;
    private readonly ITradingJournalDataQualityAnalyzer _dataQualityAnalyzer;
    private readonly ITradingJournalRuleAnalyzer _ruleAnalyzer;
    private readonly ITradingJournalAIInterpreter _aiInterpreter;
    private readonly ITradingJournalAnalyticsMerger _merger;
    private readonly ITradingJournalAnalyticsSafetyFilter _safetyFilter;
    private readonly TradingJournalAnalyticsOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TradingJournalAnalyticsService> _logger;

    public TradingJournalAnalyticsService(
        ITradingJournalAnalyticsValidator validator,
        ITradingJournalCollectionNormalizer normalizer,
        ITradeMetricsCalculator tradeMetricsCalculator,
        ITradingBehaviorPatternDetector patternDetector,
        ITradingCoachRuleAnalyzer coachingRuleAnalyzer,
        ITradingStatisticsCalculator statistics,
        ITradingPeriodAggregator periodAggregator,
        ITradingSetupAnalyzer setupAnalyzer,
        ITradingBehaviorTrendAnalyzer behaviorAnalyzer,
        IRiskDriftAnalyzer riskDriftAnalyzer,
        IPostOutcomeBehaviorAnalyzer postOutcomeAnalyzer,
        ITradingJournalDataQualityAnalyzer dataQualityAnalyzer,
        ITradingJournalRuleAnalyzer ruleAnalyzer,
        ITradingJournalAIInterpreter aiInterpreter,
        ITradingJournalAnalyticsMerger merger,
        ITradingJournalAnalyticsSafetyFilter safetyFilter,
        IOptions<TradingJournalAnalyticsOptions> options,
        TimeProvider timeProvider,
        ILogger<TradingJournalAnalyticsService> logger)
    {
        _validator = validator;
        _normalizer = normalizer;
        _tradeMetricsCalculator = tradeMetricsCalculator;
        _patternDetector = patternDetector;
        _coachingRuleAnalyzer = coachingRuleAnalyzer;
        _statistics = statistics;
        _periodAggregator = periodAggregator;
        _setupAnalyzer = setupAnalyzer;
        _behaviorAnalyzer = behaviorAnalyzer;
        _riskDriftAnalyzer = riskDriftAnalyzer;
        _postOutcomeAnalyzer = postOutcomeAnalyzer;
        _dataQualityAnalyzer = dataQualityAnalyzer;
        _ruleAnalyzer = ruleAnalyzer;
        _aiInterpreter = aiInterpreter;
        _merger = merger;
        _safetyFilter = safetyFilter;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<TradingJournalAnalyticsReport> AnalyzeAsync(
        TradingJournalAnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var analysisId = Guid.NewGuid();
        var started = _timeProvider.GetTimestamp();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            LogPhase("validation", analysisId, request, request.Trades.Count, 0, 0);
            var validation = _validator.Validate(request, _options, analysisId);
            LogPhase("normalization", analysisId, request, validation.ValidTrades.Count, validation.InvalidTradeIndexes.Count, 0);
            var normalization = _normalizer.Normalize(validation);
            _logger.LogInformation(
                "Trading journal analytics deduplication completed for analysis {AnalysisId}, correlation {CorrelationId}, tenant {TenantId}, user {UserId}, trade count {TradeCount}, valid count {ValidTradeCount}, invalid count {InvalidTradeCount}, and duplicate count {DuplicateCount}",
                analysisId, request.CorrelationId, request.TenantId, request.UserId, request.Trades.Count,
                normalization.Trades.Count, validation.InvalidTradeIndexes.Count, normalization.DuplicateCount);

            cancellationToken.ThrowIfCancellationRequested();
            var analyzedTrades = normalization.Trades.Select(trade => AnalyzeTrade(trade, request.Profile)).ToArray();
            LogPhase("metrics", analysisId, request, analyzedTrades.Length, validation.InvalidTradeIndexes.Count, normalization.DuplicateCount);

            var aggregates = _statistics.CalculateAggregates(analyzedTrades);
            var drawdown = _statistics.CalculateHistoricalDrawdown(analyzedTrades);
            var streaks = _statistics.CalculateStreaks(analyzedTrades, request.Profile, _options.MinimumDataCompleteness);
            var periods = _periodAggregator.Group(analyzedTrades, request.GroupingPeriod, _options);
            _logger.LogInformation(
                "Trading journal analytics grouping completed for analysis {AnalysisId}, correlation {CorrelationId}, period count {PeriodCount}, and trade count {TradeCount}",
                analysisId, request.CorrelationId, periods.Count, analyzedTrades.Length);

            var setups = request.IncludePerSetupAnalysis
                ? _setupAnalyzer.Analyze(analyzedTrades, _options)
                : [];
            _logger.LogInformation(
                "Trading journal analytics setup analysis completed for analysis {AnalysisId}, correlation {CorrelationId}, and setup count {SetupCount}",
                analysisId, request.CorrelationId, setups.Count);

            var minimumForTrend = Math.Max(request.MinimumTradesForTrend, _options.MinimumTradesForTrend);
            var behaviors = request.IncludeBehaviorAnalysis
                ? _behaviorAnalyzer.Analyze(analyzedTrades, minimumForTrend, _options.MaximumBehaviorPatterns)
                : [];
            _logger.LogInformation(
                "Trading journal analytics behavior analysis completed for analysis {AnalysisId}, correlation {CorrelationId}, and behavior count {BehaviorCount}",
                analysisId, request.CorrelationId, behaviors.Count);

            var riskDrift = _riskDriftAnalyzer.Analyze(analyzedTrades, request.Profile, minimumForTrend);
            _logger.LogInformation(
                "Trading journal analytics risk drift completed for analysis {AnalysisId}, correlation {CorrelationId}, drift detected {DriftDetected}, and confidence {Confidence}",
                analysisId, request.CorrelationId, riskDrift.DriftDetected, riskDrift.Confidence);
            var postOutcome = _postOutcomeAnalyzer.Analyze(analyzedTrades, minimumForTrend, request.RequestedLanguage);
            var scoreEvolution = request.IncludeScoreEvolution
                ? _statistics.CalculateScoreEvolution(analyzedTrades, periods, minimumForTrend)
                : [];
            _logger.LogInformation(
                "Trading journal analytics score evolution completed for analysis {AnalysisId}, correlation {CorrelationId}, and score count {ScoreCount}",
                analysisId, request.CorrelationId, scoreEvolution.Count);

            var dataQuality = _dataQualityAnalyzer.Analyze(validation, normalization, analyzedTrades, _options);
            _logger.LogInformation(
                "Trading journal analytics data quality completed for analysis {AnalysisId}, correlation {CorrelationId}, valid count {ValidTradeCount}, invalid count {InvalidTradeCount}, duplicate count {DuplicateCount}, and completeness {Completeness}",
                analysisId, request.CorrelationId, dataQuality.ValidTradeCount, dataQuality.InvalidTradeCount,
                dataQuality.DuplicateCount, dataQuality.OverallCompleteness);

            var rules = _ruleAnalyzer.Analyze(
                aggregates, streaks, periods, setups, behaviors, riskDrift, scoreEvolution,
                dataQuality, request.Profile);
            var generatedAtUtc = _timeProvider.GetUtcNow();
            var deterministicReport = CreateDeterministicReport(
                analysisId, validation.EffectiveDateRange, dataQuality, aggregates, drawdown, streaks,
                periods, setups, behaviors, riskDrift, postOutcome, scoreEvolution, rules, generatedAtUtc);

            TradingJournalAIInterpretation? interpretation = null;
            if (_options.IncludeAIInterpretation)
            {
                _logger.LogInformation(
                    "Trading journal analytics agent execution started for analysis {AnalysisId}, agent {AgentId}, version {AgentVersion}, correlation {CorrelationId}, tenant {TenantId}, and user {UserId}",
                    analysisId, TradingJournalAnalyticsConstants.AgentId, TradingJournalAnalyticsConstants.AgentVersion,
                    request.CorrelationId, request.TenantId, request.UserId);
                interpretation = await _aiInterpreter.InterpretAsync(
                    aggregates, dataQuality, behaviors, riskDrift, scoreEvolution, setups, rules,
                    request, _options, analysisId, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Trading journal analytics parsing completed for analysis {AnalysisId}, correlation {CorrelationId}, and AI interpretation used {AIInterpretationUsed}",
                    analysisId, request.CorrelationId, true);
            }

            var merged = _merger.Merge(deterministicReport, rules, interpretation, _options);
            _logger.LogInformation(
                "Trading journal analytics merge completed for analysis {AnalysisId}, correlation {CorrelationId}, recommendation count {RecommendationCount}, and AI interpretation used {AIInterpretationUsed}",
                analysisId, request.CorrelationId, merged.Recommendations.Count, interpretation is not null);
            var safe = _safetyFilter.Validate(merged, request.CorrelationId);
            _logger.LogInformation(
                "Trading journal analytics safety completed for analysis {AnalysisId}, correlation {CorrelationId}",
                analysisId, request.CorrelationId);
            _logger.LogInformation(
                "Trading journal analytics completed for analysis {AnalysisId}, correlation {CorrelationId}, tenant {TenantId}, user {UserId}, trade count {TradeCount}, valid count {ValidTradeCount}, invalid count {InvalidTradeCount}, duplicate count {DuplicateCount}, period count {PeriodCount}, setup count {SetupCount}, behavior count {BehaviorCount}, AI interpretation used {AIInterpretationUsed}, and duration {Duration}",
                analysisId, request.CorrelationId, request.TenantId, request.UserId, request.Trades.Count,
                dataQuality.ValidTradeCount, dataQuality.InvalidTradeCount, dataQuality.DuplicateCount,
                periods.Count, setups.Count, behaviors.Count, interpretation is not null, _timeProvider.GetElapsedTime(started));
            return safe;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Trading journal analytics cancelled for analysis {AnalysisId} and correlation {CorrelationId}",
                analysisId, request.CorrelationId);
            throw;
        }
        catch (AIAgentTimeoutException exception)
        {
            LogFailure(analysisId, request, "JOURNAL_ANALYTICS_TIMEOUT");
            throw new TradingJournalAnalyticsTimeoutException(analysisId, request.CorrelationId, exception);
        }
        catch (Exception exception) when (exception is TradingJournalAnalyticsException)
        {
            LogFailure(analysisId, request, ((TradingJournalAnalyticsException)exception).ErrorCode);
            throw;
        }
        catch (Exception exception)
        {
            LogFailure(analysisId, request, "JOURNAL_ANALYTICS_FAILED");
            throw new TradingJournalAnalyticsAnalysisException(analysisId, request.CorrelationId, exception);
        }
    }

    private TradingJournalTradeAnalysis AnalyzeTrade(
        NormalizedTradingJournalTrade trade,
        TradingCoachProfile profile)
    {
        var metrics = _tradeMetricsCalculator.Calculate(trade.Normalization);
        var coaching = _coachingRuleAnalyzer.Analyze(trade.Normalization, metrics, profile);
        var behaviors = _patternDetector.Detect(trade.Normalization.NormalizedRequest);
        return new TradingJournalTradeAnalysis(trade, metrics, coaching, behaviors);
    }

    private static TradingJournalAnalyticsReport CreateDeterministicReport(
        Guid analysisId,
        TradingDateRange dateRange,
        TradingJournalDataQuality dataQuality,
        TradingJournalAggregateMetrics aggregates,
        HistoricalRDrawdown drawdown,
        TradingStreakMetrics streaks,
        IReadOnlyList<TradingPeriodAggregate> periods,
        IReadOnlyList<TradingSetupAnalytics> setups,
        IReadOnlyList<TradingBehaviorTrend> behaviors,
        RiskDriftAnalysis riskDrift,
        IReadOnlyList<PostOutcomeBehaviorObservation> postOutcome,
        IReadOnlyList<TradingScoreEvolution> scoreEvolution,
        TradingJournalRuleAnalysisResult rules,
        DateTimeOffset generatedAtUtc) => new(
            analysisId,
            $"Deterministic historical process analysis of {aggregates.TradeCount} supplied trade(s).",
            ["All numeric metrics, streaks, trends, and scores were calculated deterministically from supplied data."],
            dateRange,
            dataQuality,
            aggregates,
            drawdown,
            streaks,
            periods,
            setups,
            behaviors,
            riskDrift,
            postOutcome,
            scoreEvolution,
            rules.Strengths,
            rules.Findings,
            rules.Recommendations,
            rules.Checklist,
            TradingJournalAnalyticsConstants.Disclaimer,
            generatedAtUtc,
            null,
            false);

    private void LogPhase(
        string phase,
        Guid analysisId,
        TradingJournalAnalyticsRequest request,
        int validCount,
        int invalidCount,
        int duplicateCount) => _logger.LogInformation(
            "Trading journal analytics phase {Phase} for analysis {AnalysisId}, correlation {CorrelationId}, tenant {TenantId}, user {UserId}, trade count {TradeCount}, valid count {ValidTradeCount}, invalid count {InvalidTradeCount}, and duplicate count {DuplicateCount}",
            phase, analysisId, request.CorrelationId, request.TenantId, request.UserId,
            request.Trades.Count, validCount, invalidCount, duplicateCount);

    private void LogFailure(Guid analysisId, TradingJournalAnalyticsRequest request, string errorCode) =>
        _logger.LogWarning(
            "Trading journal analytics failed for analysis {AnalysisId}, correlation {CorrelationId}, tenant {TenantId}, user {UserId}, trade count {TradeCount}, and error code {ErrorCode}",
            analysisId, request.CorrelationId, request.TenantId, request.UserId, request.Trades.Count, errorCode);
}
