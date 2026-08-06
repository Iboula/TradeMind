using System.Globalization;
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
    private static readonly string[] DemoWriteCommands = ["submit-order", "close-position"];
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
            var writeDecision = ValidateDemoWrite(command, fields);
            if (!writeDecision.Allowed)
            {
                logger.LogWarning("A terminal write operation was rejected by the demo safety gate with code {Code}.", writeDecision.Code);
                return Failure(writeDecision.Code, writeDecision.Message);
            }
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

    private DemoWriteDecision ValidateDemoWrite(string command, IReadOnlyDictionary<string, string> fields)
    {
        if (!configuration.EnableWriteTests) return new(false, "LIVE_MODE_FORBIDDEN", "Demo write tests are disabled.");
        if (!DemoWriteCommands.Contains(command, StringComparer.Ordinal)) return new(false, "LIVE_MODE_FORBIDDEN", "Only the bounded demo order smoke test is supported.");
        if (fields.TryGetValue("mode", out var mode) && mode.Equals("Live", StringComparison.OrdinalIgnoreCase)) return new(false, "LIVE_MODE_FORBIDDEN", "Live execution is disabled.");
        if (!RequiredTrue(fields, "demo_confirmation") || !RequiredTrue(fields, "risk_approved") || !RequiredTrue(fields, "trading_plan_valid") || !RequiredTrue(fields, "execution_session_valid") || !RequiredTrue(fields, "permission") || !RequiredTrue(fields, "capability") || !RequiredTrue(fields, "heartbeat_valid"))
            return new(false, "EXECUTION_GUARDS_REQUIRED", "The demo execution guards were not satisfied.");
        if (!fields.TryGetValue("mode", out mode) || !mode.Equals("Demo", StringComparison.OrdinalIgnoreCase)) return new(false, "DEMO_CONFIRMATION_REQUIRED", "Demo mode must be explicit.");
        if (command == "close-position") return Required(fields, "position_id") ? DemoWriteDecision.Accept() : new(false, "INVALID_REQUEST", "A position id is required for cleanup.");
        if (!fields.TryGetValue("instrument", out var instrument) || !instrument.Equals(configuration.WriteTestSymbol, StringComparison.OrdinalIgnoreCase)) return new(false, "INVALID_REQUEST", "The demo smoke test is restricted to the configured symbol.");
        if (!fields.TryGetValue("order_type", out var orderType) || !orderType.Equals("Market", StringComparison.OrdinalIgnoreCase)) return new(false, "INVALID_REQUEST", "Only one market order is permitted.");
        if (!fields.TryGetValue("quantity", out var quantityText) || !decimal.TryParse(quantityText, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) || quantity != configuration.WriteTestMinimumVolume) return new(false, "INVALID_QUANTITY", "The demo smoke test requires the configured minimum volume.");
        if (!Required(fields, "account_id") || !Required(fields, "client_order_id")) return new(false, "INVALID_REQUEST", "The demo order identifiers are required.");
        return DemoWriteDecision.Accept();
    }

    private static bool RequiredTrue(IReadOnlyDictionary<string, string> fields, string name) => fields.TryGetValue(name, out var value) && value.Equals("true", StringComparison.OrdinalIgnoreCase);
    private static bool Required(IReadOnlyDictionary<string, string> fields, string name) => fields.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value);

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

    private sealed record DemoWriteDecision(bool Allowed, string Code, string Message)
    {
        public static DemoWriteDecision Accept() => new(true, string.Empty, string.Empty);
    }
}
