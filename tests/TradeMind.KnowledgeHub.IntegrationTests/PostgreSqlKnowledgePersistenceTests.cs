using Microsoft.EntityFrameworkCore;
using TradeMind.KnowledgeHub.Domain;
using TradeMind.KnowledgeHub.Infrastructure;

namespace TradeMind.KnowledgeHub.IntegrationTests;

public sealed class PostgreSqlKnowledgePersistenceTests(PostgreSqlKnowledgeHubFixture fixture)
    : IClassFixture<PostgreSqlKnowledgeHubFixture>
{
    private static readonly CancellationToken TestCancellationToken = CancellationToken.None;

    [Fact]
    public async Task Migrations_ShouldInstallVectorExtensionAndCreateHnswIndex()
    {
        await using var dbContext = fixture.CreateDbContext();

        var vectorExtensionExists = await ScalarAsync<bool>(
            dbContext,
            "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector');");
        var hnswIndexExists = await ScalarAsync<bool>(
            dbContext,
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_class index_class
                INNER JOIN pg_am access_method ON access_method.oid = index_class.relam
                WHERE index_class.relname = 'ix_knowledge_fragments_embedding'
                  AND access_method.amname = 'hnsw'
            );
            """);

        Assert.True(vectorExtensionExists);
        Assert.True(hnswIndexExists);
    }

    [Fact]
    public async Task Repository_ShouldPersistKnowledgeSourceWithFragments()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new PostgreSqlKnowledgeSourceRepository(dbContext);
        var source = CreateReadySource("Sprint 1 notes", Hash('A'));

        await repository.AddAsync(source, TestCancellationToken);
        await repository.SaveChangesAsync(TestCancellationToken);

        var persisted = await dbContext.KnowledgeSources
            .AsNoTracking()
            .Include(item => item.Fragments)
            .SingleAsync(item => item.Id == source.Id, TestCancellationToken);

        Assert.Equal("Sprint 1 notes", persisted.Title);
        Assert.Equal(ProcessingStatus.Ready, persisted.Status);
        Assert.Equal(2, persisted.Fragments.Count);
        Assert.All(persisted.Fragments, fragment => Assert.Equal(source.Id, fragment.KnowledgeSourceId));
    }

    [Fact]
    public async Task Repository_ShouldRetrieveKnowledgeSourceById()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new PostgreSqlKnowledgeSourceRepository(dbContext);
        var source = CreateReadySource("Vector retrieval", Hash('B'));

        await repository.AddAsync(source, TestCancellationToken);
        await repository.SaveChangesAsync(TestCancellationToken);

        var retrieved = await repository.GetAsync(source.Id, TestCancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal(source.Id, retrieved.Id);
        Assert.Equal("Vector retrieval", retrieved.Title);
        Assert.Equal(2, retrieved.Fragments.Count);
    }

    [Fact]
    public async Task Repository_ShouldSearchByCosineDistance()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new PostgreSqlKnowledgeSourceRepository(dbContext);
        var liquiditySource = CreateReadySource("Liquidity notes", Hash('C'), UnitVector(10), UnitVector(12));
        var riskSource = CreateReadySource("Risk notes", Hash('D'), UnitVector(11), UnitVector(13));

        await repository.AddAsync(liquiditySource, TestCancellationToken);
        await repository.AddAsync(riskSource, TestCancellationToken);
        await repository.SaveChangesAsync(TestCancellationToken);

        var results = await repository.SearchAsync(UnitVector(10), 2, TestCancellationToken);

        Assert.NotEmpty(results);
        Assert.Equal(liquiditySource.Id, results[0].SourceId);
        Assert.Equal(0, results[0].Sequence);
        Assert.True(results[0].Score > results[1].Score);
    }

    private static KnowledgeSource CreateReadySource(
        string title,
        string contentHash,
        float[]? firstEmbedding = null,
        float[]? secondEmbedding = null)
    {
        var source = new KnowledgeSource(title, KnowledgeSourceType.Text, contentHash);
        source.StartProcessing();
        source.AddFragment(0, $"{title} fragment 0", 4, firstEmbedding ?? UnitVector(0));
        source.AddFragment(1, $"{title} fragment 1", 4, secondEmbedding ?? UnitVector(1));
        source.MarkReady();
        return source;
    }

    private static float[] UnitVector(int index)
    {
        var embedding = new float[64];
        embedding[index] = 1f;
        return embedding;
    }

    private static string Hash(char value) => new(value, 64);

    private static async Task<T> ScalarAsync<T>(
        KnowledgeHubDbContext dbContext,
        string sql)
        where T : notnull
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(TestCancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var result = await command.ExecuteScalarAsync(TestCancellationToken);
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
