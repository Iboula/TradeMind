using System.Net.Http.Headers;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Configuration;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Client.Security;

public interface IBridgeAuthenticator
{
    void Apply(HttpRequestMessage request);
}

public sealed class MutualTlsBridgeAuthenticator : IBridgeAuthenticator
{
    public void Apply(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.TryAddWithoutValidation("X-Bridge-Client-Certificate", "present");
    }
}

public sealed class SignedServiceTokenBridgeAuthenticator(MT5BridgeClientOptions options) : IBridgeAuthenticator
{
    public void Apply(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(options.ServiceToken)) throw new InvalidOperationException("A service token must be supplied at runtime.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ServiceToken);
    }
}
