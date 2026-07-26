using Microsoft.Extensions.DependencyInjection;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Application;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Tests;

public sealed class AIToolDependencyInjectionTests
{
    [Fact]
    public void DependencyInjection_ShouldResolveRegistry()
    {
        using var provider = ToolServices();
        Assert.NotNull(provider.GetRequiredService<IAIToolRegistry>());
    }

    [Fact]
    public void DependencyInjection_ShouldResolveExecutor()
    {
        using var provider = ToolServices();
        Assert.NotNull(provider.GetRequiredService<IAIToolExecutor>());
    }

    [Fact]
    public void DependencyInjection_ShouldResolveAuthorizer()
    {
        using var provider = ToolServices();
        Assert.NotNull(provider.GetRequiredService<IAIToolAuthorizer>());
    }

    [Fact]
    public void DependencyInjection_ShouldResolveArgumentValidator()
    {
        using var provider = ToolServices();
        Assert.NotNull(provider.GetRequiredService<IAIToolArgumentValidator>());
    }

    [Fact]
    public void DependencyInjection_ShouldResolveResultComposer()
    {
        using var provider = ToolServices();
        Assert.NotNull(provider.GetRequiredService<IAIToolResultComposer>());
    }

    [Fact]
    public async Task DependencyInjection_ShouldExposeEchoInDevelopment()
    {
        using var provider = ToolServices(enableDevelopmentTools: true);
        var definitions = await provider.GetRequiredService<IAIToolRegistry>().GetAvailableAsync(
            new AIToolDiscoveryContext(),
            CancellationToken.None);
        Assert.Contains(definitions, definition => definition.Id == new AIToolId("echo"));
    }

    [Fact]
    public async Task DependencyInjection_ShouldExposeAddNumbers()
    {
        using var provider = ToolServices();
        var definitions = await provider.GetRequiredService<IAIToolRegistry>().GetAvailableAsync(
            new AIToolDiscoveryContext(),
            CancellationToken.None);
        Assert.Contains(definitions, definition => definition.Id == new AIToolId("add-numbers"));
    }

    [Fact]
    public void DependencyInjection_ShouldComposeWithAIOrchestration()
    {
        var services = BaseServices();
        services.AddTradeMindAIOrchestration();
        services.AddTradeMindAIToolOrchestration();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider.GetRequiredService<IAIOrchestrator>());
        Assert.Equal(7, provider.GetServices<IAIOrchestrationStep>().Count());
    }

    [Fact]
    public void DependencyInjection_ShouldKeepToolEngineOptional()
    {
        var services = BaseServices();
        services.AddTradeMindAIOrchestration();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Null(provider.GetService<IAIToolRegistry>());
        Assert.Equal(5, provider.GetServices<IAIOrchestrationStep>().Count());
    }

    [Fact]
    public async Task Orchestrator_ShouldWorkWithoutToolEngineWhenDisabled()
    {
        var services = BaseServices();
        services.AddTradeMindAIOrchestration();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var response = await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(
            new AIOrchestrationRequest("system", "user", "Scenario"),
            CancellationToken.None);

        Assert.Equal("provider-response", response.Content);
        Assert.False(response.ToolUsed);
    }

    [Fact]
    public async Task Orchestrator_ShouldFailClosedWhenEnabledToolEngineIsNotRegistered()
    {
        var chatProvider = new ToolFakeChatProvider();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChatProvider>(chatProvider);
        services.AddSingleton<IAIProviderMetadata>(new ToolFakeProviderMetadata());
        services.AddTradeMindAIOrchestration();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        var request = new AIOrchestrationRequest("system", "user", "Scenario")
        {
            Tool = new AIToolInvocationOptions(enabled: true, toolId: new AIToolId("add-numbers"))
        };

        await Assert.ThrowsAsync<AIOrchestrationValidationException>(() =>
            provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(request, CancellationToken.None));
        Assert.Equal(0, chatProvider.CallCount);
    }

    private static ServiceProvider ToolServices(bool enableDevelopmentTools = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTradeMindAITools(options => options.EnableDevelopmentTools = enableDevelopmentTools);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ServiceCollection BaseServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChatProvider>(new ToolFakeChatProvider());
        services.AddSingleton<IAIProviderMetadata>(new ToolFakeProviderMetadata());
        return services;
    }
}
