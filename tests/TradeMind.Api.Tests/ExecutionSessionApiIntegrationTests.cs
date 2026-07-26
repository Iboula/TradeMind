using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TradeMind.Api.Contracts.ExecutionSessions;
using TradeMind.ExecutionSessions.Infrastructure.Persistence;

namespace TradeMind.Api.Tests;

public sealed class ExecutionSessionApiIntegrationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("trademind_api_execution_tests")
        .WithUsername("trademind")
        .WithPassword("trademind")
        .Build();

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        await using var context = new ExecutionSessionsDbContext(new DbContextOptionsBuilder<ExecutionSessionsDbContext>()
            .UseNpgsql(postgres.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(ExecutionSessionsDbContext).Assembly.FullName))
            .Options);
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("TradeMind:Api:Security:EnableHttpsRedirection", "false");
        builder.UseSetting("TradeMind:Persistence:Provider", "PostgreSql");
        builder.UseSetting("TradeMind:Persistence:ConnectionStringName", "TradeMind");
        builder.UseSetting("ConnectionStrings:TradeMind", postgres.GetConnectionString());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnowledgeHub"] = string.Empty,
            ["ConnectionStrings:MarketConnectors"] = string.Empty,
            ["ConnectionStrings:TradeMind"] = postgres.GetConnectionString(),
            ["TradeMind:Persistence:Provider"] = "PostgreSql",
            ["TradeMind:Persistence:ConnectionStringName"] = "TradeMind",
            ["TradeMind:Persistence:ApplyMigrationsOnStartup"] = "false"
        }));
    }
}

public sealed class ExecutionSessionApiIntegrationTests : IClassFixture<ExecutionSessionApiIntegrationFactory>
{
    private readonly ExecutionSessionApiIntegrationFactory factory;

    public ExecutionSessionApiIntegrationTests(ExecutionSessionApiIntegrationFactory factory) => this.factory = factory;

    [Fact]
    public async Task Create_and_get_execution_session_use_durable_postgresql_storage()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/execution-sessions/")
        {
            Content = JsonContent.Create(CreateRequest())
        };
        request.Headers.Add("Idempotency-Key", "api-session-create-1");

        var created = await client.SendAsync(request);
        var createdBody = await created.Content.ReadFromJsonAsync<ExecutionSessionApiResponse>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(createdBody);
        Assert.Equal("Running", createdBody!.Data.Status);

        var fetched = await client.GetFromJsonAsync<ExecutionSessionApiResponse>(
            $"/api/v1/execution-sessions/{createdBody.Data.Id}");
        Assert.NotNull(fetched);
        Assert.Equal(createdBody.Data.Id, fetched!.Data.Id);
        Assert.Equal(createdBody.Data.CorrelationId, fetched.Data.CorrelationId);
    }

    [Fact]
    public async Task Repeating_create_with_same_idempotency_key_replays_the_persisted_response()
    {
        using var client = factory.CreateClient();
        var payload = CreateRequest() with { CorrelationId = "api-replay-correlation" };

        using var first = new HttpRequestMessage(HttpMethod.Post, "/api/v1/execution-sessions/")
        {
            Content = JsonContent.Create(payload)
        };
        first.Headers.Add("Idempotency-Key", "api-session-replay-1");
        using var second = new HttpRequestMessage(HttpMethod.Post, "/api/v1/execution-sessions/")
        {
            Content = JsonContent.Create(payload)
        };
        second.Headers.Add("Idempotency-Key", "api-session-replay-1");

        var firstResponse = await client.SendAsync(first);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var secondResponse = await client.SendAsync(second);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(firstBody, secondBody);
    }

    [Fact]
    public async Task Unknown_execution_session_returns_structured_not_found()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/execution-sessions/{Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Execution session not found", body, StringComparison.Ordinal);
        Assert.Contains("traceId", body, StringComparison.Ordinal);
    }

    private static CreateExecutionSessionApiRequest CreateRequest() => new(
        1, "api-session-correlation", "EURUSD", "M15", "Api", "api-test", "v1", "v1",
        StartedAtUtc: DateTimeOffset.UtcNow);
}
