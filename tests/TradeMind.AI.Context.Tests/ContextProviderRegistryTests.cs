using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Domain;

namespace TradeMind.AI.Context.Tests;

public sealed class ContextProviderRegistryTests
{
    [Fact]
    public void EmptyRegistry_ShouldReturnEmptyPlan()
    {
        var registry = new ContextProviderRegistry([]);

        Assert.Empty(registry.Providers);
        Assert.Empty(registry.CreateExecutionPlan([]));
    }

    [Fact]
    public void Registry_ShouldResolveProviderByStableId()
    {
        var provider = ContextTestData.MarketProvider();
        var registry = new ContextProviderRegistry([provider]);

        Assert.Same(provider, registry.Get(new ContextProviderId("MARKET")));
    }

    [Fact]
    public void Registry_ShouldRejectDuplicateProviderIds()
    {
        var exception = Assert.Throws<ContextProviderRegistryException>(() =>
            new ContextProviderRegistry([
                ContextTestData.MarketProvider(id: "duplicate"),
                ContextTestData.MarketProvider(id: "DUPLICATE")
            ]));

        Assert.Equal(ContextBuildErrorCode.InvalidProviderResult, exception.Code);
    }

    [Fact]
    public void Registry_ShouldOrderDeterministicallyByPriorityThenId()
    {
        var providers = new[]
        {
            Provider("z", 20),
            Provider("b", 10),
            Provider("a", 10)
        };

        var plan = new ContextProviderRegistry(providers).CreateExecutionPlan([]);

        Assert.Equal(["a", "b", "z"], plan.Single().Select(provider => provider.Descriptor.Id.Value));
    }

    [Fact]
    public void Registry_ShouldRejectUnknownDependency()
    {
        var provider = Provider("dependent", 10, [new ContextProviderId("missing")]);

        var exception = Assert.Throws<ContextProviderRegistryException>(() =>
            new ContextProviderRegistry([provider]));

        Assert.Equal(ContextBuildErrorCode.UnknownDependency, exception.Code);
    }

    [Fact]
    public void Registry_ShouldRejectDependencyCycle()
    {
        var first = Provider("first", 10, [new ContextProviderId("second")]);
        var second = Provider("second", 10, [new ContextProviderId("first")]);

        var exception = Assert.Throws<ContextProviderRegistryException>(() =>
            new ContextProviderRegistry([first, second]));

        Assert.Equal(ContextBuildErrorCode.DependencyCycle, exception.Code);
    }

    [Fact]
    public void Registry_ShouldPlaceIndependentProvidersInSameLevel()
    {
        var plan = new ContextProviderRegistry([
            Provider("first", 10),
            Provider("second", 20)
        ]).CreateExecutionPlan([]);

        Assert.Single(plan);
        Assert.Equal(2, plan[0].Count);
    }

    [Fact]
    public void Registry_ShouldPlaceDependenciesInEarlierLevels()
    {
        var plan = new ContextProviderRegistry([
            Provider("root", 50),
            Provider("child", 10, [new ContextProviderId("root")])
        ]).CreateExecutionPlan([]);

        Assert.Equal(2, plan.Count);
        Assert.Equal("root", plan[0].Single().Descriptor.Id.Value);
        Assert.Equal("child", plan[1].Single().Descriptor.Id.Value);
    }

    [Fact]
    public void RequestedCategory_ShouldIncludeItsDependencies()
    {
        var root = Provider("root", 10, category: ContextProviderCategory.MarketSnapshot);
        var child = Provider(
            "child",
            20,
            [root.Descriptor.Id],
            ContextProviderCategory.Knowledge);
        var plan = new ContextProviderRegistry([root, child]).CreateExecutionPlan(
            [ContextProviderCategory.Knowledge]);

        Assert.Equal(2, plan.SelectMany(level => level).Count());
    }

    [Fact]
    public void Descriptor_ShouldDefensivelyCopyDependencies()
    {
        var dependencies = new List<ContextProviderId> { new("root") };
        var descriptor = new ContextProviderDescriptor(
            new ContextProviderId("child"),
            ContextProviderCategory.Knowledge,
            ContextRequirement.Preferred,
            10,
            TimeSpan.FromSeconds(1),
            dependencies);

        dependencies.Clear();

        Assert.Single(descriptor.Dependencies);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ContextProviderId>)descriptor.Dependencies).Add(new ContextProviderId("other")));
    }

    [Fact]
    public void Registry_ShouldExposeAnImmutableProviderCollection()
    {
        var registry = new ContextProviderRegistry([ContextTestData.MarketProvider()]);

        Assert.Throws<NotSupportedException>(() =>
            ((IList<IContextProvider>)registry.Providers).Clear());
    }

    private static TestContextProvider Provider(
        string id,
        int priority,
        IReadOnlyCollection<ContextProviderId>? dependencies = null,
        ContextProviderCategory category = ContextProviderCategory.Workspace) => new(
            new ContextProviderDescriptor(
                new ContextProviderId(id),
                category,
                ContextRequirement.Optional,
                priority,
                TimeSpan.FromSeconds(1),
                dependencies),
            (_, _) => Task.FromResult(ContextProviderResult.NotConfigured(
                new ContextProviderId(id),
                "not configured")));
}
