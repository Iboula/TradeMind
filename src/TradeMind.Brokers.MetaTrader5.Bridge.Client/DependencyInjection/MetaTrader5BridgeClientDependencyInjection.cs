using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Client;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Configuration;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Security;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Client.DependencyInjection;

public static class MetaTrader5BridgeClientDependencyInjection
{
    public static IServiceCollection AddTradeMindMetaTrader5BridgeClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<MT5BridgeClientOptions>().Bind(configuration.GetSection(MT5BridgeClientOptions.SectionName)).ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<MT5BridgeClientOptions>, MT5BridgeClientOptionsValidator>();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddHttpClient<MT5BridgeClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<MT5BridgeClientOptions>>().Value;
            client.BaseAddress = new Uri(options.Endpoint, UriKind.Absolute);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.TryAddSingleton<IBridgeAuthenticator>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<MT5BridgeClientOptions>>().Value;
            return options.AuthenticationMode == BridgeAuthenticationMode.MutualTls ? new MutualTlsBridgeAuthenticator() : new SignedServiceTokenBridgeAuthenticator(options);
        });
        services.AddSingleton<TradeMind.Brokers.MetaTrader5.Bridge.IMT5Bridge>(provider => provider.GetRequiredService<MT5BridgeClient>());
        services.AddSingleton<IMT5BridgeClientHealth>(provider => provider.GetRequiredService<MT5BridgeClient>());
        return services;
    }
}
