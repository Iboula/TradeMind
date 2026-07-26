using TradeMind.AI.Agents;

namespace TradeMind.Trading.Analytics;

public sealed class TradingJournalAIInterpreter : ITradingJournalAIInterpreter
{
    private readonly ITradingJournalAgentRequestFactory _requestFactory;
    private readonly IAIAgentExecutor _agentExecutor;
    private readonly ITradingJournalAnalyticsResponseParser _parser;

    public TradingJournalAIInterpreter(
        ITradingJournalAgentRequestFactory requestFactory,
        IAIAgentExecutor agentExecutor,
        ITradingJournalAnalyticsResponseParser parser)
    {
        _requestFactory = requestFactory;
        _agentExecutor = agentExecutor;
        _parser = parser;
    }

    public async Task<TradingJournalAIInterpretation> InterpretAsync(
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
        CancellationToken cancellationToken)
    {
        var executionRequest = _requestFactory.Create(
            aggregates, dataQuality, behaviors, riskDrift, scoreEvolution, setups, rules,
            request, options, analysisId, requestedAtUtc);
        var response = await _agentExecutor.ExecuteAsync(executionRequest, cancellationToken).ConfigureAwait(false);
        return _parser.Parse(response.Content, request.RequestedLanguage, analysisId, request.CorrelationId);
    }
}
