using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using TradeMind.Modules.Knowledge.Application;

namespace TradeMind.Modules.Knowledge.IntegrationTests;

public sealed class KnowledgeHubApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("trademind")
        .WithUsername("trademind")
        .WithPassword("trademind")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("IntegrationTests");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:KnowledgeDatabase"] = _postgres.GetConnectionString()
                });
            });
        });
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task UploadThenSearch_ReturnsRelevantFragment()
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent("Risk management protects trading capital and limits drawdown."u8.ToArray());
        content.Headers.ContentType = new("text/plain");
        form.Add(content, "file", "risk-management.txt");

        var uploadResponse = await _client.PostAsync("/knowledge/sources", form);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var source = await uploadResponse.Content.ReadFromJsonAsync<KnowledgeSourceResponse>();
        Assert.NotNull(source);
        Assert.Equal("Indexed", source.Status);

        var results = await _client.GetFromJsonAsync<IReadOnlyList<KnowledgeSearchResult>>(
            "/knowledge/search?query=protect%20capital&limit=5");

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Equal(source.Id, results[0].SourceId);
    }
}
