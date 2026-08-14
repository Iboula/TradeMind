using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Configuration;

public sealed class MT5BridgeHostOptions
{
    public const string SectionName = "TradeMind:Brokers:MetaTrader5:BridgeHost";

    public string ProtocolVersion { get; set; } = "1.0";
    public string BridgeVersion { get; set; } = "simulation-host-1.0";
    public string AdapterVersion { get; set; } = "1.0.0";
    public string Environment { get; set; } = "Demo";
    public string AccountEnvironment { get; set; } = "Demo";
    public bool DemoOnly { get; set; } = true;
    public bool AllowLive { get; set; }
    public bool RequireTls { get; set; } = true;
    public bool AllowInsecureDemoTransport { get; set; }
    public string AuthenticationMode { get; set; } = "MutualTls";
    public int MaximumClockSkewSeconds { get; set; } = 30;
    public int MaxConcurrentRequests { get; set; } = 4;
    public int ReplayWindowSeconds { get; set; } = 120;
    public string GatewayMode { get; set; } = "Simulation";
    public string TerminalPath { get; set; } = string.Empty;
    public Uri? TerminalBridgeEndpoint { get; set; }
    public string TerminalBridgeTokenConfigurationKey { get; set; } = "MT5_TERMINAL_BRIDGE_TOKEN";
    public bool RequireTerminalBridgeAuthentication { get; set; } = true;
    public bool AutoStartTerminal { get; set; }
    public int TerminalStartupTimeoutSeconds { get; set; } = 30;
    public int TerminalShutdownTimeoutSeconds { get; set; } = 10;
    public int TerminalHeartbeatIntervalSeconds { get; set; } = 5;
    public int TerminalHeartbeatTimeoutSeconds { get; set; } = 5;
    public int ReconnectMaximumAttempts { get; set; } = 3;
    public int ReconnectBackoffMilliseconds { get; set; } = 250;
    public int MinimumSupportedTerminalBuild { get; set; } = 3000;
    public int MaximumSupportedTerminalBuild { get; set; } = 99999;
    public string SupportedTerminalArchitecture { get; set; } = "x64";
    public string ExpectedTerminalProtocolVersion { get; set; } = "1.0";
    public bool EnableWriteTests { get; set; }
}

internal static class MT5BridgeHostOptionsCompatibility
{
    private const string LegacyBridgeSection = "TradeMind:Brokers:MetaTrader5:Bridge";
    private const string LegacyTerminalSection = LegacyBridgeSection + ":Terminal";
    private const string LegacyTokenKey = LegacyBridgeSection + ":Authentication:Token";

    public static void Apply(MT5BridgeHostOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.GetSection(MT5BridgeHostOptions.SectionName).Exists()) return;

        var mode = configuration[$"{LegacyTerminalSection}:Mode"];
        if (!string.IsNullOrWhiteSpace(mode)) options.GatewayMode = mode.Equals("Real", StringComparison.OrdinalIgnoreCase) ? "Real" : "Simulation";
        options.TerminalPath = configuration[$"{LegacyTerminalSection}:TerminalPath"] ?? options.TerminalPath;
        if (Uri.TryCreate(configuration[$"{LegacyBridgeSection}:Endpoint"], UriKind.Absolute, out var endpoint)) options.TerminalBridgeEndpoint = endpoint;
        if (bool.TryParse(configuration[$"{LegacyTerminalSection}:AutoStart"], out var autoStart)) options.AutoStartTerminal = autoStart;
        if (bool.TryParse(configuration[$"{LegacyBridgeSection}:AllowLive"], out var allowLive)) options.AllowLive = allowLive;
        if (bool.TryParse(configuration[$"{LegacyTerminalSection}:AllowLive"], out allowLive)) options.AllowLive = options.AllowLive || allowLive;
        if (bool.TryParse(configuration[$"{LegacyBridgeSection}:RequireTls"], out var requireTls))
        {
            options.RequireTls = requireTls;
            options.AllowInsecureDemoTransport = !requireTls;
        }
        if (int.TryParse(configuration[$"{LegacyBridgeSection}:HandshakeTimeoutSeconds"], out var handshakeTimeout)) options.TerminalStartupTimeoutSeconds = handshakeTimeout;
        if (int.TryParse(configuration[$"{LegacyBridgeSection}:HeartbeatIntervalSeconds"], out var heartbeatInterval)) options.TerminalHeartbeatIntervalSeconds = heartbeatInterval;
        if (int.TryParse(configuration[$"{LegacyBridgeSection}:ReconnectAttempts"], out var reconnectAttempts)) options.ReconnectMaximumAttempts = reconnectAttempts;
        options.TerminalBridgeTokenConfigurationKey = LegacyTokenKey;
    }
}

public sealed class MT5BridgeHostOptionsValidator : IValidateOptions<MT5BridgeHostOptions>
{
    public ValidateOptionsResult Validate(string? name, MT5BridgeHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        if (!BridgeHostProtocolVersion.TryParse(options.ProtocolVersion, out _)) errors.Add("ProtocolVersion is invalid.");
        if (string.IsNullOrWhiteSpace(options.BridgeVersion)) errors.Add("BridgeVersion is required.");
        if (!options.DemoOnly || options.AllowLive) errors.Add("The bridge host must remain demo-only.");
        if (options.Environment.Equals("Live", StringComparison.OrdinalIgnoreCase) || options.AccountEnvironment.Equals("Live", StringComparison.OrdinalIgnoreCase)) errors.Add("Live environments are forbidden.");
        if (!options.RequireTls && !options.AllowInsecureDemoTransport) errors.Add("Plaintext transport requires explicit local demo opt-in.");
        if (!Enum.TryParse<BridgeHostAuthenticationMode>(options.AuthenticationMode, true, out var auth)) errors.Add("AuthenticationMode is invalid.");
        if (auth == BridgeHostAuthenticationMode.MutualTls && !options.RequireTls && !options.AllowInsecureDemoTransport) errors.Add("Mutual TLS requires TLS.");
        if (options.MaximumClockSkewSeconds is < 1 or > 300) errors.Add("MaximumClockSkewSeconds must be between 1 and 300.");
        if (options.MaxConcurrentRequests is < 1 or > 64) errors.Add("MaxConcurrentRequests must be between 1 and 64.");
        if (options.ReplayWindowSeconds is < 10 or > 3600) errors.Add("ReplayWindowSeconds must be between 10 and 3600.");
        if (!MT5TerminalGatewayModeParser.TryParse(options.GatewayMode, out var gatewayMode)) errors.Add("GatewayMode must be Simulation or Real.");
        if (!BridgeHostProtocolVersion.TryParse(options.ExpectedTerminalProtocolVersion, out _)) errors.Add("ExpectedTerminalProtocolVersion is invalid.");
        if (options.TerminalStartupTimeoutSeconds is < 1 or > 300) errors.Add("TerminalStartupTimeoutSeconds must be between 1 and 300.");
        if (options.TerminalShutdownTimeoutSeconds is < 1 or > 120) errors.Add("TerminalShutdownTimeoutSeconds must be between 1 and 120.");
        if (options.TerminalHeartbeatIntervalSeconds is < 1 or > 300) errors.Add("TerminalHeartbeatIntervalSeconds must be between 1 and 300.");
        if (options.TerminalHeartbeatTimeoutSeconds is < 1 or > 120) errors.Add("TerminalHeartbeatTimeoutSeconds must be between 1 and 120.");
        if (options.ReconnectMaximumAttempts is < 0 or > 10) errors.Add("ReconnectMaximumAttempts must be between 0 and 10.");
        if (options.ReconnectBackoffMilliseconds is < 0 or > 60000) errors.Add("ReconnectBackoffMilliseconds must be between 0 and 60000.");
        if (options.MinimumSupportedTerminalBuild < 1 || options.MaximumSupportedTerminalBuild < options.MinimumSupportedTerminalBuild) errors.Add("Terminal build range is invalid.");
        if (!MT5TerminalArchitectureParser.TryParse(options.SupportedTerminalArchitecture, out _)) errors.Add("SupportedTerminalArchitecture must be x64 or x86.");
        if (gatewayMode == MT5TerminalGatewayMode.Real)
        {
            if (string.IsNullOrWhiteSpace(options.TerminalPath)) errors.Add("TerminalPath is required for the real gateway.");
            if (options.TerminalBridgeEndpoint is null || !options.TerminalBridgeEndpoint.IsAbsoluteUri || options.TerminalBridgeEndpoint.Scheme is not ("http" or "https")) errors.Add("TerminalBridgeEndpoint must be an absolute HTTP or HTTPS URI for the real gateway.");
            if (options.TerminalBridgeEndpoint?.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) == true && !options.AllowInsecureDemoTransport) errors.Add("An HTTP terminal bridge endpoint requires explicit local demo opt-in.");
            if (string.IsNullOrWhiteSpace(options.TerminalBridgeTokenConfigurationKey)) errors.Add("TerminalBridgeTokenConfigurationKey is required for the real gateway.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

internal enum MT5TerminalGatewayMode
{
    Simulation,
    Real
}

internal static class MT5TerminalGatewayModeParser
{
    public static bool TryParse(string? value, out MT5TerminalGatewayMode mode)
    {
        if (Enum.TryParse(value, true, out mode) && Enum.IsDefined(mode)) return true;
        mode = MT5TerminalGatewayMode.Simulation;
        return false;
    }
}

internal enum BridgeHostAuthenticationMode
{
    MutualTls,
    SignedServiceToken
}

internal static class BridgeHostProtocolVersion
{
    public static bool TryParse(string? value, out BridgeProtocolVersion version)
    {
        version = BridgeProtocolVersion.Current;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor)) return false;
        try { version = new BridgeProtocolVersion(major, minor); return true; }
        catch (ArgumentOutOfRangeException) { return false; }
    }
}
