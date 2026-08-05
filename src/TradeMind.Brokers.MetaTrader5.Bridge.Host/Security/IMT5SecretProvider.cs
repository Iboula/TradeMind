using Microsoft.Extensions.Configuration;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Security;

internal interface IMT5SecretProvider
{
    string? GetSecret(string key);
}

/// <summary>
/// Reads runtime secrets from the normal .NET configuration pipeline. User Secrets,
/// environment variables and a future vault provider can all populate this pipeline.
/// </summary>
internal sealed class ConfigurationMT5SecretProvider(IConfiguration configuration) : IMT5SecretProvider
{
    public string? GetSecret(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return configuration[key]
            ?? configuration[$"TradeMind:Brokers:MetaTrader5:BridgeHost:{key}"]
            ?? configuration["TradeMind:Brokers:MetaTrader5:Bridge:Authentication:Token"];
    }
}
