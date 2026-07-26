using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.Tools;

public sealed class InMemoryAIToolRegistry : IAIToolRegistry
{
    private readonly IReadOnlyDictionary<AIToolId, IAITool> _tools;
    private readonly AIToolEngineOptions _options;
    private readonly ILogger<InMemoryAIToolRegistry> _logger;

    public InMemoryAIToolRegistry(
        IEnumerable<IAITool> tools,
        IOptions<AIToolEngineOptions> options,
        ILogger<InMemoryAIToolRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(tools);
        _options = options.Value;
        _logger = logger;

        var registry = new Dictionary<AIToolId, IAITool>();
        foreach (var tool in tools)
        {
            ArgumentNullException.ThrowIfNull(tool);
            if (!registry.TryAdd(tool.Definition.Id, tool))
            {
                throw new InvalidOperationException($"Tool '{tool.Definition.Id}' is registered more than once.");
            }
        }

        _tools = registry;
    }

    public Task<IAITool> GetAsync(AIToolId toolId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_tools.TryGetValue(toolId, out var tool)
            ? tool
            : throw new AIToolNotFoundException(toolId));
    }

    public Task<IReadOnlyList<AIToolDefinition>> GetAvailableAsync(
        AIToolDiscoveryContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var definitions = _tools.Values
            .Select(tool => tool.Definition)
            .Where(definition => IsAvailable(definition, context))
            .OrderBy(definition => definition.Id.Value, StringComparer.Ordinal)
            .ToArray();

        _logger.LogInformation(
            "AI tool discovery completed for tenant {TenantId}, user {UserId}, agent {AgentId}, scenario {Scenario}, and returned {ToolCount} tools",
            context.TenantId,
            context.UserId,
            context.AgentId,
            context.Scenario,
            definitions.Length);

        return Task.FromResult<IReadOnlyList<AIToolDefinition>>(Array.AsReadOnly(definitions));
    }

    public Task<bool> ExistsAsync(AIToolId toolId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_tools.ContainsKey(toolId));
    }

    private bool IsAvailable(AIToolDefinition definition, AIToolDiscoveryContext context)
    {
        if (definition.Availability == AIToolAvailability.Disabled
            || definition.Availability == AIToolAvailability.DevelopmentOnly && !_options.EnableDevelopmentTools)
        {
            return false;
        }

        var maximumSideEffect = (AIToolSideEffectLevel)Math.Min(
            (int)_options.MaximumAllowedSideEffectLevel,
            (int)context.MaximumAllowedSideEffectLevel);

        return definition.SideEffectLevel <= maximumSideEffect
            && definition.RequiredPermissions.All(required => context.Permissions.Contains(required, StringComparer.OrdinalIgnoreCase))
            && context.Tags.All(tag => definition.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            && IsAllowed(definition.AllowedTenantIds, context.TenantId, StringComparer.Ordinal)
            && IsAllowed(definition.AllowedUserIds, context.UserId, StringComparer.Ordinal)
            && IsAllowed(definition.AllowedAgentIds, context.AgentId, StringComparer.Ordinal)
            && IsAllowed(definition.AllowedScenarios, context.Scenario, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsAllowed(
        IReadOnlyCollection<string> allowedValues,
        string? actualValue,
        StringComparer comparer) =>
        allowedValues.Count == 0
        || actualValue is not null && allowedValues.Contains(actualValue, comparer);
}
