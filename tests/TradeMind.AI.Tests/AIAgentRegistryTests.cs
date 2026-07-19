using TradeMind.AI.Agents;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Tests;

public sealed class AIAgentRegistryTests
{
    [Fact]
    public async Task Registry_ReturnsAgent()
    {
        var agent = Agent("1.0.0");
        var result = await AIAgentTestData.Registry([agent]).GetAsync(agent.Definition.Id, CancellationToken.None);
        Assert.Same(agent, result);
    }

    [Fact]
    public void Registry_RejectsDuplicateIdAndVersion()
    {
        Assert.Throws<AIAgentValidationException>(() => AIAgentTestData.Registry([Agent("1.0.0"), Agent("1.0.0")]));
    }

    [Fact]
    public async Task Registry_AcceptsDifferentVersions()
    {
        var registry = AIAgentTestData.Registry([Agent("1.0.0"), Agent("2.0.0")]);
        var available = await registry.GetAvailableAsync(new AIAgentDiscoveryContext(), CancellationToken.None);
        Assert.Equal(2, available.Count);
    }

    [Fact]
    public async Task Registry_ExactReturnsExactVersion()
    {
        var registry = AIAgentTestData.Registry([Agent("1.0.0"), Agent("2.0.0")]);
        var result = await registry.GetAsync(
            new AIAgentId("test-agent"),
            AIAgentVersionSelection.Exact,
            AIAgentVersion.Parse("1.0.0"),
            CancellationToken.None);
        Assert.Equal("1.0.0", result.Definition.Version.ToString());
    }

    [Fact]
    public async Task Registry_LatestReturnsHighestVersion()
    {
        var registry = AIAgentTestData.Registry([Agent("1.10.0"), Agent("1.2.0")]);
        var result = await registry.GetAsync(
            new AIAgentId("test-agent"),
            AIAgentVersionSelection.Latest,
            null,
            CancellationToken.None);
        Assert.Equal("1.10.0", result.Definition.Version.ToString());
    }

    [Fact]
    public async Task Registry_LatestStableExcludesPreRelease()
    {
        var registry = AIAgentTestData.Registry([Agent("1.0.0"), Agent("2.0.0-beta.1")]);
        var result = await registry.GetAsync(
            new AIAgentId("test-agent"),
            AIAgentVersionSelection.LatestStable,
            null,
            CancellationToken.None);
        Assert.Equal("1.0.0", result.Definition.Version.ToString());
    }

    [Fact]
    public async Task Registry_UnknownVersionThrows()
    {
        var registry = AIAgentTestData.Registry([Agent("1.0.0")]);
        await Assert.ThrowsAsync<AIAgentVersionNotFoundException>(() => registry.GetAsync(
            new AIAgentId("test-agent"),
            AIAgentVersionSelection.Exact,
            AIAgentVersion.Parse("2.0.0"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Registry_UnknownAgentThrows()
    {
        var registry = AIAgentTestData.Registry([Agent("1.0.0")]);
        await Assert.ThrowsAsync<AIAgentNotFoundException>(() => registry.GetAsync(
            new AIAgentId("missing-agent"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Registry_ExistsReturnsCorrectValue()
    {
        var registry = AIAgentTestData.Registry([Agent("1.0.0")]);
        Assert.True(await registry.ExistsAsync(new AIAgentId("test-agent"), CancellationToken.None));
        Assert.False(await registry.ExistsAsync(new AIAgentId("missing-agent"), CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_ExcludesDisabledAgent()
    {
        var registry = AIAgentTestData.Registry([Agent("1.0.0", AIAgentAvailability.Disabled)]);
        var result = await registry.GetAvailableAsync(
            new AIAgentDiscoveryContext(availabilities: [AIAgentAvailability.Disabled]),
            CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Discovery_ExcludesDevelopmentOnlyInProduction()
    {
        var registry = AIAgentTestData.Registry([Agent("1.0.0", AIAgentAvailability.DevelopmentOnly)]);
        var result = await registry.GetAvailableAsync(
            new AIAgentDiscoveryContext(
                availabilities: [AIAgentAvailability.DevelopmentOnly],
                includeDevelopmentAgents: true),
            CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Discovery_FiltersByPermission()
    {
        var definition = AIAgentTestData.Definition(permissions: ["agent.execute"]);
        var registry = AIAgentTestData.Registry([new StubAIAgent(definition)]);
        Assert.Empty(await registry.GetAvailableAsync(new AIAgentDiscoveryContext(), CancellationToken.None));
        Assert.Single(await registry.GetAvailableAsync(
            new AIAgentDiscoveryContext(permissions: ["agent.execute"]),
            CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_FiltersByScenario()
    {
        var registry = AIAgentTestData.Registry([Agent("1.0.0")]);
        var result = await registry.GetAvailableAsync(
            new AIAgentDiscoveryContext(scenario: "Other"),
            CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Discovery_FiltersByCapability()
    {
        var registry = AIAgentTestData.Registry([Agent("1.0.0")]);
        var result = await registry.GetAvailableAsync(
            new AIAgentDiscoveryContext(
                requiredCapabilities: new AIAgentCapabilities(supportsTools: true)),
            CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Discovery_ReturnsImmutableDefinitions()
    {
        var registry = AIAgentTestData.Registry([Agent("1.0.0")]);
        var result = await registry.GetAvailableAsync(new AIAgentDiscoveryContext(), CancellationToken.None);
        Assert.Throws<NotSupportedException>(() => ((IList<AIAgentDefinition>)result).Add(result[0]));
    }

    [Fact]
    public async Task Registry_PropagatesCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var registry = AIAgentTestData.Registry([Agent("1.0.0")]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registry.GetAsync(
            new AIAgentId("test-agent"),
            source.Token));
    }

    private static StubAIAgent Agent(
        string version,
        AIAgentAvailability availability = AIAgentAvailability.Enabled) =>
        new(AIAgentTestData.Definition(version: version, availability: availability));
}
