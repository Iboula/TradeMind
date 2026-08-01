using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TradeMind.Brokers.Infrastructure.Persistence;

namespace TradeMind.Brokers.Infrastructure.Tests;

public sealed class PostgreSqlBrokerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("trademind_brokers")
        .WithUsername("trademind")
        .WithPassword("trademind")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
    public BrokerDbContext CreateContext() => new(new DbContextOptionsBuilder<BrokerDbContext>().UseNpgsql(ConnectionString, options => options.MigrationsAssembly(typeof(BrokerDbContext).Assembly.FullName)).Options);
}
