using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.RiskEngine.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindRiskEngine(
        this IServiceCollection services,
        Action<RiskEngineOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        var options = services.AddOptions<RiskEngineOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<RiskEngineOptions>, RiskEngineOptionsValidator>());
        services.TryAddSingleton<IRiskEligibilityPolicy, DefaultRiskEligibilityPolicy>();
        services.TryAddSingleton<IPositionSizingPolicy, DefaultPositionSizingPolicy>();
        services.TryAddSingleton<IRiskReductionPolicy, DefaultRiskReductionPolicy>();
        services.TryAddScoped<IRiskEngine, RiskEngine>();
        return services;
    }
}
