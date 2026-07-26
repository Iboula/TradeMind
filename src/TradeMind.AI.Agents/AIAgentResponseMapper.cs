using TradeMind.AI.Application;

namespace TradeMind.AI.Agents;

public sealed class AIAgentResponseMapper : IAIAgentResponseMapper
{
    public AIAgentExecutionResponse Map(
        AIAgentExecutionContext context,
        AIOrchestrationResponse response,
        AIAgentExecutionMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(metrics);

        return new AIAgentExecutionResponse(
            context.Definition.Id,
            context.Definition.Version,
            response.SessionId,
            response.ConversationId,
            response.CorrelationId,
            response.Scenario,
            success: true,
            response.Content,
            AIAgentExecutionState.Completed,
            context.StartedAtUtc,
            response.CompletedAtUtc,
            response.Provider,
            response.MemoryUsed,
            response.KnowledgeUsed,
            response.ToolUsed,
            response.KnowledgeCitationIds,
            metrics,
            response.ToolId);
    }
}
