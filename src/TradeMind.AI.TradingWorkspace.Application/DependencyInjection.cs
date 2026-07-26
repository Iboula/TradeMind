using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.TradingWorkspace.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindTradingWorkspace(
        this IServiceCollection services,
        Action<TradingWorkspaceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var options = services.AddOptions<TradingWorkspaceOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TradingWorkspaceOptions>, TradingWorkspaceOptionsValidator>());
        services.TryAddSingleton<ITradingWorkspaceConsistencyPolicy, DefaultTradingWorkspaceConsistencyPolicy>();
        services.TryAddSingleton<ITradingWorkspaceFreshnessPolicy>(provider =>
            new DefaultTradingWorkspaceFreshnessPolicy(provider.GetRequiredService<IOptions<TradingWorkspaceOptions>>().Value));
        services.TryAddScoped<ITradingWorkspaceBuilder, TradingWorkspaceBuilder>();
        return services;
    }
}
