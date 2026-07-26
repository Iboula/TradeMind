using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Tests;

public sealed class AIToolRegistryTests
{
    [Fact]
    public async Task Registry_ShouldReturnTool()
    {
        var tool = Tool();
        Assert.Same(tool, await Registry([tool]).GetAsync(tool.Definition.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Registry_ShouldFailForUnknownTool()
    {
        await Assert.ThrowsAsync<AIToolNotFoundException>(() =>
            Registry([]).GetAsync(new AIToolId("unknown"), CancellationToken.None));
    }

    [Fact]
    public void Registry_ShouldRejectDuplicateTool()
    {
        var definition = AIToolTestData.Definition();
        Assert.Throws<InvalidOperationException>(() => Registry([new StubAITool(definition), new StubAITool(definition)]));
    }

    [Fact]
    public async Task Registry_ExistsAsync_ShouldReturnExpectedValue()
    {
        var registry = Registry([Tool()]);
        Assert.True(await registry.ExistsAsync(new AIToolId("test-tool"), CancellationToken.None));
        Assert.False(await registry.ExistsAsync(new AIToolId("unknown"), CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_ShouldReturnAuthorizedTools()
    {
        var tool = Tool(AIToolTestData.Definition(permissions: ["tool.use"]));
        var result = await Registry([tool]).GetAvailableAsync(
            new AIToolDiscoveryContext(permissions: ["tool.use"]),
            CancellationToken.None);

        Assert.Equal([tool.Definition], result);
    }

    [Fact]
    public async Task Discovery_ShouldExcludeDisabledTools()
    {
        var result = await Registry([Tool(AIToolTestData.Definition(availability: AIToolAvailability.Disabled))])
            .GetAvailableAsync(new AIToolDiscoveryContext(), CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Discovery_ShouldExcludeDevelopmentToolsWhenDisabled()
    {
        var result = await Registry(
                [Tool(AIToolTestData.Definition(availability: AIToolAvailability.DevelopmentOnly))],
                enableDevelopmentTools: false)
            .GetAvailableAsync(new AIToolDiscoveryContext(), CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Discovery_ShouldFilterByPermission()
    {
        var result = await Registry([Tool(AIToolTestData.Definition(permissions: ["tool.use"]))])
            .GetAvailableAsync(new AIToolDiscoveryContext(permissions: ["other"]), CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Discovery_ShouldFilterBySideEffect()
    {
        var result = await Registry([Tool(AIToolTestData.Definition(sideEffect: AIToolSideEffectLevel.ReversibleWrite))])
            .GetAvailableAsync(
                new AIToolDiscoveryContext(maximumAllowedSideEffectLevel: AIToolSideEffectLevel.ReadOnly),
                CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Discovery_ShouldReturnImmutableDefinitions()
    {
        var result = await Registry([Tool()]).GetAvailableAsync(new AIToolDiscoveryContext(), CancellationToken.None);
        Assert.Throws<NotSupportedException>(() => ((IList<AIToolDefinition>)result).Add(AIToolTestData.Definition("other")));
    }

    [Fact]
    public async Task Registry_ShouldPropagateCancellationToken()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Registry([Tool()]).GetAvailableAsync(new AIToolDiscoveryContext(), source.Token));
    }

    [Fact]
    public async Task Discovery_ShouldFilterTenantUserAgentScenarioAndTags()
    {
        var definition = AIToolTestData.Definition(
            tags: ["math"],
            allowedScenarios: ["Coach"],
            allowedTenants: ["tenant-a"],
            allowedUsers: ["user-a"],
            allowedAgents: ["agent-a"]);
        var registry = Registry([Tool(definition)]);

        var allowed = await registry.GetAvailableAsync(
            new AIToolDiscoveryContext(
                tenantId: "tenant-a",
                userId: "user-a",
                agentId: "agent-a",
                scenario: "Coach",
                tags: ["math"]),
            CancellationToken.None);
        var denied = await registry.GetAvailableAsync(
            new AIToolDiscoveryContext(tenantId: "tenant-b", scenario: "Coach", tags: ["math"]),
            CancellationToken.None);

        Assert.Single(allowed);
        Assert.Empty(denied);
    }

    private static StubAITool Tool(AIToolDefinition? definition = null) =>
        new(definition ?? AIToolTestData.Definition());

    private static InMemoryAIToolRegistry Registry(
        IEnumerable<IAITool> tools,
        bool enableDevelopmentTools = false) =>
        new(
            tools,
            Options.Create(new AIToolEngineOptions { EnableDevelopmentTools = enableDevelopmentTools }),
            NullLogger<InMemoryAIToolRegistry>.Instance);
}
