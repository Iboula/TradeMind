using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Configuration;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Protocol;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Terminal;

namespace TradeMind.Brokers.MetaTrader5.TerminalBridge.Application;

public sealed class TerminalBridgeApplication(
    IOptions<TerminalBridgeOptions> options,
    ITerminalAgentSession agent,
    TimeProvider timeProvider,
    ILogger<TerminalBridgeApplication> logger)
{
    private static readonly string[] ReadOnlyCommands = ["get-accounts", "get-account", "get-instrument", "get-orders", "get-positions", "heartbeat"];
    private readonly TerminalBridgeOptions configuration = options.Value;
    private readonly SemaphoreSlim concurrencyGate = new(options.Value.MaxConcurrentRequests, options.Value.MaxConcurrentRequests);

    public TerminalHandshakeResponse Handshake(TerminalHandshakeRequest request)
    {
        if (!string.Equals(request.ProtocolVersion, configuration.ProtocolVersion, StringComparison.Ordinal)) return HandshakeFailure("PROTOCOL_MISMATCH");
        var snapshot = agent.Snapshot;
        if (!IsReady(snapshot)) return HandshakeFailure(snapshot.Connected ? "DEMO_ACCOUNT_REQUIRED" : "TERMINAL_NOT_READY");
        return new(true, string.Empty, snapshot.ProtocolVersion, snapshot.TerminalVersion, snapshot.TerminalBuild, snapshot.TerminalArchitecture, snapshot.AccountEnvironment, snapshot.DemoAccount, snapshot.TradingEnabled, snapshot.ReadOnly, snapshot.AccountId, snapshot.Capabilities);
    }

    public TerminalPingResponse Ping(TerminalPingRequest request)
    {
        var snapshot = agent.Snapshot;
        return IsReady(snapshot)
            ? new(true, string.Empty, snapshot.TerminalVersion, snapshot.LastHeartbeatUtc)
            : new(false, "TERMINAL_NOT_READY", snapshot.TerminalVersion, snapshot.LastHeartbeatUtc);
    }

    public async Task<TerminalExecuteResponse> ExecuteAsync(TerminalExecuteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Command)) return Failure("UNKNOWN", "The terminal command is required.");
        var command = request.Command.Trim();
        var fields = NormalizeFields(request.Fields);
        if (!ReadOnlyCommands.Contains(command, StringComparer.Ordinal))
        {
            logger.LogWarning("A terminal write operation was rejected because Phase 1 is read-only.");
            return Failure("LIVE_MODE_FORBIDDEN", "Write operations are disabled in Phase 1.");
        }
        if (!IsReady(agent.Snapshot)) return Failure("TERMINAL_NOT_READY", "The terminal agent is not ready.");
        if (command == "heartbeat") return new(true, "HEARTBEAT", "Heartbeat acknowledged", new Dictionary<string, string> { ["heartbeat_at_utc"] = timeProvider.GetUtcNow().ToString("O") });

        var acquired = false;
        try
        {
            await concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            var result = await agent.EnqueueAsync(command, fields, TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds), cancellationToken).ConfigureAwait(false);
            return new(result.Success, result.Code, result.Message, result.Fields);
        }
        finally
        {
            if (acquired) concurrencyGate.Release();
        }
    }

    public AgentPollResponse Poll(AgentPollRequest request)
    {
        if (!string.Equals(request.ProtocolVersion, configuration.ProtocolVersion, StringComparison.Ordinal)) return new(false, "PROTOCOL_MISMATCH", null);
        if (request.AccountEnvironment is not null && request.AccountEnvironment.Equals("Live", StringComparison.OrdinalIgnoreCase)) return new(false, "LIVE_MODE_FORBIDDEN", null);
        if (!request.DemoAccount || !string.Equals(request.AccountEnvironment, "Demo", StringComparison.OrdinalIgnoreCase)) return new(false, "DEMO_ACCOUNT_REQUIRED", null);
        if (request.TerminalBuild < configuration.MinimumSupportedTerminalBuild || request.TerminalBuild > configuration.MaximumSupportedTerminalBuild) return new(false, "TERMINAL_NOT_READY", null);
        if (!string.Equals(request.TerminalArchitecture, configuration.SupportedTerminalArchitecture, StringComparison.OrdinalIgnoreCase)) return new(false, "TERMINAL_NOT_READY", null);
        agent.Update(request, timeProvider.GetUtcNow());
        var command = agent.Dequeue();
        return new(true, "AGENT_READY", command is null ? null : new AgentCommand(command.Id, command.Command, command.Fields));
    }

    public AgentResultResponse Complete(AgentResultRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CommandId)) return new(false, "UNKNOWN");
        var result = new AgentCommandResult(request.Success, request.Code?.Trim() ?? "UNKNOWN", request.Message?.Trim() ?? string.Empty, NormalizeFields(request.Fields));
        return agent.Complete(request.CommandId, result) ? new(true, "RESULT_ACCEPTED") : new(false, "UNKNOWN");
    }

    public TerminalHealthResponse Health()
    {
        var snapshot = agent.Snapshot;
        var ready = IsReady(snapshot);
        return new(ready ? "ready" : "not-ready", configuration.ProtocolVersion, snapshot.Connected, snapshot.DemoAccount, snapshot.AccountEnvironment, snapshot.TradingEnabled, snapshot.ReadOnly, snapshot.TerminalVersion, snapshot.TerminalBuild, snapshot.TerminalArchitecture, snapshot.LastHeartbeatUtc, snapshot.ReconnectCount);
    }

    public void Disconnect() => agent.Disconnect(timeProvider.GetUtcNow());

    private bool IsReady(TerminalAgentSnapshot snapshot) => configuration.Enabled && configuration.DemoOnly && !configuration.AllowLive && snapshot.IsReady(timeProvider.GetUtcNow(), TimeSpan.FromSeconds(configuration.AgentHeartbeatTimeoutSeconds));

    private static TerminalHandshakeResponse HandshakeFailure(string code) => new(false, code, "1.0", string.Empty, 0, string.Empty, "Demo", false, false, true, string.Empty, []);
    private static TerminalExecuteResponse Failure(string code, string message) => new(false, code, message, new Dictionary<string, string>());

    private static IReadOnlyDictionary<string, string> NormalizeFields(IReadOnlyDictionary<string, string>? fields)
    {
        if (fields is null || fields.Count == 0) return new Dictionary<string, string>();
        if (fields.Count > 64) throw new ArgumentException("Too many command fields.", nameof(fields));
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Key) || field.Key.Length > 64 || field.Value.Length > 4096) throw new ArgumentException("A command field is invalid.", nameof(fields));
            normalized[field.Key.Trim()] = field.Value;
        }
        return normalized;
    }
}
