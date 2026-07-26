using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.TradingPlans.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindTradingPlans(
        this IServiceCollection services,
        Action<TradingPlanOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        var options = services.AddOptions<TradingPlanOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<TradingPlanOptions>, TradingPlanOptionsValidator>());
        services.TryAddSingleton<ITradingPlanEligibilityPolicy, DefaultTradingPlanEligibilityPolicy>();
        services.TryAddSingleton<ITradingPlanExpirationPolicy, DefaultTradingPlanExpirationPolicy>();
        services.TryAddScoped<ITradingPlanGenerator, TradingPlanGenerator>();
        return services;
    }
}
