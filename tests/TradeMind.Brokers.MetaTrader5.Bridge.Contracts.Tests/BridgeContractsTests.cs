using System.Reflection;
using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Tests;

public sealed class BridgeContractsTests
{
    [Fact]
    public void Current_protocol_is_immutable_and_compatible_with_same_major_minor()
    {
        var current = BridgeProtocolVersion.Current;
        Assert.Equal("1.0", current.ToString());
        Assert.True(current.IsCompatibleWith(new BridgeProtocolVersion(1, 0)));
        Assert.False(current.IsCompatibleWith(new BridgeProtocolVersion(2, 0)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Protocol_version_rejects_negative_parts(int major, int minor) => Assert.Throws<ArgumentOutOfRangeException>(() => new BridgeProtocolVersion(major, minor));

    [Fact]
    public void Request_metadata_requires_all_identity_fields()
    {
        Assert.Throws<ArgumentException>(() => Metadata(requestId: ""));
        Assert.Throws<ArgumentException>(() => Metadata(nonce: "short"));
        Assert.Throws<ArgumentException>(() => Metadata(idempotencyHash: "not-a-hash"));
        Assert.Throws<ArgumentException>(() => Metadata(requestHash: "not-a-hash"));
    }

    [Fact]
    public void Request_metadata_requires_utc_timestamps_and_forward_deadline()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => new BridgeRequestMetadata("req", "corr", "session", now.ToOffset(TimeSpan.FromHours(1)), Nonce(), Hash("key"), Hash("request"), now.AddMinutes(1)));
        Assert.Throws<ArgumentException>(() => new BridgeRequestMetadata("req", "corr", "session", now, Nonce(), Hash("key"), Hash("request"), now.AddSeconds(-1)));
    }

    [Fact]
    public void Request_metadata_normalizes_hashes_but_defensively_copies_collections()
    {
        var required = new List<BridgeCapability> { new("orders", "1") };
        var request = new BridgeHandshakeRequest(BridgeProtocolVersion.Current, "1.0.0", true, "Demo", "Demo", DateTimeOffset.UtcNow, required, Metadata());
        required.Clear();
        Assert.Single(request.RequiredCapabilities);
        Assert.Equal(Hash("KEY"), Metadata(idempotencyHash: Hash("KEY")).IdempotencyKeyHash);
    }

    [Fact]
    public void Payload_limit_is_enforced()
    {
        var oversized = new string('x', BridgePayloadLimits.MaxPayloadBytes + 1);
        Assert.Throws<ArgumentException>(() => new BridgeTransportRequest(BridgeProtocolVersion.Current, BridgeOperation.Health, Metadata(), oversized));
    }

    [Fact]
    public void Collection_limit_is_enforced()
    {
        var capabilities = Enumerable.Range(0, BridgePayloadLimits.MaxCollectionItems + 1).Select(index => new BridgeCapability($"cap-{index}", "1"));
        Assert.Throws<ArgumentException>(() => new BridgeHandshakeRequest(BridgeProtocolVersion.Current, "1.0.0", true, "Demo", "Demo", DateTimeOffset.UtcNow, capabilities.ToArray(), Metadata()));
    }

    [Fact]
    public void Normalized_error_is_immutable_and_bounded()
    {
        var error = new BridgeError(BridgeErrorCode.TerminalRejected, "rejected", false, false);
        Assert.Equal(BridgeErrorCode.TerminalRejected, error.Code);
        Assert.Throws<ArgumentException>(() => new BridgeError(BridgeErrorCode.Unknown, new string('x', BridgePayloadLimits.MaxMessageLength + 1), false, false));
    }

    [Fact]
    public void Order_data_rejects_invalid_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BridgeOrderData("account", "EURUSD", "Buy", "Market", 0, null, "Day"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BridgeOrderData("account", "EURUSD", "Buy", "Market", 1, 0, "Day"));
    }

    [Fact]
    public void No_web_ef_or_mt5_sdk_dependency_is_present_in_contract_assembly()
    {
        var references = typeof(BridgeProtocolVersion).Assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(references, name => name.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase) || name.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase) || name.Contains("MetaTrader", StringComparison.OrdinalIgnoreCase) || name.Contains("MT5", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Required_contracts_are_public_and_neutral()
    {
        var names = new[] { "BridgeHandshakeRequest", "BridgeHandshakeResponse", "BridgeHealthRequest", "BridgeHealthResponse", "BridgeAccountRequest", "BridgeAccountResponse", "BridgeInstrumentRequest", "BridgeInstrumentResponse", "BridgeSubmitOrderRequest", "BridgeSubmitOrderResponse", "BridgeModifyOrderRequest", "BridgeModifyOrderResponse", "BridgeCancelOrderRequest", "BridgeCancelOrderResponse", "BridgeClosePositionRequest", "BridgeClosePositionResponse", "BridgeOrdersRequest", "BridgeOrdersResponse", "BridgePositionsRequest", "BridgePositionsResponse", "BridgeError", "BridgeCapability", "BridgeProtocolVersion" };
        foreach (var name in names) Assert.True(typeof(BridgeProtocolVersion).Assembly.GetType($"TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol.{name}")?.IsPublic, name);
    }

    private static BridgeRequestMetadata Metadata(string requestId = "req-1", string nonce = "0123456789abcdef", string? idempotencyHash = null, string? requestHash = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new(requestId, "corr-1", "session-1", now, nonce, idempotencyHash ?? Hash("key"), requestHash ?? Hash("request"), now.AddMinutes(1));
    }

    private static string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string Nonce() => "0123456789abcdef0123456789abcdef";
}
