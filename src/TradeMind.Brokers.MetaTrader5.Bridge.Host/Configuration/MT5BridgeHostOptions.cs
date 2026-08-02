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
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
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
