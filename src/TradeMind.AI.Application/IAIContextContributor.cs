namespace TradeMind.AI.Application;

public interface IAIContextContributor
{
    Task ContributeAsync(
        AIOrchestrationContext context,
        CancellationToken cancellationToken);
}
