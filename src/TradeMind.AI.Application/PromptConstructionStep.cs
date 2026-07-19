using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Application;

public sealed class PromptConstructionStep : IAIOrchestrationStep
{
    private readonly IPromptBuilder _promptBuilder;
    private readonly IPromptRenderer _promptRenderer;
    private readonly IEnumerable<IAIContextContributor> _contributors;
    private readonly TimeProvider _timeProvider;

    public PromptConstructionStep(
        IPromptBuilder promptBuilder,
        IPromptRenderer promptRenderer,
        IEnumerable<IAIContextContributor> contributors,
        TimeProvider timeProvider)
    {
        _promptBuilder = promptBuilder;
        _promptRenderer = promptRenderer;
        _contributors = contributors;
        _timeProvider = timeProvider;
    }

    public string Name => AIOrchestrationStepNames.PromptConstruction;

    public int Order => 200;

    public async Task ExecuteAsync(
        AIExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTimestamp = _timeProvider.GetTimestamp();

        foreach (var contributor in _contributors)
        {
            await contributor.ContributeAsync(context, cancellationToken).ConfigureAwait(false);
        }

        var chatRequest = context.Request.PromptTemplateId is null
            ? BuildLegacyChatRequest(context)
            : await BuildTemplateChatRequestAsync(context, cancellationToken).ConfigureAwait(false);

        context.SetChatRequest(chatRequest);
        context.Metrics.RecordPromptConstructionDuration(_timeProvider.GetElapsedTime(startTimestamp));
    }

    private ChatRequest BuildLegacyChatRequest(AIExecutionContext context)
    {
        return _promptBuilder
            .WithSystemMessage(context.Request.SystemInstruction)
            .AddUserMessage(context.Request.UserMessage)
            .Build(
                context.Request.Model,
                context.Request.Temperature,
                context.Request.MaxOutputTokens,
                BuildMetadata(context));
    }

    private async Task<ChatRequest> BuildTemplateChatRequestAsync(
        AIExecutionContext context,
        CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, string>(context.Request.PromptVariables, StringComparer.Ordinal);
        AddVariableIfMissing(variables, "systemInstruction", context.Request.SystemInstruction);
        AddVariableIfMissing(variables, "userMessage", context.Request.UserMessage);

        var result = await _promptRenderer.RenderAsync(
            new PromptRenderRequest(
                context.Request.PromptTemplateId!,
                context.Request.Scenario,
                variables,
                context.Request.PromptTemplateVersion,
                correlationId: context.Session.CorrelationId),
            cancellationToken).ConfigureAwait(false);

        context.SetPromptRenderResult(result);

        return new ChatRequest(
            result.Messages.Select(ToChatMessage).ToArray(),
            string.IsNullOrWhiteSpace(context.Request.Model) ? null : context.Request.Model,
            context.Request.Temperature,
            context.Request.MaxOutputTokens,
            BuildMetadata(context));
    }

    private static ChatMessage ToChatMessage(PromptRenderedMessage message)
    {
        return new ChatMessage(
            message.Role switch
            {
                PromptMessageRole.System => ChatRole.System,
                PromptMessageRole.User => ChatRole.User,
                PromptMessageRole.Assistant => ChatRole.Assistant,
                PromptMessageRole.Tool => ChatRole.Tool,
                _ => throw new ArgumentOutOfRangeException(nameof(message), "Unsupported prompt message role.")
            },
            message.Content);
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(AIExecutionContext context)
    {
        var metadata = new Dictionary<string, string>(
            context.Request.Metadata,
            StringComparer.OrdinalIgnoreCase)
        {
            ["SessionId"] = context.Session.SessionId,
            ["CorrelationId"] = context.Session.CorrelationId,
            ["Scenario"] = context.Session.Scenario
        };

        AddIfPresent(metadata, "ConversationId", context.Session.ConversationId);
        AddIfPresent(metadata, "TenantId", context.Session.TenantId);
        AddIfPresent(metadata, "UserId", context.Session.UserId);
        AddIfPresent(metadata, "AgentId", context.Session.AgentId);

        return metadata;
    }

    private static void AddIfPresent(IDictionary<string, string> metadata, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            metadata[key] = value;
        }
    }

    private static void AddVariableIfMissing(IDictionary<string, string> variables, string key, string? value)
    {
        if (!variables.ContainsKey(key) && !string.IsNullOrWhiteSpace(value))
        {
            variables[key] = value;
        }
    }
}
