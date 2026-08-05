namespace TradeMind.Brokers.MetaTrader5.TerminalBridge.Protocol;

public sealed record TerminalHandshakeRequest(string? ProtocolVersion);

public sealed record TerminalHandshakeResponse(
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

public sealed record TerminalPingRequest(DateTimeOffset? RequestedAtUtc);

public sealed record TerminalPingResponse(bool Success, string ErrorCode, string TerminalVersion, DateTimeOffset? HeartbeatUtc);

public sealed record TerminalExecuteRequest(string? Command, IReadOnlyDictionary<string, string>? Fields);

public sealed record TerminalExecuteResponse(bool Success, string Code, string Message, IReadOnlyDictionary<string, string> Fields);

public sealed record TerminalDisconnectResponse(bool Success);

public sealed record TerminalHealthResponse(
    string Status,
    string ProtocolVersion,
    bool AgentConnected,
    bool DemoAccount,
    string AccountEnvironment,
    bool TradingEnabled,
    bool ReadOnly,
    string TerminalVersion,
    int TerminalBuild,
    string TerminalArchitecture,
    DateTimeOffset? LastHeartbeatUtc,
    int ReconnectCount);

public sealed record AgentPollRequest(
    string? ProtocolVersion,
    string? TerminalVersion,
    int TerminalBuild,
    string? TerminalArchitecture,
    bool DemoAccount,
    string? AccountEnvironment,
    bool TradingEnabled,
    bool ReadOnly,
    string? AccountId,
    IReadOnlyList<string>? Capabilities);

public sealed record AgentPollResponse(bool Success, string Code, AgentCommand? Command);

public sealed record AgentCommand(string Id, string Command, IReadOnlyDictionary<string, string> Fields);

public sealed record AgentResultRequest(string? CommandId, bool Success, string? Code, string? Message, IReadOnlyDictionary<string, string>? Fields);

public sealed record AgentResultResponse(bool Success, string Code);
