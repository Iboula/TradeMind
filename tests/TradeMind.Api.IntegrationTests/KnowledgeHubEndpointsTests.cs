using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TradeMind.Modules.KnowledgeHub.Application;

namespace TradeMind.Api.IntegrationTests;

public sealed class KnowledgeHubEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public KnowledgeHubEndpointsTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task UploadThenSearch_ReturnsIndexedFragment()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("Risk management protects trading capital."u8.ToArray()), "file", "risk.txt");

        var upload = await _client.PostAsync("/knowledge/sources", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var source = await upload.Content.ReadFromJsonAsync<KnowledgeSourceDto>();
        Assert.NotNull(source);

        var get = await _client.GetAsync($"/knowledge/sources/{source.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var search = await _client.GetFromJsonAsync<List<KnowledgeSearchResult>>("/knowledge/search?query=risk%20capital");
        Assert.NotNull(search);
        Assert.NotEmpty(search);
        Assert.True(search.Count <= 5);
    }
}
