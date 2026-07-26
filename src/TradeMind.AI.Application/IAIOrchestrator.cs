namespace TradeMind.AI.Application;

public interface IAIOrchestrator
{
    Task<AIOrchestrationResponse> ExecuteAsync(
        AIOrchestrationRequest request,
        CancellationToken cancellationToken);
}
