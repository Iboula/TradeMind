using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Configuration;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Runtime;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Security;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Telemetry;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Application;

internal sealed class BridgeHostApplication(
    MT5BridgeHostRuntime runtime,
    IMT5TerminalGateway gateway,
    BridgeSecurityValidator security,
    IBridgeIdempotencyStore idempotency,
    IOptions<MT5BridgeHostOptions> options,
    TimeProvider timeProvider,
    ITradeMindMetrics? bridgeMetrics = null)
{
    private readonly MT5BridgeHostOptions configuration = options.Value;
    private readonly ITradeMindMetrics metrics = bridgeMetrics ?? NullBridgeMetrics.Instance;
    private readonly SemaphoreSlim concurrencyGate = new(options.Value.MaxConcurrentRequests, options.Value.MaxConcurrentRequests);
    private readonly DateTimeOffset startupUtc = timeProvider.GetUtcNow();
    private DateTimeOffset? lastHandshakeUtc;

    public BridgeHostHealthSnapshot HealthSnapshot
    {
        get
        {
            var now = timeProvider.GetUtcNow();
            var terminal = gateway.Snapshot;
            return new(runtime.State, terminal.ConnectionState.ToString(), BridgeHostProtocolVersion.TryParse(configuration.ProtocolVersion, out var version) ? version : BridgeProtocolVersion.Current, configuration.BridgeVersion, configuration.DemoOnly, lastHandshakeUtc, terminal.ReconnectCount, terminal.Latency, now - startupUtc);
        }
    }

    public Task<IResult> HealthAsync(HttpContext context, BridgeHealthRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        metrics.IncrementCounter(TelemetryMetricNames.Mt5Heartbeats, 1, Dimensions("Health"));
        var snapshot = HealthSnapshot;
        var metadata = new BridgeResponseMetadata(request.Metadata.RequestId, request.Metadata.CorrelationId, request.Metadata.ExecutionSessionId, timeProvider.GetUtcNow());
        var error = snapshot.HostState is MT5BridgeHostState.Ready or MT5BridgeHostState.Degraded ? null : new BridgeError(BridgeErrorCode.TerminalNotReady, "The bridge host is not ready.", true, true);
        return Task.FromResult<IResult>(Results.Ok(new BridgeHealthResponse(metadata, snapshot.HostState.ToString(), snapshot.TerminalState, snapshot.ProtocolVersion, snapshot.BridgeVersion, snapshot.DemoOnly, snapshot.Uptime, snapshot.ReconnectCount, snapshot.Latency.TotalMilliseconds, snapshot.LastSuccessfulHandshakeUtc, error)));
    }

    public async Task<IResult> HandshakeAsync(HttpContext context, BridgeHandshakeRequest request, CancellationToken cancellationToken)
    {
        metrics.IncrementCounter(TelemetryMetricNames.Mt5Requests, 1, Dimensions("Handshake"));
        var metadata = new BridgeResponseMetadata(request.Metadata.RequestId, request.Metadata.CorrelationId, request.Metadata.ExecutionSessionId, timeProvider.GetUtcNow());
        var protocol = BridgeHostProtocolVersion.TryParse(configuration.ProtocolVersion, out var current) ? current : BridgeProtocolVersion.Current;
        if (!runtime.IsReady) return Results.Json(new BridgeHandshakeResponse(protocol, metadata, configuration.BridgeVersion, configuration.AdapterVersion, true, configuration.Environment, configuration.AccountEnvironment, "NotReady", gateway.TerminalVersion, timeProvider.GetUtcNow(), 0, [], new(BridgeErrorCode.TerminalNotReady, "The bridge host is not ready.", true, true)), statusCode: StatusCodes.Status503ServiceUnavailable);
        if (!HasAuthentication(context)) return Results.Json(new BridgeHandshakeResponse(protocol, metadata, configuration.BridgeVersion, configuration.AdapterVersion, true, configuration.Environment, configuration.AccountEnvironment, "Ready", gateway.TerminalVersion, timeProvider.GetUtcNow(), 0, [], new(BridgeErrorCode.AuthenticationFailed, "Bridge authentication is required.", false, false)), statusCode: StatusCodes.Status401Unauthorized);
        var now = timeProvider.GetUtcNow();
        var skew = Math.Abs((now - request.ClientTimeUtc).TotalSeconds);
        if (!request.ProtocolVersion.IsCompatibleWith(protocol)) return Results.Json(new BridgeHandshakeResponse(protocol, metadata, configuration.BridgeVersion, configuration.AdapterVersion, true, configuration.Environment, configuration.AccountEnvironment, "Ready", gateway.TerminalVersion, now, skew, [], new(BridgeErrorCode.ProtocolVersionMismatch, "The requested protocol version is incompatible.", false, false)), statusCode: StatusCodes.Status400BadRequest);
        if (!request.DemoOnly || !configuration.DemoOnly || request.AccountEnvironment.Equals("Live", StringComparison.OrdinalIgnoreCase) || configuration.AccountEnvironment.Equals("Live", StringComparison.OrdinalIgnoreCase)) return Results.Json(new BridgeHandshakeResponse(protocol, metadata, configuration.BridgeVersion, configuration.AdapterVersion, true, configuration.Environment, configuration.AccountEnvironment, "Ready", gateway.TerminalVersion, now, skew, [], new(BridgeErrorCode.LiveModeForbidden, "Only demo environments are supported.", false, false)), statusCode: StatusCodes.Status403Forbidden);
        if (skew > configuration.MaximumClockSkewSeconds) return Results.Json(new BridgeHandshakeResponse(protocol, metadata, configuration.BridgeVersion, configuration.AdapterVersion, true, configuration.Environment, configuration.AccountEnvironment, "Ready", gateway.TerminalVersion, now, skew, [], new(BridgeErrorCode.ClockSkewExceeded, "The handshake clock skew is too large.", false, false)), statusCode: StatusCodes.Status400BadRequest);
        var capabilities = new[] { new BridgeCapability("orders", "1"), new BridgeCapability("positions", "1"), new BridgeCapability("health", "1") };
        var missing = request.RequiredCapabilities.Where(required => !capabilities.Any(capability => capability.Name.Equals(required.Name, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (missing.Length > 0) return Results.Json(new BridgeHandshakeResponse(protocol, metadata, configuration.BridgeVersion, configuration.AdapterVersion, true, configuration.Environment, configuration.AccountEnvironment, "Ready", gateway.TerminalVersion, now, skew, capabilities, new(BridgeErrorCode.InvalidRequest, "A required bridge capability is unavailable.", false, false)), statusCode: StatusCodes.Status400BadRequest);
        lastHandshakeUtc = now;
        return Results.Ok(new BridgeHandshakeResponse(protocol, metadata, configuration.BridgeVersion, request.AdapterVersion, true, configuration.Environment, configuration.AccountEnvironment, "Ready", gateway.TerminalVersion, now, skew, capabilities, null));
    }

    public async Task<IResult> ExecuteAsync(HttpContext context, BridgeTransportRequest request, CancellationToken cancellationToken)
    {
        var operationName = request.Operation.ToString();
        metrics.IncrementCounter(TelemetryMetricNames.Mt5Requests, 1, Dimensions(operationName));
        var now = timeProvider.GetUtcNow();
        var metadata = new BridgeResponseMetadata(request.Metadata.RequestId, request.Metadata.CorrelationId, request.Metadata.ExecutionSessionId, now);
        var validationError = security.ValidateTransport(context, request, now);
        if (validationError is not null) return Results.Json(new BridgeTransportResponse(request.ProtocolVersion, metadata, "{}", validationError), statusCode: StatusCodes.Status400BadRequest);
        if (!runtime.IsReady) return Results.Json(new BridgeTransportResponse(request.ProtocolVersion, metadata, "{}", new(BridgeErrorCode.TerminalNotReady, "The bridge host is not ready.", true, true)), statusCode: StatusCodes.Status503ServiceUnavailable);
        if (idempotency.TryGet(request.Metadata.IdempotencyKeyHash, out var previous))
        {
            if (previous.RequestHash.Equals(request.Metadata.NormalizedRequestHash, StringComparison.OrdinalIgnoreCase)) return Results.Ok(previous.Response);
            return Results.Json(new BridgeTransportResponse(request.ProtocolVersion, metadata, "{}", new(BridgeErrorCode.Conflict, "The idempotency key was reused with a different request.", false, false)), statusCode: StatusCodes.Status409Conflict);
        }
        var replay = security.ReserveNonce(request.Metadata, now);
        if (replay == ReplayReservation.Conflict) return Results.Json(new BridgeTransportResponse(request.ProtocolVersion, metadata, "{}", new(BridgeErrorCode.ReplayDetected, "The request nonce has already been used.", false, false)), statusCode: StatusCodes.Status409Conflict);
        if (replay == ReplayReservation.SameRequest) return Results.Json(new BridgeTransportResponse(request.ProtocolVersion, metadata, "{}", new(BridgeErrorCode.DuplicateRequest, "The request nonce has already been processed.", true, true)), statusCode: StatusCodes.Status409Conflict);
        if (!Enum.IsDefined(request.Operation)) return Results.Json(new BridgeTransportResponse(request.ProtocolVersion, metadata, "{}", new(BridgeErrorCode.InvalidRequest, "The bridge operation is invalid.", false, false)), statusCode: StatusCodes.Status400BadRequest);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var remaining = request.Metadata.DeadlineUtc - timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero) return Results.Json(new BridgeTransportResponse(request.ProtocolVersion, metadata, "{}", new(BridgeErrorCode.Timeout, "The bridge deadline expired.", true, true)), statusCode: StatusCodes.Status408RequestTimeout);
        deadline.CancelAfter(remaining);
        var acquired = false;
        try
        {
            await concurrencyGate.WaitAsync(deadline.Token).ConfigureAwait(false);
            acquired = true;
            var command = ParseCommand(request.PayloadJson);
            if (command.Fields.TryGetValue("mode", out var mode) && mode.Equals("Live", StringComparison.OrdinalIgnoreCase))
            {
                var liveResponse = new BridgeTransportResponse(request.ProtocolVersion, metadata, "{}", new(BridgeErrorCode.LiveModeForbidden, "Live execution is disabled by the bridge host.", false, false));
                idempotency.Store(request.Metadata.IdempotencyKeyHash, new(request.Metadata.NormalizedRequestHash, liveResponse));
                return Results.Json(liveResponse, statusCode: StatusCodes.Status403Forbidden);
            }
            var result = await gateway.ExecuteAsync(command, deadline.Token).ConfigureAwait(false);
            var response = new BridgeTransportResponse(request.ProtocolVersion, metadata, SerializeTerminalResponse(result), result.Success || IsTerminalResultCode(result.Code) ? null : MapError(result.Code));
            idempotency.Store(request.Metadata.IdempotencyKeyHash, new(request.Metadata.NormalizedRequestHash, response));
            if (!result.Success) metrics.IncrementCounter(TelemetryMetricNames.Mt5Failures, 1, Dimensions(operationName, "Rejected"));
            return Results.Ok(response);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Json(new BridgeTransportResponse(request.ProtocolVersion, metadata, "{}", new(BridgeErrorCode.Timeout, "The bridge deadline expired.", true, true)), statusCode: StatusCodes.Status408RequestTimeout);
        }
        catch (OperationCanceledException) { return Results.Json(new BridgeTransportResponse(request.ProtocolVersion, metadata, "{}", new(BridgeErrorCode.Cancelled, "The bridge request was cancelled.", false, false)), statusCode: StatusCodes.Status400BadRequest); }
        catch (JsonException) { return Results.Json(new BridgeTransportResponse(request.ProtocolVersion, metadata, "{}", new(BridgeErrorCode.InvalidRequest, "The bridge payload is invalid.", false, false)), statusCode: StatusCodes.Status400BadRequest); }
        catch (InvalidOperationException) { return Results.Json(new BridgeTransportResponse(request.ProtocolVersion, metadata, "{}", new(BridgeErrorCode.InvalidRequest, "The bridge command is invalid.", false, false)), statusCode: StatusCodes.Status400BadRequest); }
        catch (Exception)
        {
            metrics.IncrementCounter(TelemetryMetricNames.Mt5Failures, 1, Dimensions(operationName, "Failed"));
            return Results.Json(new BridgeTransportResponse(request.ProtocolVersion, metadata, "{}", new(BridgeErrorCode.Unknown, "The bridge operation failed.", false, false)), statusCode: StatusCodes.Status500InternalServerError);
        }
        finally { if (acquired) concurrencyGate.Release(); }
    }

    private bool HasAuthentication(HttpContext context) => configuration.AuthenticationMode.Equals("MutualTls", StringComparison.OrdinalIgnoreCase)
        ? context.Request.Headers.TryGetValue("X-Bridge-Client-Certificate", out var certificate) && certificate.Count > 0
        : context.Request.Headers.Authorization.Count > 0;

    private static TerminalCommand ParseCommand(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var command = root.GetProperty("command").GetString() ?? throw new InvalidOperationException("A command is required.");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("fields", out var fieldElement)) foreach (var property in fieldElement.EnumerateObject()) fields[property.Name] = property.Value.GetString() ?? string.Empty;
        return new(command, fields);
    }

    private static string SerializeTerminalResponse(TerminalCommandResult result) => JsonSerializer.Serialize(new { success = result.Success, code = result.Success ? result.Code : PublicTerminalCode(result.Code), message = result.Success ? result.Message : "The demo terminal rejected the requested operation.", fields = result.Fields }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static bool IsTerminalResultCode(string code) => code is "ORDER_REJECTED" or "INVALID_VOLUME" or "INVALID_STOPS" or "MARKET_CLOSED" or "INSUFFICIENT_MARGIN" or "NO_CONNECTION" or "DEMO_ONLY" or "DEMO_EXECUTION_REQUIRED" or "LIVE_MODE_FORBIDDEN" or "DEMO_CONFIRMATION_REQUIRED" or "EXECUTION_GUARDS_REQUIRED" or "DUPLICATE_EXECUTION" or "CLEANUP_FAILED" or "TIMEOUT" or "CANCELLED" or "INVALID_REQUEST" or "INVALID_QUANTITY" or "POSITION_LIMIT" or "POSITION_NOT_FOUND" or "ORDER_NOT_FOUND";
    private static string PublicTerminalCode(string code) => IsTerminalResultCode(code) ? code : "TERMINAL_FAILURE";
    private static BridgeError MapError(string code) => code switch
    {
        "TERMINAL_UNAVAILABLE" => new(BridgeErrorCode.TerminalUnavailable, "The external terminal is unavailable.", true, true),
        "ORDER_REJECTED" => new(BridgeErrorCode.TerminalRejected, "The demo terminal rejected the order.", false, false),
        _ => new(BridgeErrorCode.Unknown, "The bridge operation failed.", false, false)
    };

    private static MetricDimensions Dimensions(string operation, string? outcome = null) => new(Module: "MT5Bridge", Operation: operation, Stage: "Broker", Connector: "mt5", Mode: "Demo", Outcome: outcome);
}

internal sealed record BridgeHostHealthSnapshot(MT5BridgeHostState HostState, string TerminalState, BridgeProtocolVersion ProtocolVersion, string BridgeVersion, bool DemoOnly, DateTimeOffset? LastSuccessfulHandshakeUtc, int ReconnectCount, TimeSpan Latency, TimeSpan Uptime);
