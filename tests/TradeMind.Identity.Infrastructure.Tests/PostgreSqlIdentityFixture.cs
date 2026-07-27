using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TradeMind.Identity.Infrastructure.Persistence;

namespace TradeMind.Identity.Infrastructure.Tests;

public sealed class PostgreSqlIdentityFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("trademind_identity_tests")
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
    public IdentityDbContext CreateDbContext() => new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(ConnectionString, options => options.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName)).Options);
}
