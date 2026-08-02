namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;

internal interface IMT5TerminalTransport
{
    Task<TerminalBridgeHandshakeResult> HandshakeAsync(CancellationToken cancellationToken);
    Task<TerminalBridgePingResult> PingAsync(CancellationToken cancellationToken);
    Task<TerminalCommandResult> ExecuteAsync(TerminalCommand command, CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
}

internal sealed record TerminalBridgeHandshakeResult(
    bool Success,
    string ErrorCode,
    string ProtocolVersion,
    string TerminalVersion,
    int TerminalBuild,
    string TerminalArchitecture,
    string AccountEnvironment,
    bool DemoAccount,
    bool TradingEnabled,
    bool ReadOnly,
    string AccountId,
    IReadOnlyList<string> Capabilities);

internal sealed record TerminalBridgePingResult(bool Success, string ErrorCode, string TerminalVersion, DateTimeOffset? HeartbeatUtc);
