namespace TradeMind.AI.Application;

public sealed class PromptConstructionStep : IAIOrchestrationStep
{
    private readonly IPromptBuilder _promptBuilder;
    private readonly IEnumerable<IAIContextContributor> _contributors;

    public PromptConstructionStep(
        IPromptBuilder promptBuilder,
        IEnumerable<IAIContextContributor> contributors)
    {
        _promptBuilder = promptBuilder;
        _contributors = contributors;
    }

    public int Order => 200;

    public async Task ExecuteAsync(
        AIOrchestrationContext context,
        CancellationToken cancellationToken)
    {
        foreach (var contributor in _contributors)
        {
            await contributor.ContributeAsync(context, cancellationToken).ConfigureAwait(false);
        }

        context.ChatRequest = _promptBuilder
            .WithSystemMessage(context.Request.SystemInstruction)
            .AddUserMessage(context.Request.UserMessage)
            .Build(
                context.Request.Model,
                context.Request.Temperature,
                context.Request.MaxOutputTokens,
                BuildMetadata(context));
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(AIOrchestrationContext context)
    {
        var metadata = new Dictionary<string, string>(
            context.Request.Metadata ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase)
        {
            ["CorrelationId"] = context.CorrelationId,
            ["Scenario"] = context.Request.Scenario
        };

        return metadata;
    }
}
