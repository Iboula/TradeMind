using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TradeMind.KnowledgeHub.Infrastructure;
using TradeMind.MarketConnectors.Infrastructure;

namespace TradeMind.AI.Context.IntegrationTests;

public sealed class PostgreSqlContextFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("trademind_context_tests")
        .WithUsername("trademind")
        .WithPassword("trademind")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using (var knowledge = CreateKnowledgeDbContext())
        {
            await knowledge.Database.MigrateAsync();
        }

        await using var market = CreateMarketDbContext();
        await market.Database.MigrateAsync();
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
        var options = new DbContextOptionsBuilder<MarketConnectorsDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(MarketConnectorsDbContext).Assembly.FullName))
            .Options;
        return new MarketConnectorsDbContext(options);
    }
}
