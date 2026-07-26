using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TradeMind.ExecutionSessions.Infrastructure.Persistence;

namespace TradeMind.ExecutionSessions.Infrastructure.Tests;

public sealed class PostgreSqlExecutionSessionsFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("trademind_execution_tests")
        .WithUsername("trademind")
        .WithPassword("trademind")
        .Build();

    public string ConnectionString => postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    public ExecutionSessionsDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ExecutionSessionsDbContext>()
        .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsAssembly(typeof(ExecutionSessionsDbContext).Assembly.FullName))
        .Options);
}
