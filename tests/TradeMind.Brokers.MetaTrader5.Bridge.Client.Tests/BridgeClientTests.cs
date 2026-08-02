using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Client;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Configuration;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Security;
using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;
using TradeMind.Brokers.MetaTrader5.Bridge;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Client.Tests;

public sealed class BridgeClientTests
{
    [Fact]
    public async Task Client_connects_handshakes_and_reaches_ready_state()
    {
        var handler = new QueueHandler(HandshakeResponse());
        await using var client = CreateClient(handler);
        await client.OpenAsync(CancellationToken.None);
        await client.AuthenticateAsync(CancellationToken.None);
        Assert.Equal(MT5BridgeClientState.Ready, client.State);
        Assert.Equal("simulation-host-1.0", client.BridgeVersion);
        Assert.Contains(handler.Requests, request => request.RequestUri?.AbsolutePath.EndsWith("/health/ready", StringComparison.Ordinal) == true);
        Assert.Contains(handler.Requests, request => request.RequestUri?.AbsolutePath.EndsWith("/handshake", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Client_propagates_correlation_and_session_metadata_in_handshake()
    {
        var handler = new QueueHandler(HandshakeResponse());
        await using var client = CreateClient(handler);
        await client.OpenAsync(CancellationToken.None);
        await client.AuthenticateAsync(CancellationToken.None);
        var body = handler.RequestBodies.Single(body => body.Contains("adapterVersion", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("correlationId", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("executionSessionId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Client_sends_versioned_query_and_reuses_idempotency_metadata_for_safe_retry()
    {
        var handler = new QueueHandler(HandshakeResponse(), new HttpRequestException("transient"), EnvelopeResponse());
        await using var client = CreateClient(handler, retries: 1);
        await client.OpenAsync(CancellationToken.None);
        await client.AuthenticateAsync(CancellationToken.None);
        var result = await client.SendAsync(WireRequest("get-instrument"), CancellationToken.None);
        Assert.Contains("success", result, StringComparison.OrdinalIgnoreCase);
        var executeRequests = handler.Requests.Where(request => request.RequestUri?.AbsolutePath.EndsWith("/execute", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(2, executeRequests.Length);
        Assert.Equal(handler.RequestBodies[^2], handler.RequestBodies[^1]);
    }

    [Fact]
    public async Task Client_does_not_retry_submit_order_automatically()
    {
        var handler = new QueueHandler(HandshakeResponse(), new HttpRequestException("transport"), new HttpRequestException("transport"));
        await using var client = CreateClient(handler, retries: 3);
        await client.OpenAsync(CancellationToken.None);
        await client.AuthenticateAsync(CancellationToken.None);
        await Assert.ThrowsAsync<MT5BridgeException>(() => client.SendAsync(WireRequest("submit-order"), CancellationToken.None));
        Assert.Single(handler.Requests, request => request.RequestUri?.AbsolutePath.EndsWith("/execute", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Client_distinguishes_external_cancellation_from_timeout()
    {
        var handler = new QueueHandler(HandshakeResponse(), delayResponse: true);
        await using var client = CreateClient(handler, timeoutSeconds: 30);
        await client.OpenAsync(CancellationToken.None);
        await client.AuthenticateAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendAsync(WireRequest("get-orders"), cancellation.Token));
        Assert.IsNotType<MT5BridgeException>(exception);
        Assert.Equal(MT5BridgeClientState.Degraded, client.State);
    }

    [Fact]
    public async Task Client_opens_circuit_after_repeated_transport_failures()
    {
        var handler = new QueueHandler(HandshakeResponse(), new HttpRequestException("one"), new HttpRequestException("two"));
        await using var client = CreateClient(handler, retries: 0, threshold: 1);
        await client.OpenAsync(CancellationToken.None);
        await client.AuthenticateAsync(CancellationToken.None);
        await Assert.ThrowsAsync<MT5BridgeException>(() => client.SendAsync(WireRequest("get-orders"), CancellationToken.None));
        await Assert.ThrowsAsync<MT5BridgeException>(() => client.SendAsync(WireRequest("get-orders"), CancellationToken.None));
        Assert.Equal(1, handler.Requests.Count(request => request.RequestUri?.AbsolutePath.EndsWith("/execute", StringComparison.Ordinal) == true));
    }

    [Fact]
    public async Task Client_heartbeat_returns_host_health_and_updates_ready_state()
    {
        var handler = new QueueHandler(HandshakeResponse(), HealthResponse());
        await using var client = CreateClient(handler);
        await client.OpenAsync(CancellationToken.None);
        await client.AuthenticateAsync(CancellationToken.None);
        var health = await client.GetHealthAsync(CancellationToken.None);
        Assert.Equal("simulation-host-1.0", health.BridgeVersion);
        Assert.True(health.DemoOnly);
        await client.HeartbeatAsync(CancellationToken.None);
        Assert.Equal(MT5BridgeClientState.Ready, client.State);
    }

    [Fact]
    public void Options_reject_plaintext_live_and_missing_authentication()
    {
        var validator = new MT5BridgeClientOptionsValidator();
        var result = validator.Validate(null, new MT5BridgeClientOptions { Endpoint = "http://localhost", RequireTls = true });
        Assert.False(result.Succeeded);
        result = validator.Validate(null, new MT5BridgeClientOptions { AllowLive = true });
        Assert.False(result.Succeeded);
        result = validator.Validate(null, new MT5BridgeClientOptions { AuthenticationMode = BridgeAuthenticationMode.SignedServiceToken, ServiceToken = null });
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Authentication_does_not_log_or_expose_a_certificate_value()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost");
        new MutualTlsBridgeAuthenticator().Apply(request);
        Assert.Equal("present", request.Headers.GetValues("X-Bridge-Client-Certificate").Single());
        Assert.DoesNotContain("password", string.Join(" ", request.Headers.SelectMany(header => header.Value)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Client_close_transitions_to_closed_and_rejects_later_operations()
    {
        var handler = new QueueHandler(HandshakeResponse());
        await using var client = CreateClient(handler);
        await client.OpenAsync(CancellationToken.None);
        await client.CloseAsync(CancellationToken.None);
        Assert.Equal(MT5BridgeClientState.Closed, client.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.OpenAsync(CancellationToken.None));
    }

    private static MT5BridgeClient CreateClient(QueueHandler handler, int retries = 0, int threshold = 3, int timeoutSeconds = 2)
    {
        var options = new MT5BridgeClientOptions { Endpoint = "https://localhost:7443", MaxTransportRetries = retries, CircuitBreakerFailureThreshold = threshold, RequestTimeoutSeconds = timeoutSeconds, HandshakeTimeoutSeconds = timeoutSeconds };
        return new(new HttpClient(handler) { BaseAddress = new Uri(options.Endpoint) }, Options.Create(options), new MutualTlsBridgeAuthenticator(), new NoopTelemetry(), TimeProvider.System, NullLogger<MT5BridgeClient>.Instance);
    }

    private static string WireRequest(string command) => JsonSerializer.Serialize(new { command, fields = new Dictionary<string, string> { ["account_id"] = "mt5-demo-account", ["client_order_id"] = "client-1", ["instrument"] = "EURUSD", ["side"] = "Buy", ["order_type"] = command == "submit-order" ? "Market" : "Market", ["quantity"] = "1", ["time_in_force"] = "Day" } }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static BridgeHandshakeResponse HandshakeResponse() => new(BridgeProtocolVersion.Current, ResponseMetadata(), "simulation-host-1.0", "1.0.0", true, "Demo", "Demo", "Ready", "simulation-terminal-1.0", DateTimeOffset.UtcNow, 0, [new BridgeCapability("orders", "1")], null);
    private static BridgeHealthResponse HealthResponse() => new(ResponseMetadata(), "Ready", "Ready", BridgeProtocolVersion.Current, "simulation-host-1.0", true, TimeSpan.FromSeconds(1), 0, 1, DateTimeOffset.UtcNow, null);
    private static BridgeTransportResponse EnvelopeResponse() => new(BridgeProtocolVersion.Current, ResponseMetadata(), "{\"success\":true,\"code\":\"OK\",\"message\":\"ok\",\"fields\":{}}", null);
    private static BridgeResponseMetadata ResponseMetadata() => new("req-1", "corr-1", "session-1", DateTimeOffset.UtcNow);

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<object> queue;
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> RequestBodies { get; } = [];
        public bool DelayResponse { get; }
        public QueueHandler(params object[] responses) : this(responses, false) { }
        public QueueHandler(BridgeHandshakeResponse handshake, bool delayResponse) : this(new object[] { handshake }, delayResponse) { }
        public QueueHandler(object[] responses, bool delayResponse)
        {
            queue = new Queue<object>(responses);
            DelayResponse = delayResponse;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content is not null) RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            if (DelayResponse && request.RequestUri?.AbsolutePath.EndsWith("/execute", StringComparison.Ordinal) == true) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            if (request.RequestUri?.AbsolutePath.EndsWith("/health/ready", StringComparison.Ordinal) == true) return new HttpResponseMessage(HttpStatusCode.OK);
            var response = queue.Count == 0 ? EnvelopeResponse() : queue.Dequeue();
            if (response is Exception exception) throw exception;
            if (response is BridgeHandshakeResponse handshake) return JsonResponse(handshake);
            if (response is BridgeHealthResponse health) return JsonResponse(health);
            if (response is BridgeTransportResponse envelope) return JsonResponse(envelope);
            throw new InvalidOperationException("Unsupported fake response.");
        }

        private static HttpResponseMessage JsonResponse<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value, options: new(JsonSerializerDefaults.Web)) };
    }

    private sealed class NoopTelemetry : ITradeMindTelemetry
    {
        public ITradeMindActivity StartActivity(TelemetryOperation operation, TelemetryContext? context = null, IReadOnlyCollection<TelemetryLink>? links = null) => new NoopActivity();
        public void EnrichCurrent(TelemetryContext context) { }
    }

    private sealed class NoopActivity : ITradeMindActivity
    {
        public bool IsRecording => false;
        public string? TraceId => null;
        public string? SpanId => null;
        public void Dispose() { }
        public void SetTag(string name, string? value) { }
        public void SetTag(string name, long? value) { }
        public void SetOutcome(TelemetryOutcome outcome) { }
        public void RecordException(Exception exception) { }
    }
}
