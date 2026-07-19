using Microsoft.Extensions.Logging;
using TradeMind.AI.Application;

namespace TradeMind.AI.Knowledge;

public sealed class KnowledgeRetrievalStep : IAIOrchestrationStep
{
    private readonly IKnowledgeContextRetriever _retriever;
    private readonly IKnowledgeContextComposer _composer;
    private readonly ILogger<KnowledgeRetrievalStep> _logger;

    public KnowledgeRetrievalStep(
        IKnowledgeContextRetriever retriever,
        IKnowledgeContextComposer composer,
        ILogger<KnowledgeRetrievalStep> logger)
    {
        _retriever = retriever;
        _composer = composer;
        _logger = logger;
    }

    public string Name => AIOrchestrationStepNames.KnowledgeRetrieval;

    public int Order => 175;

    public async Task ExecuteAsync(
        AIExecutionContext context,
        CancellationToken cancellationToken)
    {
        var options = context.Request.Knowledge;
        if (!options.Enabled)
        {
            context.SetItem(AIExecutionContextItemKey.KnowledgeUsed, false);
            context.SetItem(AIExecutionContextItemKey.KnowledgeCitationIds, Array.Empty<string>());
            return;
        }

        try
        {
            var query = ResolveQuery(context.Request, options);
            var request = new KnowledgeContextRequest(
                query,
                context.Session.SessionId,
                context.Session.CorrelationId,
                context.Session.Scenario,
                options.MaxResults,
                options.MinimumScore,
                options.MaxCharacters,
                options.MaxEstimatedTokens,
                options.OrderingStrategy,
                options.Filters,
                context.Session.TenantId,
                context.Session.UserId,
                options.IncludeSourceMetadata,
                options.IncludeCitations,
                options.ContextLabel);

            var result = await _retriever.RetrieveAsync(request, cancellationToken).ConfigureAwait(false);
            var messages = _composer.Compose(result, options.ContextLabel);
            context.SetItem(AIExecutionContextItemKey.KnowledgeChatMessages, messages);
            context.SetItem(AIExecutionContextItemKey.KnowledgeUsed, result.SelectedResultCount > 0);
            context.SetItem(AIExecutionContextItemKey.KnowledgeCitationIds, result.Citations.Select(citation => citation.CitationId).ToArray());
            context.SetItem(AIExecutionContextItemKey.KnowledgeSelectedResultCount, result.SelectedResultCount);
            context.SetItem(AIExecutionContextItemKey.KnowledgeRetrievalDuration, result.RetrievalDuration);

            _logger.LogInformation(
                "Knowledge context prepared for session {SessionId}, correlation {CorrelationId}, scenario {Scenario}, selected {SelectedResultCount}, truncated {Truncated}, and duration {DurationMs} ms",
                context.Session.SessionId,
                context.Session.CorrelationId,
                context.Session.Scenario,
                result.SelectedResultCount,
                result.Truncated,
                result.RetrievalDuration.TotalMilliseconds);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (options.FailureMode == KnowledgeFailureMode.ContinueWithoutKnowledge)
            {
                context.SetItem(AIExecutionContextItemKey.KnowledgeUsed, false);
                context.SetItem(AIExecutionContextItemKey.KnowledgeChatMessages, Array.Empty<TradeMind.AI.Abstractions.ChatMessage>());
                context.SetItem(AIExecutionContextItemKey.KnowledgeCitationIds, Array.Empty<string>());

                _logger.LogWarning(
                    exception,
                    "Knowledge retrieval failed and orchestration continues without knowledge for session {SessionId}, correlation {CorrelationId}, and scenario {Scenario}",
                    context.Session.SessionId,
                    context.Session.CorrelationId,
                    context.Session.Scenario);
                return;
            }

            throw new KnowledgeRetrievalException("Knowledge retrieval failed.", context.Session.CorrelationId, exception);
        }
    }

    private static string ResolveQuery(AIOrchestrationRequest request, AIKnowledgeOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Query))
        {
            return options.Query;
        }

        if (options.UseCurrentUserMessageAsQuery && !string.IsNullOrWhiteSpace(request.UserMessage))
        {
            return request.UserMessage;
        }

        throw new KnowledgeContextValidationException("Knowledge query is required when knowledge is enabled.");
    }
}
