using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.Agents;
using TradeMind.AI.Application;

namespace TradeMind.Trading.Coaching;

public sealed class TradingCoachService : ITradingCoachService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly AIAgentId AgentId = new(TradingCoachConstants.AgentId);
    private static readonly AIAgentVersion AgentVersion = new(1, 0, 0);

    private readonly ITradingJournalValidator _validator;
    private readonly ITradingJournalNormalizer _normalizer;
    private readonly ITradeMetricsCalculator _metricsCalculator;
    private readonly ITradingCoachRuleAnalyzer _ruleAnalyzer;
    private readonly IAIAgentExecutor _agentExecutor;
    private readonly ITradingCoachResponseParser _responseParser;
    private readonly ITradingCoachAnalysisMerger _analysisMerger;
    private readonly ITradingCoachSafetyFilter _safetyFilter;
    private readonly TradingJournalValidationOptions _validationOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TradingCoachService> _logger;

    public TradingCoachService(
        ITradingJournalValidator validator,
        ITradingJournalNormalizer normalizer,
        ITradeMetricsCalculator metricsCalculator,
        ITradingCoachRuleAnalyzer ruleAnalyzer,
        IAIAgentExecutor agentExecutor,
        ITradingCoachResponseParser responseParser,
        ITradingCoachAnalysisMerger analysisMerger,
        ITradingCoachSafetyFilter safetyFilter,
        IOptions<TradingJournalValidationOptions> validationOptions,
        TimeProvider timeProvider,
        ILogger<TradingCoachService> logger)
    {
        _validator = validator;
        _normalizer = normalizer;
        _metricsCalculator = metricsCalculator;
        _ruleAnalyzer = ruleAnalyzer;
        _agentExecutor = agentExecutor;
        _responseParser = responseParser;
        _analysisMerger = analysisMerger;
        _safetyFilter = safetyFilter;
        _validationOptions = validationOptions?.Value ?? throw new ArgumentNullException(nameof(validationOptions));
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<TradingCoachAnalysis> AnalyzeAsync(
        TradingJournalAnalysisRequest request,
        TradingCoachProfile profile,
        TradingCoachExecutionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(options);
        var analysisId = Guid.NewGuid();
        var started = _timeProvider.GetTimestamp();

        try
        {
            _logger.LogInformation("Trading coach validation started for analysis {AnalysisId} and correlation {CorrelationId}", analysisId, options.CorrelationId);
            _validator.Validate(request, analysisId, options.CorrelationId);
            _logger.LogInformation("Trading coach validation completed for analysis {AnalysisId} and correlation {CorrelationId}", analysisId, options.CorrelationId);

            var normalization = _normalizer.Normalize(request);
            _logger.LogInformation(
                "Trading coach normalization completed for analysis {AnalysisId}, correlation {CorrelationId}, completeness {CompletenessScore}, and derived field count {DerivedFieldCount}",
                analysisId, options.CorrelationId, normalization.CompletenessScore, normalization.DerivedFields.Count);

            if (options.FailOnIncompleteData && normalization.CompletenessScore < _validationOptions.MinimumCompletenessScore)
            {
                throw new TradingJournalValidationException(analysisId, ["JournalCompleteness"], options.CorrelationId);
            }

            var metrics = _metricsCalculator.Calculate(normalization);
            _logger.LogInformation(
                "Trading coach metrics computed for analysis {AnalysisId}, correlation {CorrelationId}, computed count {ComputedMetricCount}, and unavailable count {UnavailableMetricCount}",
                analysisId, options.CorrelationId, metrics.ComputedMetrics.Count, metrics.UnavailableMetrics.Count);

            var rules = _ruleAnalyzer.Analyze(normalization, metrics, profile);
            _logger.LogInformation(
                "Trading coach rule analysis completed for analysis {AnalysisId}, correlation {CorrelationId}, and violation count {ViolationCount}",
                analysisId, options.CorrelationId, rules.Findings.Count);

            var executionRequest = CreateAgentRequest(
                normalization,
                metrics,
                rules,
                profile,
                options,
                analysisId,
                _timeProvider.GetUtcNow());
            _logger.LogInformation(
                "Trading coach agent execution started for analysis {AnalysisId}, agent {AgentId}, version {AgentVersion}, session {SessionId}, conversation {ConversationId}, correlation {CorrelationId}, tenant {TenantId}, and user {UserId}",
                analysisId, TradingCoachConstants.AgentId, TradingCoachConstants.AgentVersion, options.SessionId,
                options.ConversationId, options.CorrelationId, options.TenantId, options.UserId);
            var agentResponse = await _agentExecutor.ExecuteAsync(executionRequest, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Trading coach agent execution completed for analysis {AnalysisId}, agent {AgentId}, version {AgentVersion}, correlation {CorrelationId}, and duration {Duration}",
                analysisId, TradingCoachConstants.AgentId, TradingCoachConstants.AgentVersion,
                options.CorrelationId, agentResponse.Duration);

            var parsed = _responseParser.Parse(
                agentResponse.Content,
                analysisId,
                _timeProvider.GetUtcNow(),
                TradingCoachConstants.AgentVersion,
                options.CorrelationId);
            _logger.LogInformation("Trading coach response parsed for analysis {AnalysisId} and correlation {CorrelationId}", analysisId, options.CorrelationId);

            var merged = _analysisMerger.Merge(parsed, rules, normalization, metrics, options);
            _logger.LogInformation(
                "Trading coach analysis merged for analysis {AnalysisId}, correlation {CorrelationId}, and recommendation count {RecommendationCount}",
                analysisId, options.CorrelationId, merged.RecommendedActions.Count);

            var safe = _safetyFilter.Validate(merged, options.CorrelationId);
            _logger.LogInformation("Trading coach safety validation completed for analysis {AnalysisId} and correlation {CorrelationId}", analysisId, options.CorrelationId);
            _logger.LogInformation(
                "Trading coach analysis completed for analysis {AnalysisId}, agent {AgentId}, version {AgentVersion}, correlation {CorrelationId}, state {State}, and duration {Duration}",
                analysisId, TradingCoachConstants.AgentId, TradingCoachConstants.AgentVersion, options.CorrelationId,
                "Completed", _timeProvider.GetElapsedTime(started));
            return safe;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Trading coach analysis cancelled for analysis {AnalysisId} and correlation {CorrelationId}", analysisId, options.CorrelationId);
            throw;
        }
        catch (AIAgentTimeoutException exception)
        {
            _logger.LogWarning(
                "Trading coach analysis timed out for analysis {AnalysisId}, correlation {CorrelationId}, and error code {ErrorCode}",
                analysisId, options.CorrelationId, "TRADING_COACH_TIMEOUT");
            throw new TradingCoachTimeoutException(analysisId, options.CorrelationId, exception);
        }
        catch (Exception exception) when (exception is TradingJournalValidationException
            or TradingCoachResponseParsingException
            or TradingCoachSafetyException
            or TradingCoachTimeoutException)
        {
            _logger.LogWarning(
                "Trading coach analysis failed for analysis {AnalysisId}, correlation {CorrelationId}, and error type {ErrorType}",
                analysisId, options.CorrelationId, exception.GetType().Name);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Trading coach analysis failed for analysis {AnalysisId}, correlation {CorrelationId}, and error code {ErrorCode}",
                analysisId, options.CorrelationId, "TRADING_COACH_ANALYSIS_FAILED");
            throw new TradingCoachAnalysisException(analysisId, options.CorrelationId, exception);
        }
    }

    private static AIAgentExecutionRequest CreateAgentRequest(
        TradingJournalNormalizationResult normalization,
        TradeMetricsResult metrics,
        TradingCoachRuleAnalysisResult rules,
        TradingCoachProfile profile,
        TradingCoachExecutionOptions options,
        Guid analysisId,
        DateTimeOffset requestedAtUtc)
    {
        var knowledge = options.IncludeKnowledge
            ? new AIKnowledgeOptions(
                enabled: true,
                query: "educational trading process risk management psychology discipline journaling",
                maxResults: 4,
                minimumScore: 0.5,
                maxCharacters: 3_000,
                includeSourceMetadata: false,
                includeCitations: true,
                failureMode: KnowledgeFailureMode.ContinueWithoutKnowledge,
                filters: new Dictionary<string, string>
                {
                    ["contentPurpose"] = "educational-coaching",
                    ["topic"] = "process",
                    ["language"] = options.Language
                },
                useCurrentUserMessageAsQuery: false,
                contextLabel: "Educational coaching context")
            : AIKnowledgeOptions.Disabled;

        return new AIAgentExecutionRequest(
            AgentId,
            "Analyze the structured journal data supplied by the application.",
            TradingCoachConstants.Scenario,
            requestedAtUtc,
            AIAgentVersionSelection.Exact,
            AgentVersion,
            options.SessionId,
            options.ConversationId,
            options.TenantId,
            options.UserId,
            options.CorrelationId,
            promptVariables: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["normalizedJournal"] = JsonSerializer.Serialize(normalization.NormalizedRequest, JsonOptions),
                ["computedMetrics"] = JsonSerializer.Serialize(metrics, JsonOptions),
                ["ruleBasedFindings"] = JsonSerializer.Serialize(rules, JsonOptions),
                ["coachProfile"] = JsonSerializer.Serialize(profile, JsonOptions),
                ["requestedLanguage"] = options.Language,
                ["requiredOutputSchema"] = TradingCoachPromptTemplates.RequiredOutputSchema,
                ["safetyRules"] = TradingCoachPromptTemplates.SafetyRules
            },
            memoryOptions: new AIAgentMemoryRequestOptions(options.IncludeMemory),
            knowledgeOptions: knowledge,
            metadata: new Dictionary<string, string>
            {
                ["analysis-id"] = analysisId.ToString("D"),
                ["operation"] = "trading-coach-analysis"
            },
            timeoutOverride: options.AnalysisTimeout,
            requestedCapabilities: new AIAgentCapabilities(
                supportsPromptTemplates: true,
                supportsMemory: options.IncludeMemory,
                supportsKnowledge: options.IncludeKnowledge,
                supportsConversation: true,
                supportsStructuredOutput: true));
    }
}
