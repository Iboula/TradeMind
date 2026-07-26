using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TradeMind.KnowledgeHub.Infrastructure;

namespace TradeMind.KnowledgeHub.IntegrationTests;

public sealed class PostgreSqlKnowledgeHubFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("trademind_tests")
        .WithUsername("trademind")
        .WithPassword("trademind")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    public KnowledgeHubDbContext CreateDbContext()
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
}
