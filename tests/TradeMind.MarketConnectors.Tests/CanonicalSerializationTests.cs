using System.Text.Json;
using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Application;
using TradeMind.MarketConnectors.Infrastructure;

namespace TradeMind.MarketConnectors.Tests;

public sealed class CanonicalSerializationTests
{
    private readonly CanonicalMarketSnapshotSerializer _serializer = new();
    private readonly Sha256MarketSnapshotHasher _hasher = new();

    [Fact]
    public void Serialization_IsDeterministicAcrossDictionaryOrders()
    {
        var first = MarketConnectorTestData.Snapshot(
            metadata: new Dictionary<string, string> { ["zeta"] = "last", ["alpha"] = "first" },
            indicatorParameters: new Dictionary<string, string> { ["shift"] = "0", ["period"] = "20" });
        var second = MarketConnectorTestData.Snapshot(
            metadata: new Dictionary<string, string> { ["alpha"] = "first", ["zeta"] = "last" },
            indicatorParameters: new Dictionary<string, string> { ["period"] = "20", ["shift"] = "0" });

        Assert.Equal(
            _serializer.Serialize(first).CanonicalPayload,
            _serializer.Serialize(second).CanonicalPayload);
    }

    [Fact]
    public void Hash_IsEqualForLogicallyEquivalentSnapshots()
    {
        var first = MarketConnectorTestData.Snapshot(
            metadata: new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" });
        var second = MarketConnectorTestData.Snapshot(
            metadata: new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });

        Assert.Equal(Hash(first), Hash(second));
    }

    [Fact]
    public void Hash_ChangesWhenCanonicalContentChanges()
    {
        Assert.NotEqual(
            Hash(MarketConnectorTestData.Snapshot(close: 1.11m)),
            Hash(MarketConnectorTestData.Snapshot(close: 1.115m)));
    }

    [Fact]
    public void Hash_IsSha256LowercaseHex()
    {
        var hash = Hash(MarketConnectorTestData.Snapshot());

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
    }

    [Fact]
    public void Serialization_PersistsSchemaVersionAndByteSize()
    {
        var serialized = _serializer.Serialize(MarketConnectorTestData.Snapshot());
        using var document = JsonDocument.Parse(serialized.CanonicalPayload);

        Assert.Equal(CanonicalMarketSnapshotSerializer.SchemaVersion, serialized.SchemaVersion);
        Assert.Equal(serialized.SchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(serialized.CanonicalPayload), serialized.SizeInBytes);
    }

    [Fact]
    public void Deserialization_RestoresCompleteCanonicalSnapshot()
    {
        var source = MarketConnectorTestData.Snapshot();
        var serialized = _serializer.Serialize(source);

        var restored = _serializer.Deserialize(serialized.CanonicalPayload, serialized.SchemaVersion);

        Assert.Equal(source.Id, restored.Id);
        Assert.Equal(source.ConnectorId, restored.ConnectorId);
        Assert.Equal(source.Account, restored.Account);
        Assert.Equal(source.Instrument, restored.Instrument);
        Assert.Equal(source.Timeframe, restored.Timeframe);
        Assert.Equal(source.CapturedAt, restored.CapturedAt);
        Assert.Equal(source.ReceivedAt, restored.ReceivedAt);
        Assert.Single(restored.Candles);
        Assert.NotNull(restored.Quote);
        Assert.Single(restored.Positions);
        Assert.Single(restored.PendingOrders);
        Assert.Single(restored.Indicators);
        Assert.Equal(6, restored.Drawings.Count);
        Assert.Equal(source.Metadata, restored.Metadata);
    }

    [Fact]
    public void Deserialization_RejectsUnsupportedSchemaVersion()
    {
        var serialized = _serializer.Serialize(MarketConnectorTestData.Snapshot());

        var exception = Assert.Throws<UnsupportedMarketSnapshotSchemaVersionException>(() =>
            _serializer.Deserialize(serialized.CanonicalPayload, 99));

        Assert.Equal(99, exception.SchemaVersion);
    }

    [Fact]
    public void Deserialization_RejectsMismatchedPersistedVersion()
    {
        var serialized = _serializer.Serialize(MarketConnectorTestData.Snapshot());
        var modified = serialized.CanonicalPayload.Replace(
            "\"schemaVersion\":1",
            "\"schemaVersion\":2",
            StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => _serializer.Deserialize(modified, 1));
    }

    [Fact]
    public void CanonicalDates_AreNormalizedToUtc()
    {
        var offsetTime = new DateTimeOffset(2026, 7, 19, 17, 0, 0, TimeSpan.FromHours(-4));
        var serialized = _serializer.Serialize(MarketConnectorTestData.Snapshot(
            capturedAt: offsetTime,
            receivedAt: offsetTime.AddSeconds(1)));
        using var document = JsonDocument.Parse(serialized.CanonicalPayload);

        Assert.Equal(
            "2026-07-19T21:00:00.0000000+00:00",
            document.RootElement.GetProperty("snapshot").GetProperty("capturedAt").GetString());
    }

    private string Hash(MarketSnapshot snapshot) =>
        _hasher.ComputeHash(_serializer.Serialize(snapshot).CanonicalContent);
}
