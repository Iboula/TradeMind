using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Tools;

public interface IAITool
{
    AIToolDefinition Definition { get; }

    Task<AIToolExecutionResult> ExecuteAsync(
        AIToolExecutionContext context,
        CancellationToken cancellationToken);
}

public interface IAIToolRegistry
{
    Task<IAITool> GetAsync(AIToolId toolId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AIToolDefinition>> GetAvailableAsync(
        AIToolDiscoveryContext context,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(AIToolId toolId, CancellationToken cancellationToken);
}

public interface IAIToolExecutor
{
    Task<AIToolExecutionResult> ExecuteAsync(
        AIToolExecutionRequest request,
        AIToolAuthorizationContext authorizationContext,
        CancellationToken cancellationToken);
}

public interface IAIToolAuthorizer
{
    void Authorize(AIToolDefinition definition, AIToolAuthorizationContext context);
}

public interface IAIToolArgumentValidator
{
    AIToolArguments Validate(
        AIToolDefinition definition,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> arguments,
        string? correlationId = null);
}

public interface IAIToolResultComposer
{
    ChatMessage Compose(AIToolExecutionResult result);
}
