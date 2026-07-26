using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using TradeMind.Api.Contracts.MarketContext;

namespace TradeMind.Api.Tests;

public sealed class ApiHostTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public ApiHostTests(ApiApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Api_starts_and_returns_version_metadata()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/system/version");
        var body = await response.Content.ReadFromJsonAsync<VersionResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("v1", body!.ApiVersion);
        Assert.Equal("v1.0.0-core", body.CoreReleaseVersion);
    }

    [Fact]
    public async Task Live_and_ready_health_checks_are_machine_readable()
    {
        using var client = _factory.CreateClient();
        var live = await client.GetFromJsonAsync<HealthResponse>("/health/live");
        var ready = await client.GetFromJsonAsync<HealthResponse>("/health/ready");
        Assert.Equal("Healthy", live!.Status);
        Assert.Equal("live", live.Check);
        Assert.Equal("Healthy", ready!.Status);
        Assert.Equal("ready", ready.Check);
    }

    [Fact]
    public async Task Correlation_is_generated_and_returned()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/system/version");
        var correlation = response.Headers.GetValues("X-Correlation-ID").Single();
        Assert.Matches("^[A-Za-z0-9._:-]+$", correlation);
    }

    [Fact]
    public async Task Valid_incoming_correlation_is_preserved()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "client-correlation-01");
        var response = await client.GetAsync("/api/v1/system/version");
        Assert.Equal("client-correlation-01", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Unsafe_correlation_is_replaced_deterministically()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", new string('x', 200));
        var response = await client.GetAsync("/api/v1/system/version");
        var correlation = response.Headers.GetValues("X-Correlation-ID").Single();
        Assert.NotEqual(new string('x', 200), correlation);
        Assert.InRange(correlation.Length, 8, 64);
    }

    [Fact]
    public async Task Missing_market_context_fields_return_problem_details()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/market-context/build", new { schemaVersion = 1 });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("errors", body, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unsupported_schema_version_returns_client_error()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/consensus/build", new { schemaVersion = 99, payload = new { } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("UNSUPPORTED_SCHEMA_VERSION", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unexpected_exception_returns_safe_problem_details()
    {
        var behavior = new FakeApiBehavior { ThrowUnexpectedException = true };
        using var factory = new ApiApplicationFactory(behavior);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/consensus/build", ValidModuleRequest());
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Internal server error", body, StringComparison.Ordinal);
        Assert.DoesNotContain("internal test failure", body, StringComparison.Ordinal);
        Assert.DoesNotContain("TradeMind.Api.Tests", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_is_mapped_without_exposing_exception_details()
    {
        var behavior = new FakeApiBehavior { ThrowCancellation = true };
        using var factory = new ApiApplicationFactory(behavior);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/consensus/build", ValidModuleRequest());
        Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
        Assert.DoesNotContain("OperationCanceledException", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_document_is_available_in_test_environment()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("TradeMind API", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Market_context_endpoint_invokes_application_facade()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/market-context/build", new BuildMarketContextApiRequest(1, "user", "session", "EURUSD", "M15"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(ApiModule.MarketContext, _factory.Behavior.Invocations);
    }

    [Theory]
    [InlineData("/api/v1/experts/dispatch", "ExpertDispatch")]
    [InlineData("/api/v1/experts/analyze", "ExpertAnalysis")]
    [InlineData("/api/v1/consensus/build", "Consensus")]
    [InlineData("/api/v1/trading-decisions/evaluate", "TradingDecision")]
    [InlineData("/api/v1/risk/evaluate", "Risk")]
    [InlineData("/api/v1/trading-plans/generate", "TradingPlan")]
    [InlineData("/api/v1/trading-workspaces/build", "TradingWorkspace")]
    [InlineData("/api/v1/trading-assistant/ask", "TradingAssistant")]
    [InlineData("/api/v1/paper-trading/simulate", "PaperTrading")]
    public async Task Pipeline_endpoint_invokes_application_facade(string path, string moduleName)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(ValidModuleRequest())
        };
        if (path.Contains("workspaces", StringComparison.Ordinal) || path.Contains("assistant", StringComparison.Ordinal) || path.Contains("paper-trading", StringComparison.Ordinal))
        {
            request.Headers.Add("Idempotency-Key", $"test-{moduleName}");
        }

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(_factory.Behavior.Invocations, module => module.ToString() == moduleName);
    }

    [Fact]
    public async Task Same_idempotency_key_replays_the_same_response()
    {
        var behavior = new FakeApiBehavior();
        using var factory = new ApiApplicationFactory(behavior);
        using var client = factory.CreateClient();
        var first = new HttpRequestMessage(HttpMethod.Post, "/api/v1/trading-assistant/ask") { Content = JsonContent.Create(ValidModuleRequest()) };
        first.Headers.Add("Idempotency-Key", "replay-key");
        var second = new HttpRequestMessage(HttpMethod.Post, "/api/v1/trading-assistant/ask") { Content = JsonContent.Create(ValidModuleRequest()) };
        second.Headers.Add("Idempotency-Key", "replay-key");
        var firstResponse = await client.SendAsync(first);
        var secondResponse = await client.SendAsync(second);
        Assert.Equal(await firstResponse.Content.ReadAsStringAsync(), await secondResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, behavior.ExecutionCount);
    }

    [Fact]
    public async Task Reusing_idempotency_key_with_different_payload_returns_conflict()
    {
        var behavior = new FakeApiBehavior();
        using var factory = new ApiApplicationFactory(behavior);
        using var client = factory.CreateClient();
        var first = new HttpRequestMessage(HttpMethod.Post, "/api/v1/trading-assistant/ask") { Content = JsonContent.Create(ValidModuleRequest()) };
        first.Headers.Add("Idempotency-Key", "conflict-key");
        var second = new HttpRequestMessage(HttpMethod.Post, "/api/v1/trading-assistant/ask") { Content = JsonContent.Create(new { schemaVersion = 1, payload = new { different = true } }) };
        second.Headers.Add("Idempotency-Key", "conflict-key");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(second)).StatusCode);
        Assert.Equal(1, behavior.ExecutionCount);
    }

    [Fact]
    public async Task Concurrent_same_key_executes_operation_once()
    {
        var behavior = new FakeApiBehavior { WaitForRelease = true };
        using var factory = new ApiApplicationFactory(behavior);
        using var client = factory.CreateClient();
        var firstTask = SendIdempotentAsync(client, "concurrent-key");
        await behavior.Started.Task;
        var secondTask = SendIdempotentAsync(client, "concurrent-key");
        behavior.Release.TrySetResult(true);
        var responses = await Task.WhenAll(firstTask, secondTask);
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1, behavior.ExecutionCount);
    }

    [Fact]
    public async Task Invalid_idempotency_key_returns_bad_request()
    {
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/trading-assistant/ask") { Content = JsonContent.Create(ValidModuleRequest()) };
        request.Headers.Add("Idempotency-Key", new string('x', 200));
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Payload_over_configured_limit_returns_payload_too_large()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/trading-assistant/ask")
        {
            Content = new ByteArrayContent(new byte[(5 * 1024 * 1024) + 1])
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add("Idempotency-Key", "oversized-payload");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Security_headers_are_present()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/system/version");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public void Api_contracts_are_records_with_get_only_properties()
    {
        var contractTypes = typeof(ApiResponseEnvelope).Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith("TradeMind.Api.Contracts", StringComparison.Ordinal) == true)
            .Where(type => type.IsClass && (type.Name.EndsWith("Request", StringComparison.Ordinal) || type.Name.EndsWith("Response", StringComparison.Ordinal)));
        Assert.NotEmpty(contractTypes);
        Assert.All(contractTypes, type => Assert.True(type.GetProperties().All(property => property.SetMethod is null || property.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)))));
    }

    [Fact]
    public void Api_assembly_has_no_broker_mt5_or_concrete_llm_reference()
    {
        var references = typeof(Program).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.DoesNotContain(references, name => name is not null && (name.Contains("MT5", StringComparison.OrdinalIgnoreCase) || name.Contains("MetaTrader", StringComparison.OrdinalIgnoreCase) || name.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)));
    }

    private static object ValidModuleRequest() => new { schemaVersion = 1, payload = new { request = "test" } };

    private static Task<HttpResponseMessage> SendIdempotentAsync(HttpClient client, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/trading-assistant/ask") { Content = JsonContent.Create(ValidModuleRequest()) };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }
}
