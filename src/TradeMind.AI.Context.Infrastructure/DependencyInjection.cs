using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradeMind.AI.Context.Application;

namespace TradeMind.AI.Context.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindContextEngine(
        this IServiceCollection services,
        Action<ContextEngineOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.AddOptions<ContextEngineOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IContextFreshnessPolicy, ContextFreshnessPolicy>();
        services.TryAddSingleton<IContextQualityPolicy, ContextQualityPolicy>();
        services.TryAddSingleton<IContextSizePolicy, ContextSizePolicy>();
        services.TryAddScoped<IContextProviderRegistry, ContextProviderRegistry>();
        services.TryAddScoped<IMarketContextBuilder, MarketContextBuilder>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IContextProvider, MarketSnapshotContextProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IContextProvider, KnowledgeContextProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IContextProvider, MemoryContextProvider>());
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<ApplicationAssemblyMarker>());

        return services;
    }
}
