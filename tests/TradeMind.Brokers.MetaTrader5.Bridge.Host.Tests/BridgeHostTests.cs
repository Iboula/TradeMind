using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Client;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Configuration;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Security;
using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Configuration;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Runtime;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Tests;

public sealed class BridgeHostTests
{
    [Fact]
    public async Task Health_endpoints_expose_liveness_readiness_and_startup_without_sensitive_fields()
    {
        await using var factory = new BridgeHostFactory();
        using var client = factory.CreateClient();
        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");
        var startup = await client.GetAsync("/health/startup");
        var body = await ready.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, startup.StatusCode);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("account number", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Compatible_handshake_succeeds_and_returns_capabilities_and_terminal_build()
    {
        await using var factory = new BridgeHostFactory();
        using var client = Authenticated(factory);
        var response = await client.PostAsJsonAsync("/bridge/v1/handshake", Handshake());
        var result = await response.Content.ReadFromJsonAsync<BridgeHandshakeResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Null(result!.Error);
        Assert.Equal("simulation-host-1.0", result.BridgeVersion);
        Assert.Equal("simulation-terminal-1.0", result.TerminalBuild);
        Assert.Contains(result.Capabilities, capability => capability.Name == "orders");
    }

    [Fact]
    public async Task Incompatible_protocol_is_rejected_before_execution()
    {
        await using var factory = new BridgeHostFactory();
        using var client = Authenticated(factory);
        var response = await client.PostAsJsonAsync("/bridge/v1/handshake", Handshake(protocol: new BridgeProtocolVersion(2, 0)));
        var result = await response.Content.ReadFromJsonAsync<BridgeHandshakeResponse>();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(BridgeErrorCode.ProtocolVersionMismatch, result!.Error!.Code);
    }

    [Fact]
    public async Task Live_environment_and_live_execution_are_rejected_independently()
    {
        await using var factory = new BridgeHostFactory();
        using var client = Authenticated(factory);
        var handshake = await client.PostAsJsonAsync("/bridge/v1/handshake", Handshake(demoOnly: false, accountEnvironment: "Live"));
        var handshakeResult = await handshake.Content.ReadFromJsonAsync<BridgeHandshakeResponse>();
        Assert.Equal(BridgeErrorCode.LiveModeForbidden, handshakeResult!.Error!.Code);
        var execute = await client.PostAsJsonAsync("/bridge/v1/execute", Envelope(fields: new Dictionary<string, string> { ["mode"] = "Live" }));
        var executeResult = await execute.Content.ReadFromJsonAsync<BridgeTransportResponse>();
        Assert.Equal(HttpStatusCode.Forbidden, execute.StatusCode);
        Assert.Equal(BridgeErrorCode.LiveModeForbidden, executeResult!.Error!.Code);
    }

    [Fact]
    public async Task Missing_authentication_is_rejected_for_handshake_and_commands()
    {
        await using var factory = new BridgeHostFactory();
        using var client = factory.CreateClient();
        var handshake = await client.PostAsJsonAsync("/bridge/v1/handshake", Handshake());
        var result = await handshake.Content.ReadFromJsonAsync<BridgeHandshakeResponse>();
        Assert.Equal(HttpStatusCode.Unauthorized, handshake.StatusCode);
        Assert.Equal(BridgeErrorCode.AuthenticationFailed, result!.Error!.Code);
        var command = await client.PostAsJsonAsync("/bridge/v1/execute", Envelope());
        var commandResult = await command.Content.ReadFromJsonAsync<BridgeTransportResponse>();
        Assert.Equal(BridgeErrorCode.AuthenticationFailed, commandResult!.Error!.Code);
    }

    [Fact]
    public async Task Excessive_clock_skew_and_stale_deadlines_are_rejected()
    {
        await using var factory = new BridgeHostFactory();
        using var client = Authenticated(factory);
        var old = Handshake(clientTime: DateTimeOffset.UtcNow.AddMinutes(-10));
        var response = await client.PostAsJsonAsync("/bridge/v1/handshake", old);
        var result = await response.Content.ReadFromJsonAsync<BridgeHandshakeResponse>();
        Assert.Equal(BridgeErrorCode.ClockSkewExceeded, result!.Error!.Code);
        var expired = await client.PostAsJsonAsync("/bridge/v1/execute", Envelope(deadline: DateTimeOffset.UtcNow.AddSeconds(-1)));
        var expiredResult = await expired.Content.ReadFromJsonAsync<BridgeTransportResponse>();
        Assert.Equal(BridgeErrorCode.Timeout, expiredResult!.Error!.Code);
    }

    [Fact]
    public async Task Missing_capability_is_rejected_during_handshake()
    {
        await using var factory = new BridgeHostFactory();
        using var client = Authenticated(factory);
        var response = await client.PostAsJsonAsync("/bridge/v1/handshake", Handshake(required: [new BridgeCapability("unsupported", "1")]));
        var result = await response.Content.ReadFromJsonAsync<BridgeHandshakeResponse>();
        Assert.Equal(BridgeErrorCode.InvalidRequest, result!.Error!.Code);
    }

    [Fact]
    public async Task Same_idempotency_key_and_hash_replays_the_safe_response()
    {
        await using var factory = new BridgeHostFactory();
        using var client = Authenticated(factory);
        var request = Envelope("request-1", "nonce-1111111111111111", Hash("same-key"), Hash("same-request"));
        var first = await client.PostAsJsonAsync("/bridge/v1/execute", request);
        var second = await client.PostAsJsonAsync("/bridge/v1/execute", request);
        var firstResult = await first.Content.ReadFromJsonAsync<BridgeTransportResponse>();
        var secondResult = await second.Content.ReadFromJsonAsync<BridgeTransportResponse>();
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstResult!.PayloadJson, secondResult!.PayloadJson);
        Assert.Null(secondResult.Error);
    }

    [Fact]
    public async Task Same_idempotency_key_with_different_hash_returns_conflict()
    {
        await using var factory = new BridgeHostFactory();
        using var client = Authenticated(factory);
        await client.PostAsJsonAsync("/bridge/v1/execute", Envelope("request-1", "nonce-2222222222222222", Hash("conflict-key"), Hash("first")));
        var response = await client.PostAsJsonAsync("/bridge/v1/execute", Envelope("request-2", "nonce-3333333333333333", Hash("conflict-key"), Hash("second")));
        var result = await response.Content.ReadFromJsonAsync<BridgeTransportResponse>();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(BridgeErrorCode.Conflict, result!.Error!.Code);
    }

    [Fact]
    public async Task Replayed_nonce_with_different_request_is_rejected()
    {
        await using var factory = new BridgeHostFactory();
        using var client = Authenticated(factory);
        await client.PostAsJsonAsync("/bridge/v1/execute", Envelope("request-1", "nonce-4444444444444444", Hash("nonce-key-1"), Hash("nonce-request-1")));
        var response = await client.PostAsJsonAsync("/bridge/v1/execute", Envelope("request-2", "nonce-4444444444444444", Hash("nonce-key-2"), Hash("nonce-request-2")));
        var result = await response.Content.ReadFromJsonAsync<BridgeTransportResponse>();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(BridgeErrorCode.ReplayDetected, result!.Error!.Code);
    }

    [Fact]
    public async Task Malformed_command_payload_is_returned_as_a_normalized_error()
    {
        await using var factory = new BridgeHostFactory();
        using var client = Authenticated(factory);
        var response = await client.PostAsJsonAsync("/bridge/v1/execute", Envelope("request-invalid", "nonce-invalid-1111111", payloadJson: "not-json"));
        var result = await response.Content.ReadFromJsonAsync<BridgeTransportResponse>();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(BridgeErrorCode.InvalidRequest, result!.Error!.Code);
        Assert.DoesNotContain("JsonException", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Demo_submit_modify_cancel_and_close_are_normalized()
    {
        await using var factory = new BridgeHostFactory();
        var httpClient = Authenticated(factory);
        var client = new MT5BridgeClient(httpClient, Options.Create(new MT5BridgeClientOptions { Endpoint = httpClient.BaseAddress!.ToString(), RequireTls = false, AllowInsecureDemoTransport = true }), new MutualTlsBridgeAuthenticator(), new NoopTelemetry(), TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<MT5BridgeClient>.Instance);
        await using (client)
        {
            await client.OpenAsync(CancellationToken.None);
            await client.AuthenticateAsync(CancellationToken.None);
            var submit = await client.SendAsync(Wire("submit-order", new Dictionary<string, string> { ["account_id"] = "mt5-demo-account", ["client_order_id"] = "integration-market", ["instrument"] = "EURUSD", ["side"] = "Buy", ["order_type"] = "Market", ["quantity"] = "1", ["time_in_force"] = "Day" }), CancellationToken.None);
            Assert.Contains("ORDER_SUBMITTED", submit, StringComparison.Ordinal);
            var orderJson = JsonDocument.Parse(submit).RootElement.GetProperty("fields").GetProperty("order").GetString()!;
            var orderId = JsonDocument.Parse(orderJson).RootElement.GetProperty("orderId").GetString()!;
            await client.SendAsync(Wire("modify-order", new Dictionary<string, string> { ["order_id"] = orderId, ["quantity"] = "2", ["limit_price"] = "1.09" }), CancellationToken.None);
            await client.SendAsync(Wire("cancel-order", new Dictionary<string, string> { ["order_id"] = orderId }), CancellationToken.None);
            var positionId = "mt5-position-" + StableId("integration-market");
            var closed = await client.SendAsync(Wire("close-position", new Dictionary<string, string> { ["position_id"] = positionId }), CancellationToken.None);
            Assert.Contains("POSITION_CLOSED", closed, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Bounded_concurrency_accepts_parallel_queries_without_shared_state_leakage()
    {
        await using var factory = new BridgeHostFactory(maxConcurrentRequests: 2);
        using var client = Authenticated(factory);
        var tasks = Enumerable.Range(0, 12).Select(async index =>
        {
            var response = await client.PostAsJsonAsync("/bridge/v1/execute", Envelope($"request-parallel-{index}", $"nonce-parallel-{index:000000000000}", Hash($"parallel-key-{index}"), Hash("parallel-request")));
            return response.StatusCode;
        });
        var statuses = await Task.WhenAll(tasks);
        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    [Fact]
    public void Host_state_machine_requires_explicit_transitions()
    {
        var runtime = new MT5BridgeHostRuntime();
        Assert.Equal(MT5BridgeHostState.Starting, runtime.State);
        runtime.MarkWaitingForTerminal();
        runtime.MarkReady();
        runtime.MarkDegraded();
        runtime.MarkReady();
        runtime.MarkStopping();
        runtime.MarkStopped();
        Assert.Equal(MT5BridgeHostState.Stopped, runtime.State);
        Assert.Throws<InvalidOperationException>(() => runtime.MarkReady());
    }

    [Fact]
    public void Host_options_reject_live_and_plaintext_without_local_demo_opt_in()
    {
        var validator = new MT5BridgeHostOptionsValidator();
        Assert.False(validator.Validate(null, new MT5BridgeHostOptions { AllowLive = true }).Succeeded);
        Assert.False(validator.Validate(null, new MT5BridgeHostOptions { RequireTls = false, AllowInsecureDemoTransport = false }).Succeeded);
        Assert.False(validator.Validate(null, new MT5BridgeHostOptions { Environment = "Live" }).Succeeded);
    }

    [Fact]
    public void Host_and_client_do_not_reference_an_mt5_sdk_or_analytical_core()
    {
        var hostReferences = typeof(Program).Assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty);
        var clientReferences = typeof(MT5BridgeClient).Assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty);
        Assert.DoesNotContain(hostReferences, name => name.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase) || name.Contains("MT5Sdk", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(clientReferences, name => name.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase) || name.Contains("MT5Sdk", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(hostReferences, name => name.Contains("TradeMind.AI", StringComparison.OrdinalIgnoreCase));
    }

    private static HttpClient Authenticated(BridgeHostFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bridge-Client-Certificate", "present");
        return client;
    }

    private static BridgeHandshakeRequest Handshake(BridgeProtocolVersion? protocol = null, bool demoOnly = true, string accountEnvironment = "Demo", DateTimeOffset? clientTime = null, IReadOnlyList<BridgeCapability>? required = null) => new(protocol ?? BridgeProtocolVersion.Current, "1.0.0", demoOnly, "Demo", accountEnvironment, clientTime ?? DateTimeOffset.UtcNow, required ?? [new BridgeCapability("orders", "1")], Metadata());

    private static BridgeTransportRequest Envelope(string requestId = "request-default", string nonce = "nonce-default-111111", string? idempotencyHash = null, string? requestHash = null, DateTimeOffset? deadline = null, IReadOnlyDictionary<string, string>? fields = null, string? payloadJson = null)
    {
        var requestedDeadline = deadline;
        var now = requestedDeadline?.AddSeconds(-5) ?? DateTimeOffset.UtcNow;
        var payload = payloadJson ?? JsonSerializer.Serialize(new { command = "heartbeat", fields = fields ?? new Dictionary<string, string>() }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new(BridgeProtocolVersion.Current, BridgeOperation.Heartbeat, new(requestId, "corr-" + requestId, "session-1", now, nonce, idempotencyHash ?? Hash(requestId + "-key"), requestHash ?? Hash(requestId + "-request"), requestedDeadline ?? now.AddMinutes(1)), payload);
    }

    private static string Wire(string command, IReadOnlyDictionary<string, string> fields) => JsonSerializer.Serialize(new { command, fields }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string StableId(string value) => Hash(value)[..16];
    private static BridgeRequestMetadata Metadata() { var now = DateTimeOffset.UtcNow; return new("request-handshake", "corr-handshake", "session-handshake", now, "nonce-handshake-111111", Hash("handshake-key"), Hash("handshake-request"), now.AddMinutes(1)); }
    private sealed class NoopTelemetry : TradeMind.Observability.Abstractions.ITradeMindTelemetry
    {
        public TradeMind.Observability.Abstractions.ITradeMindActivity StartActivity(TradeMind.Observability.Abstractions.TelemetryOperation operation, TradeMind.Observability.Abstractions.TelemetryContext? context = null, IReadOnlyCollection<TradeMind.Observability.Abstractions.TelemetryLink>? links = null) => new NoopActivity();
        public void EnrichCurrent(TradeMind.Observability.Abstractions.TelemetryContext context) { }
    }
    private sealed class NoopActivity : TradeMind.Observability.Abstractions.ITradeMindActivity
    {
        public bool IsRecording => false; public string? TraceId => null; public string? SpanId => null; public void Dispose() { } public void SetTag(string name, string? value) { } public void SetTag(string name, long? value) { } public void SetOutcome(TradeMind.Observability.Abstractions.TelemetryOutcome outcome) { } public void RecordException(Exception exception) { }
    }
}

public sealed class BridgeHostFactory(int maxConcurrentRequests = 4) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TradeMind:Brokers:MetaTrader5:BridgeHost:RequireTls"] = "false",
            ["TradeMind:Brokers:MetaTrader5:BridgeHost:AllowInsecureDemoTransport"] = "true",
            ["TradeMind:Brokers:MetaTrader5:BridgeHost:MaxConcurrentRequests"] = maxConcurrentRequests.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }));
    }
}
