namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;

internal interface IMT5TerminalGateway
{
    string TerminalVersion { get; }
    bool IsAvailable { get; }
    MT5TerminalGatewaySnapshot Snapshot { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task<TerminalCommandResult> ExecuteAsync(TerminalCommand command, CancellationToken cancellationToken);
}

internal sealed record TerminalCommand(string Command, IReadOnlyDictionary<string, string> Fields);

internal sealed record TerminalCommandResult(bool Success, string Code, string Message, IReadOnlyDictionary<string, string> Fields);

internal enum MT5TerminalConnectionState
{
    Disconnected,
    Connecting,
    Authenticating,
    Connected,
    Reconnecting,
    Faulted,
    Closed
}

internal sealed record MT5TerminalGatewaySnapshot(
    MT5TerminalConnectionState ConnectionState,
    string TerminalVersion,
    int TerminalBuild,
    string TerminalArchitecture,
    string ProtocolVersion,
    string AccountEnvironment,
    bool TradingEnabled,
    bool ReadOnly,
    TimeSpan Latency,
    DateTimeOffset? LastHeartbeatUtc,
    DateTimeOffset? LastReconnectUtc,
    int ReconnectCount);
