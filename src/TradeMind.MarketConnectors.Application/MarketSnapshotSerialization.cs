using TradeMind.Market.Abstractions;

namespace TradeMind.MarketConnectors.Application;

public sealed record SerializedMarketSnapshot(
    string CanonicalPayload,
    string CanonicalContent,
    int SchemaVersion,
    int SizeInBytes);

public interface IMarketSnapshotSerializer
{
    int CurrentSchemaVersion { get; }

    SerializedMarketSnapshot Serialize(MarketSnapshot snapshot);

    MarketSnapshot Deserialize(string canonicalPayload, int schemaVersion);
}

public interface IMarketSnapshotHasher
{
    string ComputeHash(string canonicalPayload);
}

public sealed class UnsupportedMarketSnapshotSchemaVersionException(int schemaVersion)
    : NotSupportedException($"Market snapshot schema version '{schemaVersion}' is not supported.")
{
    public int SchemaVersion { get; } = schemaVersion;
}
