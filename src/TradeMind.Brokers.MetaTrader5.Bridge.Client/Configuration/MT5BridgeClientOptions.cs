using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Client.Configuration;

public enum BridgeAuthenticationMode
{
    MutualTls,
    SignedServiceToken
}

public sealed class MT5BridgeClientOptions
{
    public const string SectionName = "TradeMind:Brokers:MetaTrader5:Bridge";

    public string Endpoint { get; set; } = "https://localhost:7443";
    public string ProtocolVersion { get; set; } = BridgeProtocolVersion.Current.ToString();
    public int RequestTimeoutSeconds { get; set; } = 10;
    public int HandshakeTimeoutSeconds { get; set; } = 10;
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int MaximumClockSkewSeconds { get; set; } = 30;
    public int MaxConcurrentRequests { get; set; } = 4;
    public int MaxTransportRetries { get; set; } = 2;
    public int CircuitBreakerFailureThreshold { get; set; } = 3;
    public int CircuitBreakerOpenSeconds { get; set; } = 10;
    public bool Enabled { get; set; } = true;
    public bool AllowLive { get; set; }
    public bool RequireTls { get; set; } = true;
    public bool AllowInsecureDemoTransport { get; set; }
    public BridgeAuthenticationMode AuthenticationMode { get; set; } = BridgeAuthenticationMode.MutualTls;
    public string? ServiceToken { get; set; }
    public string AdapterVersion { get; set; } = "1.0.0";
}

public sealed class MT5BridgeClientOptionsValidator : IValidateOptions<MT5BridgeClientOptions>
{
    public ValidateOptionsResult Validate(string? name, MT5BridgeClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) || endpoint is null || endpoint is { Scheme: not "http" and not "https" })
            errors.Add("Bridge Endpoint must be an absolute HTTP(S) URI.");
        else if (options.RequireTls && endpoint.Scheme != Uri.UriSchemeHttps && !options.AllowInsecureDemoTransport)
            errors.Add("Plaintext bridge transport is forbidden when TLS is required.");
        if (!BridgeProtocolVersionExtensions.TryParse(options.ProtocolVersion, out _)) errors.Add("Bridge ProtocolVersion is invalid.");
        if (options.RequestTimeoutSeconds is < 1 or > 300) errors.Add("RequestTimeoutSeconds must be between 1 and 300.");
        if (options.HandshakeTimeoutSeconds is < 1 or > 300) errors.Add("HandshakeTimeoutSeconds must be between 1 and 300.");
        if (options.HeartbeatIntervalSeconds is < 1 or > 3600) errors.Add("HeartbeatIntervalSeconds must be between 1 and 3600.");
        if (options.MaximumClockSkewSeconds is < 1 or > 300) errors.Add("MaximumClockSkewSeconds must be between 1 and 300.");
        if (options.MaxConcurrentRequests is < 1 or > 64) errors.Add("MaxConcurrentRequests must be between 1 and 64.");
        if (options.MaxTransportRetries is < 0 or > 5) errors.Add("MaxTransportRetries must be between 0 and 5.");
        if (options.CircuitBreakerFailureThreshold is < 1 or > 20) errors.Add("CircuitBreakerFailureThreshold must be between 1 and 20.");
        if (options.CircuitBreakerOpenSeconds is < 1 or > 300) errors.Add("CircuitBreakerOpenSeconds must be between 1 and 300.");
        if (options.AllowLive) errors.Add("Live bridge execution is forbidden in Sprint 30.");
        if (options.AuthenticationMode == BridgeAuthenticationMode.SignedServiceToken && string.IsNullOrWhiteSpace(options.ServiceToken))
            errors.Add("SignedServiceToken authentication requires a runtime-provided service token.");
        if (options.AuthenticationMode == BridgeAuthenticationMode.MutualTls && !options.RequireTls)
            errors.Add("Mutual TLS authentication requires TLS.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
