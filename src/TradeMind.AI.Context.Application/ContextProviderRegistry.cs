using TradeMind.AI.Context.Domain;

namespace TradeMind.AI.Context.Application;

public interface IContextProviderRegistry
{
    IReadOnlyList<IContextProvider> Providers { get; }

    IContextProvider Get(ContextProviderId id);

    IReadOnlyList<IReadOnlyList<IContextProvider>> CreateExecutionPlan(
        IReadOnlyCollection<ContextProviderCategory> requestedCategories);
}

public sealed class ContextProviderRegistry : IContextProviderRegistry
{
    private readonly IReadOnlyDictionary<ContextProviderId, IContextProvider> _byId;

    public ContextProviderRegistry(IEnumerable<IContextProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        var materialized = providers
            .OrderBy(provider => provider.Descriptor.Priority)
            .ThenBy(provider => provider.Descriptor.Id.Value, StringComparer.Ordinal)
            .ToArray();

        var duplicate = materialized
            .GroupBy(provider => provider.Descriptor.Id)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ContextProviderRegistryException(
                ContextBuildErrorCode.InvalidProviderResult,
                $"Provider id '{duplicate.Key}' is registered more than once.");
        }

        _byId = materialized.ToDictionary(provider => provider.Descriptor.Id);
        Providers = Array.AsReadOnly(materialized);
        ValidateDependencies(materialized);
        _ = CreateExecutionPlan([]);
    }

    public IReadOnlyList<IContextProvider> Providers { get; }

    public IContextProvider Get(ContextProviderId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _byId.TryGetValue(id, out var provider)
            ? provider
            : throw new KeyNotFoundException($"Context provider '{id}' is not registered.");
    }

    public IReadOnlyList<IReadOnlyList<IContextProvider>> CreateExecutionPlan(
        IReadOnlyCollection<ContextProviderCategory> requestedCategories)
    {
        ArgumentNullException.ThrowIfNull(requestedCategories);
        var selected = SelectProviders(requestedCategories);
        var remaining = new HashSet<ContextProviderId>(selected.Select(provider => provider.Descriptor.Id));
        var completed = new HashSet<ContextProviderId>();
        var levels = new List<IReadOnlyList<IContextProvider>>();

        while (remaining.Count > 0)
        {
            var level = selected
                .Where(provider => remaining.Contains(provider.Descriptor.Id))
                .Where(provider => provider.Descriptor.Dependencies.All(completed.Contains))
                .OrderBy(provider => provider.Descriptor.Priority)
                .ThenBy(provider => provider.Descriptor.Id.Value, StringComparer.Ordinal)
                .ToArray();
            if (level.Length == 0)
            {
                throw new ContextProviderRegistryException(
                    ContextBuildErrorCode.DependencyCycle,
                    "The context provider dependency graph contains a cycle.");
            }

            levels.Add(Array.AsReadOnly(level));
            foreach (var provider in level)
            {
                remaining.Remove(provider.Descriptor.Id);
                completed.Add(provider.Descriptor.Id);
            }
        }

        return Array.AsReadOnly(levels.ToArray());
    }

    private IReadOnlyList<IContextProvider> SelectProviders(
        IReadOnlyCollection<ContextProviderCategory> requestedCategories)
    {
        if (requestedCategories.Count == 0)
        {
            return Providers;
        }

        var categories = requestedCategories.ToHashSet();
        var selectedIds = Providers
            .Where(provider => categories.Contains(provider.Descriptor.Category))
            .Select(provider => provider.Descriptor.Id)
            .ToHashSet();

        var pending = new Stack<ContextProviderId>(selectedIds);
        while (pending.TryPop(out var id))
        {
            foreach (var dependency in _byId[id].Descriptor.Dependencies)
            {
                if (selectedIds.Add(dependency))
                {
                    pending.Push(dependency);
                }
            }
        }

        return Providers.Where(provider => selectedIds.Contains(provider.Descriptor.Id)).ToArray();
    }

    private void ValidateDependencies(IEnumerable<IContextProvider> providers)
    {
        foreach (var provider in providers)
        {
            foreach (var dependency in provider.Descriptor.Dependencies)
            {
                if (!_byId.ContainsKey(dependency))
                {
                    throw new ContextProviderRegistryException(
                        ContextBuildErrorCode.UnknownDependency,
                        $"Provider '{provider.Descriptor.Id}' depends on unknown provider '{dependency}'.");
                }
            }
        }
    }
}

public sealed class ContextProviderRegistryException(
    ContextBuildErrorCode code,
    string message) : InvalidOperationException(message)
{
    public ContextBuildErrorCode Code { get; } = code;
}
