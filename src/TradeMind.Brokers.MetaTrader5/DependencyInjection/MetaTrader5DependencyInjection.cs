using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.MetaTrader5.Bridge;
using TradeMind.Brokers.MetaTrader5.Configuration;
using TradeMind.Brokers.MetaTrader5.Connection;
using TradeMind.Brokers.MetaTrader5.Execution;
using TradeMind.Brokers.MetaTrader5.Health;
using TradeMind.Brokers.MetaTrader5.Protocol;
using TradeMind.Brokers.MetaTrader5.Retry;
using TradeMind.Brokers.MetaTrader5.Serialization;

namespace TradeMind.Brokers.MetaTrader5.DependencyInjection;

public static class MetaTrader5DependencyInjection
{
    public static IServiceCollection AddTradeMindMetaTrader5(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<MT5Options>().Bind(configuration.GetSection(MT5Options.SectionName)).ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<MT5Options>, MT5OptionsValidator>();
        services.TryAddSingleton(provider => provider.GetRequiredService<IOptions<MT5Options>>().Value);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<IMT5Serializer, MT5JsonSerializer>();
        services.TryAddSingleton<IMT5Bridge, MT5SimulationBridge>();
        services.TryAddSingleton<IMT5ConnectionFactory, MT5ConnectionFactory>();
        services.TryAddSingleton<IMT5Protocol, MT5Protocol>();
        services.TryAddSingleton<IMT5ReconnectPolicy, MT5ReconnectPolicy>();
        services.TryAddSingleton<IMT5RetryPolicy, MT5RetryPolicy>();
        services.TryAddSingleton<IMT5HeartbeatService, MT5HeartbeatService>();
        services.TryAddSingleton<IMT5HealthService, MT5HealthService>();
        services.AddSingleton<IBrokerConnector, MT5BrokerConnector>();
        return services;
    }
}
