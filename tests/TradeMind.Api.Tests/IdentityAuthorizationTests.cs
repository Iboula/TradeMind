using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using TradeMind.Api.Contracts.Common;

namespace TradeMind.Api.Tests;

public sealed class IdentityAuthorizationTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory factory;

    public IdentityAuthorizationTests(IdentityApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task Anonymous_protected_pipeline_endpoint_returns_401()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/market-context/build", new { schemaVersion = 1, userId = "user", sessionId = "session", instrument = "EURUSD", timeframe = "M15" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Test_actor_with_exact_permission_can_call_pipeline_endpoint()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TradeMind-Test-Actor", "actor=actor-1;organization=org-1;tenant=tenant-1;permissions=TradeMind.MarketContext.Build");
        var response = await client.PostAsJsonAsync("/api/v1/market-context/build", new { schemaVersion = 1, userId = "user", sessionId = "session", instrument = "EURUSD", timeframe = "M15" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_actor_without_permission_returns_403()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TradeMind-Test-Actor", "actor=actor-1;organization=org-1;tenant=tenant-1;permissions=TradeMind.System.ReadVersion");
        var response = await client.PostAsJsonAsync("/api/v1/market-context/build", new { schemaVersion = 1, userId = "user", sessionId = "session", instrument = "EURUSD", timeframe = "M15" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_selector_mismatch_returns_403()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TradeMind-Test-Actor", "actor=actor-1;organization=org-1;tenant=tenant-1;permissions=TradeMind.MarketContext.Build");
        client.DefaultRequestHeaders.Add("X-TradeMind-Tenant-ID", "tenant-2");
        var response = await client.PostAsJsonAsync("/api/v1/market-context/build", new { schemaVersion = 1, userId = "user", sessionId = "session", instrument = "EURUSD", timeframe = "M15" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Identity_me_returns_safe_contract_without_raw_claims()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TradeMind-Test-Actor", "actor=actor-1;organization=org-1;tenant=tenant-1;permissions=TradeMind.System.ReadVersion");
        var response = await client.GetAsync("/api/v1/identity/me");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("actor-1", body, StringComparison.Ordinal);
        Assert.DoesNotContain("rawSecret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unauthorized_response_is_problem_details()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/execution-sessions");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Authentication required", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_documents_jwt_and_api_key_schemes()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("bearer", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("X-TradeMind-Api-Key", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Authorization_handler_does_not_capture_a_scoped_actor_accessor()
    {
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var firstHandler = first.ServiceProvider.GetServices<IAuthorizationHandler>().Single(handler => handler.GetType().Name == "PermissionAuthorizationHandler");
        var secondHandler = second.ServiceProvider.GetServices<IAuthorizationHandler>().Single(handler => handler.GetType().Name == "PermissionAuthorizationHandler");
        Assert.NotSame(firstHandler, secondHandler);
    }
}

public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("TradeMind:Identity:Enabled", "true");
        builder.UseSetting("TradeMind:Identity:Jwt:Enabled", "false");
        builder.UseSetting("TradeMind:Identity:Development:EnableTestAuthentication", "true");
        builder.UseSetting("TradeMind:Identity:RateLimiting:Enabled", "false");
        builder.UseSetting("TradeMind:Api:Security:EnableHttpsRedirection", "false");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnowledgeHub"] = string.Empty,
            ["ConnectionStrings:MarketConnectors"] = string.Empty,
            ["ConnectionStrings:Identity"] = string.Empty,
            ["TradeMind:Identity:Enabled"] = "true",
            ["TradeMind:Identity:Jwt:Enabled"] = "false",
            ["TradeMind:Identity:Development:EnableTestAuthentication"] = "true",
            ["TradeMind:Identity:RateLimiting:Enabled"] = "false"
        }));
    }
}
