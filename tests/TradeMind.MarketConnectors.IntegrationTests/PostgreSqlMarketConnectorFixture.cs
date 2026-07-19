using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TradeMind.KnowledgeHub.Infrastructure;
using TradeMind.MarketConnectors.Infrastructure;

namespace TradeMind.MarketConnectors.IntegrationTests;

public sealed class PostgreSqlMarketConnectorFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("trademind_market_tests")
        .WithUsername("trademind")
        .WithPassword("trademind")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using (var knowledgeContext = CreateKnowledgeDbContext())
        {
            await knowledgeContext.Database.MigrateAsync();
        }

        await using var marketContext = CreateMarketDbContext();
        await marketContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    public KnowledgeHubDbContext CreateKnowledgeDbContext()
    {
        var options = new DbContextOptionsBuilder<KnowledgeHubDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(KnowledgeHubDbContext).Assembly.FullName);
                npgsql.UseVector();
            })
            .Options;
        return new KnowledgeHubDbContext(options);
    }

    public MarketConnectorsDbContext CreateMarketDbContext()
    {
        return CreateMarketDbContext(ConnectionString);
    }

    public MarketConnectorsDbContext CreateMarketDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<MarketConnectorsDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(MarketConnectorsDbContext).Assembly.FullName))
            .Options;
        return new MarketConnectorsDbContext(options);
    }

    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var databaseName = $"market_empty_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();

        var databaseBuilder = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = databaseName };
        return databaseBuilder.ConnectionString;
    }
}
