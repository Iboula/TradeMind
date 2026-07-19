namespace TradeMind.AI.Application;

public sealed class ResponseNormalizationStep : IAIOrchestrationStep
{
    public string Name => AIOrchestrationStepNames.ResponseNormalization;

    public int Order => 500;

    public Task ExecuteAsync(
        AIExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var chatResponse = context.ChatResponse
            ?? throw new AIOrchestrationException(
                "Chat response has not been produced.",
                context.Session.CorrelationId,
                Name);

        context.SetFinalResponse(new AIOrchestrationResponse(
            context.Session.SessionId,
            context.Session.ConversationId,
            context.Session.CorrelationId,
            context.Session.Scenario,
            chatResponse.ProviderName,
            chatResponse.Model,
            chatResponse.Content,
            chatResponse.Usage,
            TimeSpan.Zero,
            context.Metrics.ProviderDuration,
            context.ExecutedSteps.ToArray(),
            chatResponse.GeneratedAtUtc,
            context.State,
            chatResponse.ResponseId));

        return Task.CompletedTask;
    }
}
