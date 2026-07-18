namespace TradeMind.AI.Application;

public interface IAIOrchestrationStep
{
    int Order { get; }

    Task ExecuteAsync(
        AIOrchestrationContext context,
        CancellationToken cancellationToken);
}
