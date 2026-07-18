namespace TradeMind.AI.Application;

public sealed class ResponseNormalizationStep : IAIOrchestrationStep
{
    public int Order => 500;

    public Task ExecuteAsync(
        AIOrchestrationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var chatResponse = context.ChatResponse
            ?? throw new AIOrchestrationException(
                "Chat response has not been produced.",
                context.CorrelationId,
                nameof(ResponseNormalizationStep));

        context.FinalResponse = new AIOrchestrationResponse(
            chatResponse.Content,
            chatResponse.ProviderName,
            chatResponse.Model,
            chatResponse.Usage,
            context.CorrelationId,
            DateTimeOffset.UtcNow - context.StartedAtUtc,
            context.ExecutedSteps.ToArray(),
            chatResponse.GeneratedAtUtc,
            chatResponse.ResponseId);

        return Task.CompletedTask;
    }
}
