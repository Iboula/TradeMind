namespace TradeMind.AI.Application;

public sealed class PromptConstructionStep : IAIOrchestrationStep
{
    private readonly IPromptBuilder _promptBuilder;
    private readonly IEnumerable<IAIContextContributor> _contributors;
    private readonly TimeProvider _timeProvider;

    public PromptConstructionStep(
        IPromptBuilder promptBuilder,
        IEnumerable<IAIContextContributor> contributors,
        TimeProvider timeProvider)
    {
        _promptBuilder = promptBuilder;
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

        var chatRequest = _promptBuilder
            .WithSystemMessage(context.Request.SystemInstruction)
            .AddUserMessage(context.Request.UserMessage)
            .Build(
                context.Request.Model,
                context.Request.Temperature,
                context.Request.MaxOutputTokens,
                BuildMetadata(context));

        context.SetChatRequest(chatRequest);
        context.Metrics.RecordPromptConstructionDuration(_timeProvider.GetElapsedTime(startTimestamp));
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
}
