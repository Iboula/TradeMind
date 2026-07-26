using TradeMind.Market.Abstractions;

namespace TradeMind.MarketConnectors.Application;

public sealed record MarketSnapshotPersistenceRequest(
    MarketSnapshot Snapshot,
    string ContentHash,
    string CanonicalPayload,
    int SchemaVersion,
    int PayloadSizeBytes,
    DateTimeOffset CreatedAt);

public sealed record StoredMarketSnapshotIdentity(
    SnapshotId SnapshotId,
    ConnectorId ConnectorId,
    string ContentHash,
    DateTimeOffset ReceivedAt);

public enum MarketSnapshotAtomicInsertStatus
{
    Inserted,
    Duplicate,
    Conflict
}

public sealed record MarketSnapshotAtomicInsertResult(
    MarketSnapshotAtomicInsertStatus Status,
    StoredMarketSnapshotIdentity StoredSnapshot);

public sealed record MarketSnapshotLookup(
    ConnectorId ConnectorId,
    ExternalAccountReference? Account,
    Instrument Instrument,
    Timeframe Timeframe,
    DateTimeOffset? MinimumCapturedAt);

public sealed record StoredMarketSnapshotPayload(
    string CanonicalPayload,
    int SchemaVersion);

public interface IMarketSnapshotRepository
{
    Task<StoredMarketSnapshotIdentity?> FindByKeyAsync(
        ConnectorId connectorId,
        SnapshotId snapshotId,
        CancellationToken cancellationToken);

    Task<MarketSnapshotAtomicInsertResult> InsertAsync(
        MarketSnapshotPersistenceRequest request,
        CancellationToken cancellationToken);

    Task<StoredMarketSnapshotPayload?> GetLatestAsync(
        MarketSnapshotLookup lookup,
        CancellationToken cancellationToken);

    Task<DateTimeOffset?> GetLatestReceivedAtAsync(
        ConnectorId connectorId,
        CancellationToken cancellationToken);
}
