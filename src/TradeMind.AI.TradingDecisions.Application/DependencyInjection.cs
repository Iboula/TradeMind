using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.TradingDecisions.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindTradingDecisions(
        this IServiceCollection services,
        Action<TradingDecisionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        var options = services.AddOptions<TradingDecisionOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<TradingDecisionOptions>, TradingDecisionOptionsValidator>());
        services.TryAddSingleton<ITradingDecisionEligibilityPolicy, DefaultTradingDecisionEligibilityPolicy>();
        services.TryAddSingleton<ITradingDecisionPolicy, DefaultTradingDecisionPolicy>();
        services.TryAddScoped<ITradingDecisionEngine, TradingDecisionEngine>();
        return services;
    }
}
