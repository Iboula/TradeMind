using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace TradeMind.Brokers.MetaTrader5.TerminalBridge.Configuration;

public sealed class TerminalBridgeOptions
{
    public const string SectionName = "TradeMind:Brokers:MetaTrader5:TerminalBridge";

    public bool Enabled { get; set; } = true;
    public string Urls { get; set; } = "https://localhost:5001";
    public string ProtocolVersion { get; set; } = "1.0";
    public string TerminalVersion { get; set; } = "mql5-agent";
    public bool DemoOnly { get; set; } = true;
    public bool AllowLive { get; set; }
    public bool AllowInsecureDemoTransport { get; set; }
    public int MaxConcurrentRequests { get; set; } = 4;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxPayloadBytes { get; set; } = 64 * 1024;
    public int MaximumClockSkewSeconds { get; set; } = 30;
    public int ReplayWindowSeconds { get; set; } = 120;
    public int MaxReplayEntries { get; set; } = 4096;
    public string AuthenticationMode { get; set; } = "SignedToken";
    public string TokenConfigurationKey { get; set; } = "MT5_TERMINAL_BRIDGE_TOKEN";
    public string AgentTokenConfigurationKey { get; set; } = "MT5_TERMINAL_AGENT_TOKEN";
    public bool RequireAgent { get; set; } = true;
    public int AgentHeartbeatTimeoutSeconds { get; set; } = 15;
    public int MinimumSupportedTerminalBuild { get; set; } = 3000;
    public int MaximumSupportedTerminalBuild { get; set; } = 99999;
    public string SupportedTerminalArchitecture { get; set; } = "x64";
    public int Mql5PollTimeoutSeconds { get; set; } = 25;
    public int Mql5PollIntervalSeconds { get; set; } = 1;
    public bool EnableWriteTests { get; set; }
}

public sealed class TerminalBridgeOptionsValidator : IValidateOptions<TerminalBridgeOptions>
{
    public ValidateOptionsResult Validate(string? name, TerminalBridgeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Urls)) errors.Add("Urls is required.");
        if (options.Urls.Contains(',', StringComparison.Ordinal)) errors.Add("Urls must contain one local endpoint.");
        if (!Uri.TryCreate(options.Urls, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("http" or "https") || !IsLoopback(endpoint.Host)) errors.Add("Urls must be an absolute localhost HTTP or HTTPS endpoint.");
        if (options.ProtocolVersion != "1.0") errors.Add("ProtocolVersion must be 1.0.");
        if (!options.DemoOnly || options.AllowLive) errors.Add("The terminal bridge must remain demo-only.");
        if (options.AuthenticationMode != "SignedToken") errors.Add("AuthenticationMode must be SignedToken.");
        if (string.IsNullOrWhiteSpace(options.TokenConfigurationKey)) errors.Add("TokenConfigurationKey is required.");
        if (string.IsNullOrWhiteSpace(options.AgentTokenConfigurationKey)) errors.Add("AgentTokenConfigurationKey is required.");
        if (options.MaxConcurrentRequests is < 1 or > 64) errors.Add("MaxConcurrentRequests must be between 1 and 64.");
        if (options.RequestTimeoutSeconds is < 1 or > 300) errors.Add("RequestTimeoutSeconds must be between 1 and 300.");
        if (options.MaxPayloadBytes is < 1024 or > 1024 * 1024) errors.Add("MaxPayloadBytes is outside the supported range.");
        if (options.MaximumClockSkewSeconds is < 1 or > 300) errors.Add("MaximumClockSkewSeconds must be between 1 and 300.");
        if (options.ReplayWindowSeconds is < 10 or > 3600) errors.Add("ReplayWindowSeconds must be between 10 and 3600.");
        if (options.MaxReplayEntries is < 128 or > 100000) errors.Add("MaxReplayEntries is outside the supported range.");
        if (options.AgentHeartbeatTimeoutSeconds is < 2 or > 300) errors.Add("AgentHeartbeatTimeoutSeconds must be between 2 and 300.");
        if (options.MinimumSupportedTerminalBuild < 1 || options.MaximumSupportedTerminalBuild < options.MinimumSupportedTerminalBuild) errors.Add("Terminal build range is invalid.");
        if (options.SupportedTerminalArchitecture is not ("x64" or "x86")) errors.Add("SupportedTerminalArchitecture must be x64 or x86.");
        if (options.Mql5PollTimeoutSeconds is < 1 or > 120) errors.Add("Mql5PollTimeoutSeconds must be between 1 and 120.");
        if (options.Mql5PollIntervalSeconds is < 1 or > 30) errors.Add("Mql5PollIntervalSeconds must be between 1 and 30.");
        if (endpoint?.Scheme == "http" && !options.AllowInsecureDemoTransport) errors.Add("HTTP requires explicit local demo opt-in.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool IsLoopback(string host) => host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
}

public sealed class TerminalBridgeSecretProvider(IConfiguration configuration)
{
    public string? Get(string key)
    {
        var value = configuration[key] ?? Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(value)) return value;
        return key.Equals("MT5_TERMINAL_BRIDGE_TOKEN", StringComparison.Ordinal)
            ? configuration["TradeMind:Brokers:MetaTrader5:Bridge:Authentication:Token"]
            : null;
    }
}
