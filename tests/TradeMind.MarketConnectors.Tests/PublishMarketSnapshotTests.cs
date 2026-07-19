using TradeMind.MarketConnectors.Application;

namespace TradeMind.MarketConnectors.Tests;

public sealed class PublishMarketSnapshotTests
{
    [Fact]
    public async Task Ingestion_AcceptsNewSnapshot()
    {
        var repository = new FakeMarketSnapshotRepository();
        var snapshot = MarketConnectorTestData.Snapshot();

        var result = await MarketConnectorTestData.IngestionHandler(repository)
            .Handle(new PublishMarketSnapshotCommand(snapshot), CancellationToken.None);

        Assert.Equal(PublishMarketSnapshotStatus.Accepted, result.Status);
        Assert.Equal(snapshot.Id, result.SnapshotId);
        Assert.Equal(snapshot.ConnectorId, result.ConnectorId);
        Assert.Equal(snapshot.ReceivedAt, result.PersistedReceivedAt);
        Assert.Null(result.Reason);
        Assert.NotNull(repository.LastInsertionRequest);
        Assert.Equal(MarketConnectorTestData.Now, repository.LastInsertionRequest.CreatedAt);
    }

    [Fact]
    public async Task Ingestion_ReturnsDuplicateForExistingEqualContent()
    {
        var snapshot = MarketConnectorTestData.Snapshot();
        var serialized = MarketConnectorTestData.Serialize(snapshot);
        var hash = new TradeMind.MarketConnectors.Infrastructure.Sha256MarketSnapshotHasher()
            .ComputeHash(serialized.CanonicalContent);
        var repository = new FakeMarketSnapshotRepository
        {
            Existing = new StoredMarketSnapshotIdentity(
                snapshot.Id,
                snapshot.ConnectorId,
                hash,
                snapshot.ReceivedAt)
        };

        var result = await MarketConnectorTestData.IngestionHandler(repository)
            .Handle(new PublishMarketSnapshotCommand(snapshot), CancellationToken.None);

        Assert.Equal(PublishMarketSnapshotStatus.Duplicate, result.Status);
        Assert.Equal("MARKET_SNAPSHOT_DUPLICATE", result.Reason?.Code);
        Assert.Null(repository.LastInsertionRequest);
    }

    [Fact]
    public async Task Ingestion_ReturnsConflictForExistingDifferentContent()
    {
        var snapshot = MarketConnectorTestData.Snapshot();
        var repository = new FakeMarketSnapshotRepository
        {
            Existing = new StoredMarketSnapshotIdentity(
                snapshot.Id,
                snapshot.ConnectorId,
                new string('0', 64),
                snapshot.ReceivedAt)
        };

        var result = await MarketConnectorTestData.IngestionHandler(repository)
            .Handle(new PublishMarketSnapshotCommand(snapshot), CancellationToken.None);

        Assert.Equal(PublishMarketSnapshotStatus.Conflict, result.Status);
        Assert.Equal("MARKET_SNAPSHOT_CONTENT_CONFLICT", result.Reason?.Code);
    }

    [Fact]
    public async Task Ingestion_MapsAtomicRaceToDuplicate()
    {
        var snapshot = MarketConnectorTestData.Snapshot();
        var repository = new FakeMarketSnapshotRepository
        {
            Insertion = new MarketSnapshotAtomicInsertResult(
                MarketSnapshotAtomicInsertStatus.Duplicate,
                new StoredMarketSnapshotIdentity(
                    snapshot.Id,
                    snapshot.ConnectorId,
                    new string('a', 64),
                    snapshot.ReceivedAt))
        };

        var result = await MarketConnectorTestData.IngestionHandler(repository)
            .Handle(new PublishMarketSnapshotCommand(snapshot), CancellationToken.None);

        Assert.Equal(PublishMarketSnapshotStatus.Duplicate, result.Status);
    }

    [Fact]
    public async Task Ingestion_MapsAtomicRaceToConflict()
    {
        var snapshot = MarketConnectorTestData.Snapshot();
        var repository = new FakeMarketSnapshotRepository
        {
            Insertion = new MarketSnapshotAtomicInsertResult(
                MarketSnapshotAtomicInsertStatus.Conflict,
                new StoredMarketSnapshotIdentity(
                    snapshot.Id,
                    snapshot.ConnectorId,
                    new string('b', 64),
                    snapshot.ReceivedAt))
        };

        var result = await MarketConnectorTestData.IngestionHandler(repository)
            .Handle(new PublishMarketSnapshotCommand(snapshot), CancellationToken.None);

        Assert.Equal(PublishMarketSnapshotStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task Ingestion_RejectsNullSnapshot()
    {
        var result = await MarketConnectorTestData.IngestionHandler(new FakeMarketSnapshotRepository())
            .Handle(new PublishMarketSnapshotCommand(null), CancellationToken.None);

        Assert.Equal(PublishMarketSnapshotStatus.Rejected, result.Status);
        Assert.Equal("MARKET_SNAPSHOT_REQUIRED", result.Reason?.Code);
    }

    [Fact]
    public async Task Ingestion_RejectsPayloadOverConfiguredLimit()
    {
        var options = new MarketConnectorCoreOptions { MaximumPayloadBytes = 1 };
        var repository = new FakeMarketSnapshotRepository();

        var result = await MarketConnectorTestData.IngestionHandler(repository, options)
            .Handle(new PublishMarketSnapshotCommand(MarketConnectorTestData.Snapshot()), CancellationToken.None);

        Assert.Equal(PublishMarketSnapshotStatus.Rejected, result.Status);
        Assert.Equal("MARKET_SNAPSHOT_PAYLOAD_TOO_LARGE", result.Reason?.Code);
        Assert.Null(repository.LastInsertionRequest);
    }

    [Fact]
    public async Task Ingestion_RejectsTooManyMetadataEntries()
    {
        var options = new MarketConnectorCoreOptions { MaximumMetadataEntries = 1 };

        var result = await MarketConnectorTestData.IngestionHandler(new FakeMarketSnapshotRepository(), options)
            .Handle(new PublishMarketSnapshotCommand(MarketConnectorTestData.Snapshot()), CancellationToken.None);

        Assert.Equal(PublishMarketSnapshotStatus.Rejected, result.Status);
        Assert.Equal("MARKET_SNAPSHOT_METADATA_LIMIT", result.Reason?.Code);
    }

    [Fact]
    public async Task Ingestion_RejectsUnsupportedSerializerVersion()
    {
        var result = await MarketConnectorTestData.IngestionHandler(
                new FakeMarketSnapshotRepository(),
                serializer: new UnsupportedVersionSerializer())
            .Handle(new PublishMarketSnapshotCommand(MarketConnectorTestData.Snapshot()), CancellationToken.None);

        Assert.Equal(PublishMarketSnapshotStatus.Rejected, result.Status);
        Assert.Equal("MARKET_SNAPSHOT_SCHEMA_UNSUPPORTED", result.Reason?.Code);
    }

    [Fact]
    public async Task Ingestion_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            MarketConnectorTestData.IngestionHandler(new FakeMarketSnapshotRepository())
                .Handle(new PublishMarketSnapshotCommand(MarketConnectorTestData.Snapshot()), cancellation.Token));
    }

    private sealed class UnsupportedVersionSerializer : IMarketSnapshotSerializer
    {
        public int CurrentSchemaVersion => 2;

        public SerializedMarketSnapshot Serialize(TradeMind.Market.Abstractions.MarketSnapshot snapshot) =>
            new("{}", "{}", 2, 2);

        public TradeMind.Market.Abstractions.MarketSnapshot Deserialize(string canonicalPayload, int schemaVersion) =>
            throw new NotSupportedException();
    }
}
