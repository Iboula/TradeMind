namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Configuration;

internal enum MT5TerminalArchitecture
{
    X64,
    X86
}

internal static class MT5TerminalArchitectureParser
{
    public static bool TryParse(string? value, out MT5TerminalArchitecture architecture)
    {
        if (Enum.TryParse(value, true, out architecture) && Enum.IsDefined(architecture)) return true;
        architecture = MT5TerminalArchitecture.X64;
        return false;
    }
}
