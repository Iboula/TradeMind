namespace TradeMind.AI.Application;

public interface IAISessionFactory
{
    AISession Create(AIOrchestrationRequest request);
}
