using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Configuration;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Security;

internal enum ReplayReservation
{
    New,
    SameRequest,
    Conflict
}

internal interface IReplayProtector
{
    ReplayReservation Reserve(string nonce, string requestHash, DateTimeOffset now, TimeSpan window);
}

internal sealed class InMemoryReplayProtector : IReplayProtector
{
    private readonly ConcurrentDictionary<string, ReplayEntry> entries = new(StringComparer.Ordinal);

    public ReplayReservation Reserve(string nonce, string requestHash, DateTimeOffset now, TimeSpan window)
    {
        foreach (var entry in entries.Where(entry => now - entry.Value.SeenAtUtc > window)) entries.TryRemove(entry.Key, out _);
        var candidate = new ReplayEntry(requestHash, now);
        if (entries.TryAdd(nonce, candidate)) return ReplayReservation.New;
        return entries[nonce].RequestHash.Equals(requestHash, StringComparison.OrdinalIgnoreCase) ? ReplayReservation.SameRequest : ReplayReservation.Conflict;
    }

    private sealed record ReplayEntry(string RequestHash, DateTimeOffset SeenAtUtc);
}

internal interface IBridgeIdempotencyStore
{
    bool TryGet(string keyHash, out BridgeIdempotencyEntry entry);
    void Store(string keyHash, BridgeIdempotencyEntry entry);
}

internal sealed class InMemoryBridgeIdempotencyStore : IBridgeIdempotencyStore
{
    private readonly ConcurrentDictionary<string, BridgeIdempotencyEntry> entries = new(StringComparer.Ordinal);
    public bool TryGet(string keyHash, out BridgeIdempotencyEntry entry) => entries.TryGetValue(keyHash, out entry!);
    public void Store(string keyHash, BridgeIdempotencyEntry entry) => entries.TryAdd(keyHash, entry);
}

internal sealed record BridgeIdempotencyEntry(string RequestHash, BridgeTransportResponse Response);

internal sealed class BridgeSecurityValidator(IOptions<MT5BridgeHostOptions> options, IReplayProtector replayProtector)
{
    private readonly MT5BridgeHostOptions configuration = options.Value;

    public BridgeError? ValidateTransport(HttpContext context, BridgeTransportRequest request, DateTimeOffset now)
    {
        if (!configuration.DemoOnly || configuration.AllowLive) return new(BridgeErrorCode.LiveModeForbidden, "Live bridge execution is disabled.", false, false);
        if (!request.ProtocolVersion.IsCompatibleWith(RequiredProtocol())) return new(BridgeErrorCode.ProtocolVersionMismatch, "The bridge protocol version is incompatible.", false, false);
        if (configuration.RequireTls && context.Request.Scheme != Uri.UriSchemeHttps && !configuration.AllowInsecureDemoTransport) return new(BridgeErrorCode.AuthenticationFailed, "TLS is required for bridge transport.", false, false);
        if (!HasAuthentication(context)) return new(BridgeErrorCode.AuthenticationFailed, "Bridge authentication is required.", false, false);
        var skew = Math.Abs((now - request.Metadata.TimestampUtc).TotalSeconds);
        if (skew > configuration.MaximumClockSkewSeconds) return new(BridgeErrorCode.ClockSkewExceeded, "The request timestamp is outside the permitted clock skew.", false, false);
        if (request.Metadata.DeadlineUtc < now) return new(BridgeErrorCode.Timeout, "The request deadline has expired.", false, false);
        return null;
    }

    public ReplayReservation ReserveNonce(BridgeRequestMetadata metadata, DateTimeOffset now) => replayProtector.Reserve(metadata.Nonce, metadata.NormalizedRequestHash, now, TimeSpan.FromSeconds(configuration.ReplayWindowSeconds));

    private bool HasAuthentication(HttpContext context) => configuration.AuthenticationMode.Equals("MutualTls", StringComparison.OrdinalIgnoreCase)
        ? context.Request.Headers.TryGetValue("X-Bridge-Client-Certificate", out var certificate) && certificate.Count > 0
        : context.Request.Headers.Authorization.Count > 0 && context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.Ordinal);

    private BridgeProtocolVersion RequiredProtocol() => BridgeHostProtocolVersion.TryParse(configuration.ProtocolVersion, out var version) ? version : BridgeProtocolVersion.Current;
}
