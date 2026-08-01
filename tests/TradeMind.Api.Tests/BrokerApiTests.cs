using System.Net;
using System.Net.Http.Json;

namespace TradeMind.Api.Tests;

public sealed class BrokerApiTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public BrokerApiTests(ApiApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Broker_discovery_returns_only_neutral_descriptor_data()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/brokers");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("in-memory", body, StringComparison.Ordinal);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Broker_health_is_available_without_live_execution()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/brokers/in-memory/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Submit_order_requires_execution_session_and_never_fabricates_success()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/broker-orders", new
        {
            connectorId = "in-memory",
            accountId = "account",
            clientOrderId = "client-1",
            instrument = "EURUSD",
            side = "buy",
            orderType = "market",
            quantity = 1,
            timeInForce = "day",
            idempotencyKey = "key-1",
            mode = "simulation"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
