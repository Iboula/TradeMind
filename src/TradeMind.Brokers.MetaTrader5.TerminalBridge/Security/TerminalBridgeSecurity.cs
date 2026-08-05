using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Configuration;

namespace TradeMind.Brokers.MetaTrader5.TerminalBridge.Security;

public sealed record SecurityValidationResult(bool Accepted, int StatusCode, string Code, string Message)
{
    public static SecurityValidationResult Success() => new(true, StatusCodes.Status200OK, string.Empty, string.Empty);
    public static SecurityValidationResult Failure(int statusCode, string code, string message) => new(false, statusCode, code, message);
}

public sealed class TerminalBridgeSecurity(
    IOptions<TerminalBridgeOptions> options,
    TerminalBridgeSecretProvider secrets,
    ReplayCache replayCache,
    TimeProvider timeProvider)
{
    private readonly TerminalBridgeOptions configuration = options.Value;

    public SecurityValidationResult ValidateBridge(HttpContext context, string body)
    {
        if (!configuration.AllowInsecureDemoTransport && !context.Request.IsHttps) return SecurityValidationResult.Failure(StatusCodes.Status400BadRequest, "PROTOCOL_MISMATCH", "HTTPS is required.");
        if (context.Request.ContentLength is > 0 && context.Request.ContentLength > configuration.MaxPayloadBytes) return SecurityValidationResult.Failure(StatusCodes.Status413PayloadTooLarge, "UNKNOWN", "The request payload is too large.");
        if (!TryGetBearerToken(context, out var suppliedToken)) return SecurityValidationResult.Failure(StatusCodes.Status401Unauthorized, "TERMINAL_TRANSPORT_FAILURE", "Bridge authentication failed.");
        var configuredToken = secrets.Get(configuration.TokenConfigurationKey);
        if (string.IsNullOrWhiteSpace(configuredToken)) return SecurityValidationResult.Failure(StatusCodes.Status503ServiceUnavailable, "TERMINAL_TRANSPORT_FAILURE", "Bridge authentication is not configured.");
        if (!FixedTimeEquals(configuredToken, suppliedToken)) return SecurityValidationResult.Failure(StatusCodes.Status401Unauthorized, "TERMINAL_TRANSPORT_FAILURE", "Bridge authentication failed.");
        if (!context.Request.Headers.TryGetValue(BridgeRequestSigning.SignatureVersionHeader, out var version) || version != BridgeRequestSigning.SignatureVersion) return SecurityValidationResult.Failure(StatusCodes.Status400BadRequest, "PROTOCOL_MISMATCH", "The bridge signature version is not supported.");
        if (!context.Request.Headers.TryGetValue(BridgeRequestSigning.TimestampHeader, out var timestampValue) || !long.TryParse(timestampValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestampSeconds)) return SecurityValidationResult.Failure(StatusCodes.Status400BadRequest, "PROTOCOL_MISMATCH", "The bridge timestamp is invalid.");
        if (!context.Request.Headers.TryGetValue(BridgeRequestSigning.NonceHeader, out var nonce) || nonce.Count != 1 || string.IsNullOrWhiteSpace(nonce[0]) || nonce[0]!.Length > BridgePayloadLimits.MaxNonceLength) return SecurityValidationResult.Failure(StatusCodes.Status400BadRequest, "PROTOCOL_MISMATCH", "The bridge nonce is invalid.");
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        if ((timeProvider.GetUtcNow() - timestamp).Duration() > TimeSpan.FromSeconds(configuration.MaximumClockSkewSeconds)) return SecurityValidationResult.Failure(StatusCodes.Status400BadRequest, "PROTOCOL_MISMATCH", "The bridge timestamp is outside the allowed clock skew.");
        var suppliedSignature = context.Request.Headers[BridgeRequestSigning.SignatureHeader].ToString();
        if (!BridgeRequestSigning.VerifySignature(configuredToken, context.Request.Method, context.Request.Path.Value ?? "/", timestampValue.ToString(), nonce[0]!, body, suppliedSignature)) return SecurityValidationResult.Failure(StatusCodes.Status401Unauthorized, "TERMINAL_TRANSPORT_FAILURE", "Bridge authentication failed.");
        if (!replayCache.TryAccept(nonce[0]!, timestamp.AddSeconds(configuration.ReplayWindowSeconds), configuration.MaxReplayEntries)) return SecurityValidationResult.Failure(StatusCodes.Status409Conflict, "REPLAY_DETECTED", "The bridge request was already received.");
        return SecurityValidationResult.Success();
    }

    public SecurityValidationResult ValidateAgent(HttpContext context)
    {
        if (!configuration.AllowInsecureDemoTransport && !context.Request.IsHttps) return SecurityValidationResult.Failure(StatusCodes.Status400BadRequest, "PROTOCOL_MISMATCH", "HTTPS is required.");
        var expected = secrets.Get(configuration.AgentTokenConfigurationKey);
        var supplied = context.Request.Headers["X-TradeMind-Agent-Token"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || !FixedTimeEquals(expected, supplied)) return SecurityValidationResult.Failure(StatusCodes.Status401Unauthorized, "TERMINAL_TRANSPORT_FAILURE", "Agent authentication failed.");
        return SecurityValidationResult.Success();
    }

    private static bool TryGetBearerToken(HttpContext context, out string token)
    {
        token = string.Empty;
        var value = context.Request.Headers.Authorization.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        token = value[7..].Trim();
        return token.Length > 0;
    }

    private static bool FixedTimeEquals(string expected, string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
