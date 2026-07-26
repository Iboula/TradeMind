using System.Data;
using Microsoft.EntityFrameworkCore;
using TradeMind.KnowledgeHub.Domain;
using TradeMind.KnowledgeHub.Infrastructure;
using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Application;
using TradeMind.MarketConnectors.Infrastructure;

namespace TradeMind.MarketConnectors.IntegrationTests;

public sealed class PostgreSqlMarketConnectorTests(PostgreSqlMarketConnectorFixture fixture)
    : IClassFixture<PostgreSqlMarketConnectorFixture>
{
    [Fact]
    public async Task Migration_AppliesToEmptyDatabase()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await using var context = fixture.CreateMarketDbContext(connectionString);

        await context.Database.MigrateAsync();

        Assert.True(await ExistsAsync(context, "SELECT to_regclass('market_snapshot_records') IS NOT NULL;"));
        Assert.True(await ExistsAsync(context, "SELECT to_regclass('market_snapshot_payloads') IS NOT NULL;"));
        Assert.False(await ExistsAsync(context, "SELECT to_regclass('knowledge_sources') IS NOT NULL;"));
    }

    [Fact]
    public async Task Migration_CreatesTablesConstraintsAndIndexes()
    {
        await using var context = fixture.CreateMarketDbContext();

        Assert.True(await ExistsAsync(context, "SELECT to_regclass('market_snapshot_records') IS NOT NULL;"));
        Assert.True(await ExistsAsync(context, "SELECT to_regclass('market_snapshot_payloads') IS NOT NULL;"));
        Assert.True(await ExistsAsync(context,
            "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'uq_market_snapshot_records_connector_snapshot');"));
        Assert.True(await ExistsAsync(context,
            "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'ix_market_snapshot_records_latest');"));
        Assert.True(await ExistsAsync(context,
            "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'ix_market_snapshot_records_health');"));
    }

    [Fact]
    public async Task Repository_InsertsAndReadsSnapshot()
    {
        var snapshot = MarketConnectorIntegrationData.Snapshot(UniqueConnector());
        var accepted = await PublishAsync(snapshot);

        await using var context = fixture.CreateMarketDbContext();
        var repository = new PostgreSqlMarketSnapshotRepository(context);
        var stored = await repository.FindByKeyAsync(snapshot.ConnectorId, snapshot.Id, CancellationToken.None);

        Assert.Equal(PublishMarketSnapshotStatus.Accepted, accepted.Status);
        Assert.NotNull(stored);
        Assert.Equal(snapshot.Id, stored.SnapshotId);
        Assert.Equal(snapshot.ConnectorId, stored.ConnectorId);
        Assert.Equal(snapshot.ReceivedAt, stored.ReceivedAt);
    }

    [Fact]
    public async Task Repository_PreservesCanonicalValueObjects()
    {
        var snapshot = MarketConnectorIntegrationData.Snapshot(
            UniqueConnector(),
            account: "Opaque:Account/A-42",
            instrument: "eurusd.raw-pro",
            timeframe: Timeframe.H4);
        await PublishAsync(snapshot);

        var restored = await QueryLatestAsync(
            snapshot.ConnectorId,
            snapshot.Account,
            snapshot.Instrument,
            snapshot.Timeframe);

        Assert.NotNull(restored);
        Assert.Equal(snapshot.Id, restored.Id);
        Assert.Equal("Opaque:Account/A-42", restored.Account.Value);
        Assert.Equal("EURUSD.RAW-PRO", restored.Instrument.Symbol);
        Assert.Equal(Timeframe.H4, restored.Timeframe);
    }

    [Fact]
    public async Task Persistence_StoresCanonicalDocumentAsJsonb()
    {
        var snapshot = MarketConnectorIntegrationData.Snapshot(UniqueConnector());
        await PublishAsync(snapshot);
        await using var context = fixture.CreateMarketDbContext();

        var dataTypeIsJsonb = await ScalarAsync<bool>(context,
            "SELECT data_type = 'jsonb' FROM information_schema.columns "
            + "WHERE table_name = 'market_snapshot_payloads' AND column_name = 'document';");
        var documentIsObject = await ScalarAsync<bool>(context,
            "SELECT jsonb_typeof(p.document) = 'object' FROM market_snapshot_payloads p "
            + "INNER JOIN market_snapshot_records r ON r.id = p.market_snapshot_record_id "
            + $"WHERE r.snapshot_id = '{snapshot.Id.Value:D}';");

        Assert.True(dataTypeIsJsonb);
        Assert.True(documentIsObject);
    }

    [Fact]
    public async Task Database_EnforcesUniqueConnectorAndSnapshotKey()
    {
        var snapshot = MarketConnectorIntegrationData.Snapshot(UniqueConnector());
        await PublishAsync(snapshot);

        await using var context = fixture.CreateMarketDbContext();
        var isUnique = await ScalarAsync<bool>(context,
            "SELECT i.indisunique FROM pg_class c "
            + "INNER JOIN pg_index i ON i.indexrelid = c.oid "
            + "WHERE c.relname = 'uq_market_snapshot_records_connector_snapshot';");

        Assert.True(isUnique);
        Assert.Single(await context.MarketSnapshotRecords
            .Where(record => record.ConnectorId == snapshot.ConnectorId.Value
                && record.SnapshotId == snapshot.Id.Value)
            .ToListAsync());
    }

    [Fact]
    public async Task Ingestion_ReturnsDuplicateAgainstRealDatabase()
    {
        var snapshot = MarketConnectorIntegrationData.Snapshot(UniqueConnector());

        var first = await PublishAsync(snapshot);
        var second = await PublishAsync(snapshot);

        Assert.Equal(PublishMarketSnapshotStatus.Accepted, first.Status);
        Assert.Equal(PublishMarketSnapshotStatus.Duplicate, second.Status);
        Assert.Equal("MARKET_SNAPSHOT_DUPLICATE", second.Reason?.Code);
    }

    [Fact]
    public async Task Ingestion_ReturnsConflictAgainstRealDatabase()
    {
        var id = Guid.NewGuid();
        var connector = UniqueConnector();
        var first = MarketConnectorIntegrationData.Snapshot(connector, id, close: 1.11m);
        var changed = MarketConnectorIntegrationData.Snapshot(connector, id, close: 1.115m);

        var accepted = await PublishAsync(first);
        var conflict = await PublishAsync(changed);

        Assert.Equal(PublishMarketSnapshotStatus.Accepted, accepted.Status);
        Assert.Equal(PublishMarketSnapshotStatus.Conflict, conflict.Status);
        Assert.Equal("MARKET_SNAPSHOT_CONTENT_CONFLICT", conflict.Reason?.Code);
    }

    [Fact]
    public async Task LatestQuery_ReturnsNewestCapturedSnapshotAndBreaksTiesDeterministically()
    {
        var connector = UniqueConnector();
        var capturedAt = MarketConnectorIntegrationData.Now.AddMinutes(-1);
        var earlierReceived = MarketConnectorIntegrationData.Snapshot(
            connector,
            capturedAt: capturedAt,
            receivedAt: capturedAt.AddSeconds(1));
        var laterReceived = MarketConnectorIntegrationData.Snapshot(
            connector,
            capturedAt: capturedAt,
            receivedAt: capturedAt.AddSeconds(2));
        await PublishAsync(earlierReceived);
        await PublishAsync(laterReceived);

        var latest = await QueryLatestAsync(
            earlierReceived.ConnectorId,
            earlierReceived.Account,
            earlierReceived.Instrument,
            earlierReceived.Timeframe);

        Assert.NotNull(latest);
        Assert.Equal(laterReceived.Id, latest.Id);
    }

    [Fact]
    public async Task LatestQuery_FiltersByAccount()
    {
        var connector = UniqueConnector();
        var accountA = MarketConnectorIntegrationData.Snapshot(connector, account: "Account-A");
        var accountB = MarketConnectorIntegrationData.Snapshot(
            connector,
            account: "Account-B",
            capturedAt: MarketConnectorIntegrationData.Now.AddSeconds(-1));
        await PublishAsync(accountA);
        await PublishAsync(accountB);

        var latest = await QueryLatestAsync(
            accountA.ConnectorId,
            accountA.Account,
            accountA.Instrument,
            accountA.Timeframe);

        Assert.NotNull(latest);
        Assert.Equal(accountA.Id, latest.Id);
    }

    [Fact]
    public async Task LatestQuery_FiltersByInstrument()
    {
        var connector = UniqueConnector();
        var euro = MarketConnectorIntegrationData.Snapshot(connector, instrument: "EURUSD");
        var bitcoin = MarketConnectorIntegrationData.Snapshot(
            connector,
            instrument: "BTC/USD",
            capturedAt: MarketConnectorIntegrationData.Now.AddSeconds(-1));
        await PublishAsync(euro);
        await PublishAsync(bitcoin);

        var latest = await QueryLatestAsync(
            euro.ConnectorId,
            euro.Account,
            euro.Instrument,
            euro.Timeframe);

        Assert.NotNull(latest);
        Assert.Equal(euro.Id, latest.Id);
    }

    [Fact]
    public async Task LatestQuery_FiltersByTimeframe()
    {
        var connector = UniqueConnector();
        var m15 = MarketConnectorIntegrationData.Snapshot(connector, timeframe: Timeframe.M15);
        var h1 = MarketConnectorIntegrationData.Snapshot(
            connector,
            timeframe: Timeframe.H1,
            capturedAt: MarketConnectorIntegrationData.Now.AddSeconds(-1));
        await PublishAsync(m15);
        await PublishAsync(h1);

        var latest = await QueryLatestAsync(
            m15.ConnectorId,
            m15.Account,
            m15.Instrument,
            m15.Timeframe);

        Assert.NotNull(latest);
        Assert.Equal(m15.Id, latest.Id);
    }

    [Fact]
    public async Task LatestQuery_RespectsMaximumAge()
    {
        var snapshot = MarketConnectorIntegrationData.Snapshot(
            UniqueConnector(),
            capturedAt: MarketConnectorIntegrationData.Now.AddMinutes(-10));
        await PublishAsync(snapshot);

        await using var context = fixture.CreateMarketDbContext();
        var handler = MarketConnectorIntegrationData.LatestHandler(
            new PostgreSqlMarketSnapshotRepository(context));
        var result = await handler.Handle(new GetLatestMarketSnapshotQuery(
            snapshot.ConnectorId,
            snapshot.Account,
            snapshot.Instrument,
            snapshot.Timeframe,
            TimeSpan.FromMinutes(5)), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Repository_ReturnsLatestReceivedAtByConnector()
    {
        var connector = UniqueConnector();
        var first = MarketConnectorIntegrationData.Snapshot(
            connector,
            receivedAt: MarketConnectorIntegrationData.Now.AddMinutes(-2));
        var second = MarketConnectorIntegrationData.Snapshot(
            connector,
            receivedAt: MarketConnectorIntegrationData.Now.AddMinutes(-1));
        await PublishAsync(first);
        await PublishAsync(second);
        await using var context = fixture.CreateMarketDbContext();

        var result = await new PostgreSqlMarketSnapshotRepository(context)
            .GetLatestReceivedAtAsync(first.ConnectorId, CancellationToken.None);

        Assert.Equal(second.ReceivedAt, result);
    }

    [Fact]
    public async Task ConcurrentIdenticalInsertions_AreAcceptedAndDuplicate()
    {
        var snapshot = MarketConnectorIntegrationData.Snapshot(UniqueConnector());

        var results = await Task.WhenAll(PublishAsync(snapshot), PublishAsync(snapshot));

        Assert.Single(results, result => result.Status == PublishMarketSnapshotStatus.Accepted);
        Assert.Single(results, result => result.Status == PublishMarketSnapshotStatus.Duplicate);
        await AssertSingleLogicalRecordAsync(snapshot);
    }

    [Fact]
    public async Task ConcurrentConflictingInsertions_AreAcceptedAndConflict()
    {
        var id = Guid.NewGuid();
        var connector = UniqueConnector();
        var first = MarketConnectorIntegrationData.Snapshot(connector, id, close: 1.11m);
        var changed = MarketConnectorIntegrationData.Snapshot(connector, id, close: 1.115m);

        var results = await Task.WhenAll(PublishAsync(first), PublishAsync(changed));

        Assert.Single(results, result => result.Status == PublishMarketSnapshotStatus.Accepted);
        Assert.Single(results, result => result.Status == PublishMarketSnapshotStatus.Conflict);
        await AssertSingleLogicalRecordAsync(first);
    }

    [Fact]
    public async Task Insert_RollsBackRecordWhenPayloadConstraintFails()
    {
        var snapshot = MarketConnectorIntegrationData.Snapshot(UniqueConnector());
        await using var context = fixture.CreateMarketDbContext();
        var repository = new PostgreSqlMarketSnapshotRepository(context);
        var invalid = MarketConnectorIntegrationData.PersistenceRequest(snapshot, schemaVersion: 0);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repository.InsertAsync(invalid, CancellationToken.None));
        context.ChangeTracker.Clear();

        Assert.False(await context.MarketSnapshotRecords.AnyAsync(record =>
            record.ConnectorId == snapshot.ConnectorId.Value
            && record.SnapshotId == snapshot.Id.Value));
    }

    [Fact]
    public async Task Repository_PropagatesCancellation()
    {
        var snapshot = MarketConnectorIntegrationData.Snapshot(UniqueConnector());
        await using var context = fixture.CreateMarketDbContext();
        var repository = new PostgreSqlMarketSnapshotRepository(context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.InsertAsync(
                MarketConnectorIntegrationData.PersistenceRequest(snapshot),
                cancellation.Token));
    }

    [Fact]
    public async Task MarketMigration_DoesNotImpactKnowledgeHubOrPgvectorObjects()
    {
        await using var knowledgeContext = fixture.CreateKnowledgeDbContext();
        var source = new KnowledgeSource("Market coexistence", KnowledgeSourceType.Text, new string('f', 64));
        source.StartProcessing();
        var embedding = new float[64];
        embedding[0] = 1;
        source.AddFragment(0, "Knowledge remains available.", 3, embedding);
        source.MarkReady();
        await knowledgeContext.KnowledgeSources.AddAsync(source);
        await knowledgeContext.SaveChangesAsync();

        Assert.True(await knowledgeContext.KnowledgeSources.AnyAsync(item => item.Id == source.Id));
        Assert.True(await ExistsAsync(knowledgeContext,
            "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector');"));
        Assert.True(await ExistsAsync(knowledgeContext,
            "SELECT to_regclass('knowledge_sources') IS NOT NULL;"));
        Assert.True(await ExistsAsync(knowledgeContext,
            "SELECT to_regclass('ix_knowledge_fragments_embedding') IS NOT NULL;"));
    }

    private async Task<PublishMarketSnapshotResult> PublishAsync(MarketSnapshot snapshot)
    {
        await using var context = fixture.CreateMarketDbContext();
        var repository = new PostgreSqlMarketSnapshotRepository(context);
        return await MarketConnectorIntegrationData.Handler(repository)
            .Handle(new PublishMarketSnapshotCommand(snapshot), CancellationToken.None);
    }

    private async Task<MarketSnapshot?> QueryLatestAsync(
        ConnectorId connectorId,
        ExternalAccountReference? account,
        Instrument instrument,
        Timeframe timeframe)
    {
        await using var context = fixture.CreateMarketDbContext();
        var handler = MarketConnectorIntegrationData.LatestHandler(
            new PostgreSqlMarketSnapshotRepository(context));
        return await handler.Handle(
            new GetLatestMarketSnapshotQuery(connectorId, account, instrument, timeframe),
            CancellationToken.None);
    }

    private async Task AssertSingleLogicalRecordAsync(MarketSnapshot snapshot)
    {
        await using var context = fixture.CreateMarketDbContext();
        Assert.Equal(1, await context.MarketSnapshotRecords.CountAsync(record =>
            record.ConnectorId == snapshot.ConnectorId.Value
            && record.SnapshotId == snapshot.Id.Value));
    }

    private static string UniqueConnector() => $"integration-{Guid.NewGuid():N}";

    private static Task<bool> ExistsAsync(DbContext context, string sql) =>
        ScalarAsync<bool>(context, sql);

    private static async Task<T> ScalarAsync<T>(DbContext context, string sql)
        where T : notnull
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var result = await command.ExecuteScalarAsync();
            return result is T typed
                ? typed
                : throw new InvalidOperationException($"Expected a {typeof(T).Name} scalar result.");
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
