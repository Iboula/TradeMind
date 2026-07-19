using TradeMind.AI.Application;

namespace TradeMind.AI.Agents;

public interface IAIAgent
{
    AIAgentDefinition Definition { get; }

    ValueTask OnExecutionStartingAsync(
        AIAgentExecutionContext context,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    ValueTask OnExecutionCompletedAsync(
        AIAgentExecutionContext context,
        AIAgentExecutionResponse response,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    ValueTask OnExecutionFailedAsync(
        AIAgentExecutionContext context,
        Exception exception,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

public interface IAIAgentRegistry
{
    Task<IAIAgent> GetAsync(AIAgentId agentId, CancellationToken cancellationToken);

    Task<IAIAgent> GetAsync(
        AIAgentId agentId,
        AIAgentVersionSelection versionSelection,
        AIAgentVersion? exactVersion,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(AIAgentId agentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AIAgentDefinition>> GetAvailableAsync(
        AIAgentDiscoveryContext context,
        CancellationToken cancellationToken);
}

public interface IAIAgentExecutor
{
    Task<AIAgentExecutionResponse> ExecuteAsync(
        AIAgentExecutionRequest request,
        CancellationToken cancellationToken);
}

public interface IAIAgentAuthorizer
{
    void Authorize(
        AIAgentDefinition definition,
        AIAgentExecutionRequest request,
        AIAgentAuthorizationContext context);
}

public interface IAIAgentRequestMapper
{
    AIOrchestrationRequest Map(
        AIAgentDefinition definition,
        AIAgentExecutionRequest request);
}

public interface IAIAgentResponseMapper
{
    AIAgentExecutionResponse Map(
        AIAgentExecutionContext context,
        AIOrchestrationResponse response,
        AIAgentExecutionMetrics metrics);
}
