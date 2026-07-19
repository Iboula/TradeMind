using Microsoft.Extensions.Options;

namespace TradeMind.AI.Agents;

public sealed class InMemoryAIAgentRegistry : IAIAgentRegistry
{
    private readonly IReadOnlyDictionary<AIAgentId, IReadOnlyList<IAIAgent>> _agents;
    private readonly AIAgentFrameworkOptions _options;

    public InMemoryAIAgentRegistry(
        IEnumerable<IAIAgent> agents,
        IOptions<AIAgentFrameworkOptions> options)
    {
        ArgumentNullException.ThrowIfNull(agents);
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        var materialized = agents.ToArray();
        var duplicate = materialized
            .GroupBy(agent => (agent.Definition.Id, agent.Definition.Version))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new AIAgentValidationException(
                duplicate.Key.Id,
                "An AI agent with the same id and version is already registered.",
                duplicate.Key.Version,
                errorCode: "AI_AGENT_DUPLICATE_REGISTRATION");
        }

        _agents = materialized
            .GroupBy(agent => agent.Definition.Id)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<IAIAgent>)group
                    .OrderBy(agent => agent.Definition.Version)
                    .ToArray());
    }

    public Task<IAIAgent> GetAsync(AIAgentId agentId, CancellationToken cancellationToken) =>
        GetAsync(agentId, _options.DefaultVersionSelection, null, cancellationToken);

    public Task<IAIAgent> GetAsync(
        AIAgentId agentId,
        AIAgentVersionSelection versionSelection,
        AIAgentVersion? exactVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_agents.TryGetValue(agentId, out var versions))
        {
            throw new AIAgentNotFoundException(agentId);
        }

        IAIAgent? selected = versionSelection switch
        {
            AIAgentVersionSelection.Exact when exactVersion is null => throw new AIAgentValidationException(
                agentId,
                "ExactVersion is required for exact version selection."),
            AIAgentVersionSelection.Exact => versions.SingleOrDefault(agent => agent.Definition.Version == exactVersion),
            AIAgentVersionSelection.Latest => versions[^1],
            AIAgentVersionSelection.LatestStable => versions.LastOrDefault(agent => agent.Definition.Version.IsStable),
            _ => throw new AIAgentValidationException(agentId, "The version selection strategy is invalid.")
        };

        return Task.FromResult(selected ?? throw new AIAgentVersionNotFoundException(agentId, exactVersion));
    }

    public Task<bool> ExistsAsync(AIAgentId agentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_agents.ContainsKey(agentId));
    }

    public Task<IReadOnlyList<AIAgentDefinition>> GetAvailableAsync(
        AIAgentDiscoveryContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var definitions = _agents.Values
            .SelectMany(versions => versions)
            .Select(agent => agent.Definition)
            .Where(definition => IsDiscoverable(definition, context))
            .OrderBy(definition => definition.Id.Value, StringComparer.Ordinal)
            .ThenByDescending(definition => definition.Version)
            .ToArray();

        return Task.FromResult<IReadOnlyList<AIAgentDefinition>>(Array.AsReadOnly(definitions));
    }

    private bool IsDiscoverable(AIAgentDefinition definition, AIAgentDiscoveryContext context)
    {
        if (definition.Availability == AIAgentAvailability.Disabled)
        {
            return false;
        }

        if (!context.Availabilities.Contains(definition.Availability)
            || definition.Availability == AIAgentAvailability.DevelopmentOnly
                && (!_options.EnableDevelopmentAgents || !context.IncludeDevelopmentAgents))
        {
            return false;
        }

        return ContainsAll(context.Permissions, definition.RequiredPermissions)
            && IsAllowed(context.TenantId, definition.AllowedTenantIds)
            && IsAllowed(context.UserId, definition.AllowedUserIds)
            && (context.Scenario is null || definition.SupportedScenarios.Contains(context.Scenario, StringComparer.OrdinalIgnoreCase))
            && definition.Capabilities.Includes(context.RequiredCapabilities)
            && ContainsAll(definition.Tags, context.Tags)
            && definition.MaximumAllowedSideEffectLevel <= context.MaximumAllowedSideEffectLevel;
    }

    private static bool IsAllowed(string? value, IReadOnlyCollection<string> allowedValues) =>
        allowedValues.Count == 0
        || value is not null && allowedValues.Contains(value, StringComparer.Ordinal);

    private static bool ContainsAll(
        IReadOnlyCollection<string> available,
        IReadOnlyCollection<string> required) =>
        required.All(value => available.Contains(value, StringComparer.OrdinalIgnoreCase));
}
