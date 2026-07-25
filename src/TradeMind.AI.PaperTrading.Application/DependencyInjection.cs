using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.PaperTrading.Domain;

namespace TradeMind.AI.PaperTrading.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindPaperTrading(
        this IServiceCollection services,
        Action<PaperTradingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var options = services.AddOptions<PaperTradingOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PaperTradingOptions>, PaperTradingOptionsValidator>());
        services.TryAddSingleton<IPaperTradingEligibilityPolicy, DefaultPaperTradingEligibilityPolicy>();
        services.TryAddScoped<IPaperTradingSimulator, PaperTradingSimulator>();
        return services;
    }
}
