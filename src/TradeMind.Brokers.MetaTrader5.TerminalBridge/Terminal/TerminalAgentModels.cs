namespace TradeMind.Brokers.MetaTrader5.TerminalBridge.Terminal;

public sealed record TerminalAgentSnapshot(
    bool Connected,
    bool DemoAccount,
    string AccountEnvironment,
    bool TradingEnabled,
    bool ReadOnly,
    string ProtocolVersion,
    string TerminalVersion,
    int TerminalBuild,
    string TerminalArchitecture,
    string AccountId,
    IReadOnlyList<string> Capabilities,
    DateTimeOffset? LastHeartbeatUtc,
    int ReconnectCount)
{
    public bool IsReady(DateTimeOffset now, TimeSpan heartbeatTimeout) =>
        Connected &&
        DemoAccount &&
        AccountEnvironment.Equals("Demo", StringComparison.OrdinalIgnoreCase) &&
        TradingEnabled &&
        !ReadOnly &&
        LastHeartbeatUtc is not null &&
        now - LastHeartbeatUtc.Value <= heartbeatTimeout;
}

public sealed record PendingAgentCommand(string Id, string Command, IReadOnlyDictionary<string, string> Fields);

public sealed record AgentCommandResult(bool Success, string Code, string Message, IReadOnlyDictionary<string, string> Fields);
