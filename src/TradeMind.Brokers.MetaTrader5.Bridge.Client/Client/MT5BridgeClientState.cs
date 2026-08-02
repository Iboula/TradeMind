namespace TradeMind.Brokers.MetaTrader5.Bridge.Client.Client;

public enum MT5BridgeClientState
{
    Disconnected,
    Connecting,
    Handshaking,
    Ready,
    Degraded,
    Reconnecting,
    Faulted,
    Closed
}

public sealed record MT5BridgeClientHealth(
    MT5BridgeClientState State,
    string? ProtocolVersion,
    string? BridgeVersion,
    string? TerminalState,
    bool DemoOnly,
    TimeSpan HeartbeatAge,
    int ReconnectCount,
    double LatencyMilliseconds,
    DateTimeOffset? LastSuccessfulHandshakeUtc,
    TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol.BridgeError? Error);
