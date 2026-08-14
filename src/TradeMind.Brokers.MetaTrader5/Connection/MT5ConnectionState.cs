namespace TradeMind.Brokers.MetaTrader5.Connection;

public enum MT5ConnectionState
{
    Disconnected,
    Connecting,
    Authenticating,
    Connected,
    Reconnecting,
    Faulted,
    Closed
}

public sealed record MT5ConnectionSnapshot(
    string BridgeVersion,
    string ProtocolVersion,
    string TerminalVersion,
    TimeSpan Latency,
    bool Heartbeat,
    int ReconnectCount,
    MT5ConnectionState ConnectionState,
    DateTimeOffset? LastHeartbeat,
    DateTimeOffset? LastReconnect,
    string AccountEnvironment = "Unknown",
    bool TradingEnabled = false,
    bool ReadOnly = true);
