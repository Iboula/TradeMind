using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using TradeMind.Modules.KnowledgeHub.Application;

namespace TradeMind.Api.IntegrationTests;

public sealed class KnowledgeHubEndpointsTests : IAsyncLifetime
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
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:KnowledgeHub"] = _postgres.GetConnectionString()
                }));
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
    public async Task UploadThenSearch_ReturnsIndexedFragment()
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent("Risk management protects trading capital."u8.ToArray());
        content.Headers.ContentType = new("text/plain");
        form.Add(content, "file", "risk.txt");

        var upload = await _client.PostAsync("/knowledge/sources", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var source = await upload.Content.ReadFromJsonAsync<KnowledgeSourceDto>();
        Assert.NotNull(source);
        Assert.Equal("Indexed", source.Status);

        var get = await _client.GetAsync($"/knowledge/sources/{source.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var search = await _client.GetFromJsonAsync<List<KnowledgeSearchResult>>(
            "/knowledge/search?query=risk%20capital");
        Assert.NotNull(search);
        Assert.NotEmpty(search);
        Assert.True(search.Count <= 5);
        Assert.Equal(source.Id, search[0].SourceId);
    }
}
