namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;

internal sealed record MT5TerminalSafetyDecision(bool Allowed, string Code, string Message)
{
    public static MT5TerminalSafetyDecision Accept() => new(true, string.Empty, string.Empty);
}

internal sealed class MT5TerminalExecutionSafetyPolicy
{
    public MT5TerminalSafetyDecision Validate(TerminalCommand command, MT5TerminalGatewaySnapshot snapshot, DateTimeOffset now, TimeSpan heartbeatTimeout)
    {
        if (command.Command is not ("submit-order" or "modify-order" or "cancel-order" or "close-position")) return MT5TerminalSafetyDecision.Accept();
        if (command.Fields.TryGetValue("mode", out var mode) && mode.Equals("Live", StringComparison.OrdinalIgnoreCase)) return new(false, "LIVE_MODE_FORBIDDEN", "Live execution is disabled.");
        if (!command.Fields.TryGetValue("mode", out mode) || !mode.Equals("Demo", StringComparison.OrdinalIgnoreCase)) return new(false, "DEMO_CONFIRMATION_REQUIRED", "Demo mode must be explicit.");
        if (!snapshot.ConnectionState.Equals(MT5TerminalConnectionState.Connected)) return new(false, "TERMINAL_UNAVAILABLE", "The demo terminal is not connected.");
        if (!snapshot.AccountEnvironment.Equals("Demo", StringComparison.OrdinalIgnoreCase) || snapshot.ReadOnly || !snapshot.TradingEnabled) return new(false, "DEMO_EXECUTION_REQUIRED", "Execution requires a writable demo account.");
        if (snapshot.LastHeartbeatUtc is null || now - snapshot.LastHeartbeatUtc.Value > heartbeatTimeout) return new(false, "HEARTBEAT_INVALID", "A recent terminal heartbeat is required.");
        if (!RequiredTrue(command.Fields, "demo_confirmation") || !RequiredTrue(command.Fields, "risk_approved") || !RequiredTrue(command.Fields, "trading_plan_valid") || !RequiredTrue(command.Fields, "execution_session_valid") || !RequiredTrue(command.Fields, "permission") || !RequiredTrue(command.Fields, "capability") || !RequiredTrue(command.Fields, "heartbeat_valid"))
        {
            return new(false, "EXECUTION_GUARDS_REQUIRED", "The demo execution guards were not satisfied.");
        }

        return MT5TerminalSafetyDecision.Accept();
    }

    private static bool RequiredTrue(IReadOnlyDictionary<string, string> fields, string name) => fields.TryGetValue(name, out var value) && value.Equals("true", StringComparison.OrdinalIgnoreCase);
}
