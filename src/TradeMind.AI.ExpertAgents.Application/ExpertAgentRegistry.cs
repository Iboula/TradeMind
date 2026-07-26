using System.Collections.ObjectModel;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.ExpertAgents.Application;

public sealed record ExpertAgentDiscoveryQuery
{
    public AgentSpecialty? Specialty { get; init; }
    public Instrument? Instrument { get; init; }
    public Timeframe? Timeframe { get; init; }
    public AgentAnalysisMode? AnalysisMode { get; init; }
    public IReadOnlyCollection<ContextProviderCategory> RequiredContextCategories { get; init; } = [];
}

public interface IExpertAgentRegistry
{
    bool TryResolve(
        AgentId agentId,
        AgentVersionSelection versionSelection,
        AgentVersion? exactVersion,
        out IExpertAgent? agent);

    IReadOnlyList<IExpertAgent> GetVersions(AgentId agentId);

    IReadOnlyList<AgentDescriptor> GetAvailable();

    IReadOnlyList<AgentDescriptor> Find(ExpertAgentDiscoveryQuery query);
}

public sealed class ImmutableExpertAgentRegistry : IExpertAgentRegistry
{
    private readonly IReadOnlyDictionary<AgentId, IReadOnlyList<IExpertAgent>> _agents;
    private readonly IReadOnlyList<IExpertAgent> _orderedAgents;

    public ImmutableExpertAgentRegistry(IEnumerable<IExpertAgent> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);
        var materialized = agents.ToArray();
        if (materialized.Any(agent => agent is null))
        {
            throw new ArgumentException("The expert agent collection cannot contain null entries.", nameof(agents));
        }

        var duplicate = materialized
            .GroupBy(agent => (agent.Descriptor.Id, agent.Descriptor.Version))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ExpertAgentRegistryException(
                $"Agent '{duplicate.Key.Id}' version '{duplicate.Key.Version}' is registered more than once.");
        }

        _orderedAgents = Array.AsReadOnly(materialized
            .OrderBy(agent => agent.Descriptor.Id.Value, StringComparer.Ordinal)
            .ThenByDescending(agent => agent.Descriptor.Version)
            .ToArray());
        _agents = new ReadOnlyDictionary<AgentId, IReadOnlyList<IExpertAgent>>(
            materialized
                .GroupBy(agent => agent.Descriptor.Id)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<IExpertAgent>)Array.AsReadOnly(group
                        .OrderBy(agent => agent.Descriptor.Version)
                        .ToArray())));
    }

    public bool TryResolve(
        AgentId agentId,
        AgentVersionSelection versionSelection,
        AgentVersion? exactVersion,
        out IExpertAgent? agent)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        agent = null;
        if (!_agents.TryGetValue(agentId, out var versions))
        {
            return false;
        }

        if (versionSelection == AgentVersionSelection.Exact && exactVersion is null)
        {
            throw new ExpertAgentRegistryException("Exact version selection requires an exact version.");
        }

        agent = versionSelection switch
        {
            AgentVersionSelection.Exact => versions.SingleOrDefault(candidate => candidate.Descriptor.Version == exactVersion),
            AgentVersionSelection.Latest => versions[^1],
            AgentVersionSelection.LatestStable => versions.LastOrDefault(candidate => candidate.Descriptor.Version.IsStable),
            _ => throw new ExpertAgentRegistryException("The requested version selection is not supported.")
        };
        return agent is not null;
    }

    public IReadOnlyList<IExpertAgent> GetVersions(AgentId agentId)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        return _agents.TryGetValue(agentId, out var versions) ? versions : [];
    }

    public IReadOnlyList<AgentDescriptor> GetAvailable() => Array.AsReadOnly(
        _orderedAgents
            .Where(agent => agent.Descriptor.ActivationStatus == AgentActivationStatus.Enabled)
            .Select(agent => agent.Descriptor)
            .ToArray());

    public IReadOnlyList<AgentDescriptor> Find(ExpertAgentDiscoveryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Array.AsReadOnly(_orderedAgents
            .Where(agent => agent.Descriptor.ActivationStatus == AgentActivationStatus.Enabled)
            .Where(agent => query.Specialty is null || agent.Descriptor.Specialty == query.Specialty)
            .Where(agent => query.Instrument is null || agent.Descriptor.Capabilities.Supports(query.Instrument))
            .Where(agent => query.Timeframe is null || agent.Descriptor.Capabilities.Supports(query.Timeframe))
            .Where(agent => query.AnalysisMode is null || agent.Descriptor.Capabilities.Supports(query.AnalysisMode.Value))
            .Where(agent => query.RequiredContextCategories.All(category =>
                agent.Descriptor.Capabilities.RequiredContextCategories.Contains(category)))
            .Select(agent => agent.Descriptor)
            .ToArray());
    }
}

public sealed class ExpertAgentRegistryException(string message) : InvalidOperationException(message);
