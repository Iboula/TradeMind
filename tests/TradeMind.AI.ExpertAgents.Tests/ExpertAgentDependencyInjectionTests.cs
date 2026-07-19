using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TradeMind.AI.ExpertAgents.Application;
using TradeMind.AI.ExpertAgents.Domain;

namespace TradeMind.AI.ExpertAgents.Tests;

public sealed class ExpertAgentDependencyInjectionTests
{
    [Fact]
    public void DependencyInjection_ShouldRegisterPipelineAndResolveTestAgent()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTradeMindExpertAgents();
        services.AddSingleton<IExpertAgent>(new TestExpertAgent(ExpertAgentTestData.Descriptor()));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        using var scope = provider.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<IExpertAgentExecutor>();
        var registry = provider.GetRequiredService<IExpertAgentRegistry>();

        Assert.NotNull(executor);
        Assert.Single(registry.GetAvailable());
    }

    [Fact]
    public void DependencyInjection_ShouldValidateInvalidOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTradeMindExpertAgents(options =>
        {
            options.DefaultExecutionTimeout = TimeSpan.FromMinutes(2);
            options.MaximumExecutionTimeout = TimeSpan.FromMinutes(1);
        });

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IExpertAgentExecutor>());
    }

    [Fact]
    public void DependencyInjection_ShouldKeepRegistrySingletonAndExecutorScoped()
    {
        var services = new ServiceCollection();
        services.AddTradeMindExpertAgents();

        var registry = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IExpertAgentRegistry));
        var executor = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IExpertAgentExecutor));

        Assert.Equal(ServiceLifetime.Singleton, registry.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, executor.Lifetime);
    }
}
