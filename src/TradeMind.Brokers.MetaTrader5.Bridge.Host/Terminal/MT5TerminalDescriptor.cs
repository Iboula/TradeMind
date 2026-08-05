namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;

internal sealed record MT5TerminalDescriptor(
    string Path,
    string Version,
    int Build,
    string Architecture,
    bool IsRunning);

internal interface IMT5TerminalDiscovery
{
    Task<MT5TerminalDescriptor> DiscoverAsync(string terminalPath, CancellationToken cancellationToken);
    bool IsRunning(string terminalPath);
}

internal interface IMT5TerminalProcessController
{
    Task StartAsync(string terminalPath, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
