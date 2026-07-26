namespace TradeMind.AI.Application;

public interface IAIContextContributor
{
    Task ContributeAsync(
        AIExecutionContext context,
        CancellationToken cancellationToken);
}
