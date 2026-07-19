using Microsoft.Extensions.DependencyInjection;
using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Infrastructure;

namespace TradeMind.AI.Context.Tests;

public sealed class ContextDependencyInjectionTests
{
    [Fact]
    public void DependencyInjection_ShouldRegisterContextPipelineAndDefaultProviders()
    {
        var services = new ServiceCollection();

        services.AddTradeMindContextEngine();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IContextProviderRegistry)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMarketContextBuilder)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Equal(
            3,
            services.Count(descriptor => descriptor.ServiceType == typeof(IContextProvider)));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(TimeProvider)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }
}
