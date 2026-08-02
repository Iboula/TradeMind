using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Configuration;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Security;
using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;
using TradeMind.Brokers.MetaTrader5.Bridge;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Telemetry;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Client.Client;

public sealed class MT5BridgeClient(
    HttpClient httpClient,
    IOptions<MT5BridgeClientOptions> options,
    IBridgeAuthenticator authenticator,
    ITradeMindTelemetry telemetry,
    TimeProvider timeProvider,
    ILogger<MT5BridgeClient> logger,
    ITradeMindMetrics? bridgeMetrics = null) : IMT5Bridge, IMT5BridgeClientHealth, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MT5BridgeClientOptions configuration = options.Value;
    private readonly ITradeMindMetrics metrics = bridgeMetrics ?? NullBridgeMetrics.Instance;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim requestGate = new(options.Value.MaxConcurrentRequests, options.Value.MaxConcurrentRequests);
    private MT5BridgeClientState state = MT5BridgeClientState.Disconnected;
    private BridgeProtocolVersion? negotiatedProtocolVersion;
    private string? bridgeVersion;
    private string? terminalState;
    private DateTimeOffset? lastSuccessfulHandshakeUtc;
    private DateTimeOffset? lastHeartbeatUtc;
    private int reconnectCount;
    private int consecutiveFailures;
    private DateTimeOffset? circuitOpenUntilUtc;

    public string BridgeVersion => bridgeVersion ?? "external";
    public MT5BridgeClientState State => state;

    public async Task OpenAsync(CancellationToken cancellationToken)
    {
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (state == MT5BridgeClientState.Closed) throw new ObjectDisposedException(nameof(MT5BridgeClient));
            if (state is MT5BridgeClientState.Ready or MT5BridgeClientState.Handshaking) return;
            state = state == MT5BridgeClientState.Degraded ? MT5BridgeClientState.Reconnecting : MT5BridgeClientState.Connecting;
            if (state == MT5BridgeClientState.Reconnecting) reconnectCount++;
            using var activity = StartActivity("Connect");
            using var request = new HttpRequestMessage(HttpMethod.Get, "health/ready");
            authenticator.Apply(request);
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                state = MT5BridgeClientState.Degraded;
                throw new MT5BridgeException(new(BridgeErrorCode.BridgeUnavailable, "The bridge host is not ready.", true, true));
            }
            activity.SetOutcome(TelemetryOutcome.Succeeded);
            metrics.IncrementCounter(TelemetryMetricNames.Mt5Connections, 1, Dimensions("Connect"));
        }
        catch (OperationCanceledException) { state = MT5BridgeClientState.Degraded; throw; }
        catch { state = MT5BridgeClientState.Degraded; metrics.IncrementCounter(TelemetryMetricNames.Mt5Failures, 1, Dimensions("Connect", "Failed")); throw; }
        finally { lifecycleGate.Release(); }
    }

    public async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (state == MT5BridgeClientState.Closed) throw new ObjectDisposedException(nameof(MT5BridgeClient));
            state = MT5BridgeClientState.Handshaking;
            using var activity = StartActivity("Handshake");
            var metadata = CreateMetadata("handshake", "handshake");
            var request = new BridgeHandshakeRequest(
                ParseProtocolVersion(),
                configuration.AdapterVersion,
                true,
                "Demo",
                "Demo",
                timeProvider.GetUtcNow(),
                [new BridgeCapability("orders", "1"), new BridgeCapability("positions", "1")],
                metadata);
            var result = await PostAsync<BridgeHandshakeRequest, BridgeHandshakeResponse>("bridge/v1/handshake", request, "Handshake", configuration.HandshakeTimeoutSeconds, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded || result.Error is not null)
            {
                state = MT5BridgeClientState.Faulted;
                throw new MT5BridgeException(result.Error ?? new(BridgeErrorCode.ProtocolFailure, "Bridge handshake failed.", false, false));
            }
            negotiatedProtocolVersion = result.ProtocolVersion;
            bridgeVersion = result.BridgeVersion;
            terminalState = result.TerminalState;
            lastSuccessfulHandshakeUtc = result.ServerTimeUtc;
            lastHeartbeatUtc = result.ServerTimeUtc;
            consecutiveFailures = 0;
            circuitOpenUntilUtc = null;
            state = MT5BridgeClientState.Ready;
            activity.SetOutcome(TelemetryOutcome.Succeeded);
        }
        catch (OperationCanceledException) { state = MT5BridgeClientState.Degraded; throw; }
        catch { if (state != MT5BridgeClientState.Faulted) state = MT5BridgeClientState.Degraded; metrics.IncrementCounter(TelemetryMetricNames.Mt5Failures, 1, Dimensions("Handshake", "Failed")); throw; }
        finally { lifecycleGate.Release(); }
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            state = MT5BridgeClientState.Closed;
            using var activity = StartActivity("Disconnect");
            activity.SetOutcome(TelemetryOutcome.Succeeded);
        }
        finally { lifecycleGate.Release(); }
    }

    public async Task<string> SendAsync(string payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("A payload is required.", nameof(payload));
        EnsureReady();
        var command = ReadCommand(payload);
        var safeToRetry = command is not ("submit-order" or "modify-order" or "cancel-order" or "close-position");
        var metadata = CreateMetadata(command, payload);
        var transportRequest = new BridgeTransportRequest(ParseProtocolVersion(), ToOperation(command), metadata, payload);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                EnsureCircuitClosed();
                var result = await PostEnvelopeAsync(transportRequest, cancellationToken).ConfigureAwait(false);
                consecutiveFailures = 0;
                circuitOpenUntilUtc = null;
                lastHeartbeatUtc = timeProvider.GetUtcNow();
                state = MT5BridgeClientState.Ready;
                if (result.Error is not null) throw new MT5BridgeException(result.Error);
                return result.PayloadJson;
            }
            catch (MT5BridgeException exception) when ((exception.Error.Code is BridgeErrorCode.Timeout or BridgeErrorCode.TransportFailure or BridgeErrorCode.BridgeUnavailable) && safeToRetry && attempt < configuration.MaxTransportRetries)
            {
                RegisterFailure();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { state = MT5BridgeClientState.Degraded; throw; }
            catch
            {
                RegisterFailure();
                throw;
            }
        }
    }

    public async Task<MT5BridgeClientHealth> GetHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var metadata = CreateMetadata("health", "health");
            var request = new BridgeHealthRequest(metadata);
            var result = await PostAsync<BridgeHealthRequest, BridgeHealthResponse>("bridge/v1/health", request, "Health", configuration.RequestTimeoutSeconds, cancellationToken).ConfigureAwait(false);
            if (result.Error is not null) return new(state, result.ProtocolVersion.ToString(), result.BridgeVersion, result.TerminalState, result.DemoOnly, result.HeartbeatAge, result.ReconnectCount, result.LatencyMilliseconds, result.LastSuccessfulHandshakeUtc, result.Error);
            return new(state, result.ProtocolVersion.ToString(), result.BridgeVersion, result.TerminalState, result.DemoOnly, result.HeartbeatAge, result.ReconnectCount, result.LatencyMilliseconds, result.LastSuccessfulHandshakeUtc, null);
        }
        catch (MT5BridgeException exception)
        {
            state = MT5BridgeClientState.Degraded;
            return new(state, negotiatedProtocolVersion?.ToString(), bridgeVersion, terminalState, true, HeartbeatAge(), reconnectCount, 0, lastSuccessfulHandshakeUtc, exception.Error);
        }
    }

    public async Task HeartbeatAsync(CancellationToken cancellationToken)
    {
        var health = await GetHealthAsync(cancellationToken).ConfigureAwait(false);
        if (health.Error is not null) { state = MT5BridgeClientState.Degraded; throw new MT5BridgeException(health.Error); }
        lastHeartbeatUtc = timeProvider.GetUtcNow();
        state = MT5BridgeClientState.Ready;
        metrics.IncrementCounter(TelemetryMetricNames.Mt5Heartbeats, 1, Dimensions("Heartbeat"));
    }

    public async ValueTask DisposeAsync()
    {
        if (state != MT5BridgeClientState.Closed)
        {
            try { await CloseAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (OperationCanceledException) { state = MT5BridgeClientState.Closed; }
        }
        lifecycleGate.Dispose();
        requestGate.Dispose();
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest payload, string operation, int timeoutSeconds, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload, options: JsonOptions) };
        authenticator.Apply(request);
        await requestGate.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            var started = Stopwatch.GetTimestamp();
            metrics.IncrementCounter(TelemetryMetricNames.Mt5Requests, 1, Dimensions(operation));
            using var response = await httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
            var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, timeout.Token).ConfigureAwait(false);
            if (result is null) throw new MT5BridgeException(new(BridgeErrorCode.ProtocolFailure, "The bridge returned an empty response.", false, false));
            if (!response.IsSuccessStatusCode && result is BridgeHandshakeResponse handshake && handshake.Error is null) throw new MT5BridgeException(new(BridgeErrorCode.BridgeUnavailable, "The bridge request failed.", true, true));
            metrics.RecordDuration(TelemetryMetricNames.Mt5Latency, Stopwatch.GetElapsedTime(started), Dimensions(operation));
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MT5BridgeException(new(BridgeErrorCode.Timeout, "The bridge request exceeded its deadline.", true, true));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("MT5 bridge transport failure: {FailureType}", exception.GetType().Name);
            metrics.IncrementCounter(TelemetryMetricNames.Mt5Failures, 1, Dimensions(operation, "Failed"));
            throw new MT5BridgeException(new(BridgeErrorCode.TransportFailure, "The bridge transport failed.", true, true));
        }
        catch (JsonException exception)
        {
            logger.LogWarning("MT5 bridge protocol failure: {FailureType}", exception.GetType().Name);
            metrics.IncrementCounter(TelemetryMetricNames.Mt5Failures, 1, Dimensions(operation, "Failed"));
            throw new MT5BridgeException(new(BridgeErrorCode.ProtocolFailure, "The bridge returned an invalid response.", false, false));
        }
        finally { requestGate.Release(); }
    }

    private async Task<BridgeTransportResponse> PostEnvelopeAsync(BridgeTransportRequest payload, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds));
        var operation = OperationName(payload.Operation);
        using var activity = StartActivity(operation);
        using var request = new HttpRequestMessage(HttpMethod.Post, "bridge/v1/execute") { Content = JsonContent.Create(payload, options: JsonOptions) };
        authenticator.Apply(request);
        await requestGate.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            var started = Stopwatch.GetTimestamp();
            metrics.IncrementCounter(TelemetryMetricNames.Mt5Requests, 1, Dimensions(operation));
            using var response = await httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
            var result = await response.Content.ReadFromJsonAsync<BridgeTransportResponse>(JsonOptions, timeout.Token).ConfigureAwait(false);
            if (result is null) throw new MT5BridgeException(new(BridgeErrorCode.ProtocolFailure, "The bridge returned an empty response.", false, false));
            activity.SetOutcome(result.Error is null ? TelemetryOutcome.Succeeded : TelemetryOutcome.Rejected);
            metrics.RecordDuration(TelemetryMetricNames.Mt5Latency, Stopwatch.GetElapsedTime(started), Dimensions(operation));
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MT5BridgeException(new(BridgeErrorCode.Timeout, "The bridge request exceeded its deadline.", true, true));
        }
        catch (HttpRequestException)
        {
            metrics.IncrementCounter(TelemetryMetricNames.Mt5Failures, 1, Dimensions(operation, "Failed"));
            throw new MT5BridgeException(new(BridgeErrorCode.TransportFailure, "The bridge transport failed.", true, true));
        }
        catch (JsonException)
        {
            metrics.IncrementCounter(TelemetryMetricNames.Mt5Failures, 1, Dimensions(operation, "Failed"));
            throw new MT5BridgeException(new(BridgeErrorCode.ProtocolFailure, "The bridge returned an invalid response.", false, false));
        }
        finally { requestGate.Release(); }
    }

    private BridgeRequestMetadata CreateMetadata(string operation, string input)
    {
        var now = timeProvider.GetUtcNow();
        var normalizedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
        return new($"req-{Guid.NewGuid():N}", $"corr-{Guid.NewGuid():N}", $"session-{Guid.NewGuid():N}", now, Guid.NewGuid().ToString("N"), Sha256($"mt5-bridge:{operation}:{normalizedHash}"), normalizedHash, now.AddSeconds(configuration.RequestTimeoutSeconds));
    }

    private BridgeProtocolVersion ParseProtocolVersion()
    {
        if (!BridgeProtocolVersionExtensions.TryParse(configuration.ProtocolVersion, out var version)) throw new InvalidOperationException("Bridge protocol version is invalid.");
        return version;
    }

    private void EnsureReady()
    {
        if (state != MT5BridgeClientState.Ready) throw new MT5BridgeException(new(BridgeErrorCode.BridgeUnavailable, "The bridge client is not ready.", true, true));
    }

    private void EnsureCircuitClosed()
    {
        if (circuitOpenUntilUtc is { } until && timeProvider.GetUtcNow() < until) throw new MT5BridgeException(new(BridgeErrorCode.BridgeUnavailable, "The bridge circuit is open.", true, true));
        if (circuitOpenUntilUtc is not null) state = MT5BridgeClientState.Reconnecting;
    }

    private void RegisterFailure()
    {
        consecutiveFailures++;
        state = MT5BridgeClientState.Degraded;
        if (consecutiveFailures >= configuration.CircuitBreakerFailureThreshold) circuitOpenUntilUtc = timeProvider.GetUtcNow().AddSeconds(configuration.CircuitBreakerOpenSeconds);
    }

    private TimeSpan HeartbeatAge() => lastHeartbeatUtc is { } last ? timeProvider.GetUtcNow() - last : TimeSpan.MaxValue;
    private MetricDimensions Dimensions(string operation, string? outcome = null) => new(Module: "MT5Bridge", Operation: operation, Stage: "Broker", Connector: "mt5", Mode: "Demo", Outcome: outcome);
    private static string ReadCommand(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("command").GetString() ?? throw new InvalidOperationException("A command is required.");
    }

    private static BridgeOperation ToOperation(string command) => command switch
    {
        "get-accounts" => BridgeOperation.Account,
        "get-instrument" => BridgeOperation.Instrument,
        "submit-order" => BridgeOperation.SubmitOrder,
        "modify-order" => BridgeOperation.ModifyOrder,
        "cancel-order" => BridgeOperation.CancelOrder,
        "close-position" => BridgeOperation.ClosePosition,
        "get-orders" => BridgeOperation.Orders,
        "get-positions" => BridgeOperation.Positions,
        "heartbeat" => BridgeOperation.Heartbeat,
        _ => throw new InvalidOperationException("Unsupported bridge command.")
    };

    private static string OperationName(BridgeOperation operation) => operation.ToString();
    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private ITradeMindActivity StartActivity(string operation) => telemetry.StartActivity(new TelemetryOperation($"TradeMind.MT5Bridge.{operation}", "TradeMind.MT5Bridge", TelemetryStage.Broker), TelemetryContext.System() with { Module = "TradeMind.MT5Bridge", Operation = operation });
}
