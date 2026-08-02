using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Client.Configuration;

internal static class BridgeProtocolVersionExtensions
{
    public static bool TryParse(string? value, out BridgeProtocolVersion version)
    {
        version = BridgeProtocolVersion.Current;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor)) return false;
        try
        {
            version = new BridgeProtocolVersion(major, minor);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
