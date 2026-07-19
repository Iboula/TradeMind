namespace TradeMind.AI.Application;

public interface IAIOrchestrationStep
{
    string Name { get; }

    int Order { get; }

    Task ExecuteAsync(
        AIExecutionContext context,
        CancellationToken cancellationToken);
}
