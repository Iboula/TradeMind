using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Domain;
using TradeMind.Brokers.MetaTrader5.Configuration;
using TradeMind.Brokers.MetaTrader5.Connection;
using TradeMind.Brokers.MetaTrader5.Health;
using TradeMind.Brokers.MetaTrader5.Mapping;
using TradeMind.Brokers.MetaTrader5.Protocol;
using TradeMind.Brokers.MetaTrader5.Retry;
using TradeMind.Brokers.MetaTrader5.Serialization;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.MetaTrader5.Execution;

public sealed class MT5BrokerConnector(
    IOptions<MT5Options> options,
    IMT5ConnectionFactory connectionFactory,
    IMT5Protocol protocol,
    IMT5HealthService healthService,
    IMT5ReconnectPolicy reconnectPolicy,
    IMT5RetryPolicy retryPolicy,
    ITradeMindTelemetry telemetry,
    ITradeMindMetrics metrics,
    TimeProvider timeProvider,
    ILogger<MT5BrokerConnector> logger) : IBrokerConnector, IAsyncDisposable
{
    private readonly MT5Options configuration = options.Value;
    private readonly IMT5Connection connection = connectionFactory.Create();

    public BrokerConnectorDescriptor Descriptor { get; } = CreateDescriptor(options.Value);

    public async Task<BrokerHealthResult> GetHealthAsync(BrokerExecutionContext context, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("Health");
        var result = await healthService.CheckAsync(connection, cancellationToken).ConfigureAwait(false);
        var status = result.Healthy ? BrokerHealthStatus.Healthy : connection.State == MT5ConnectionState.Faulted ? BrokerHealthStatus.Unhealthy : BrokerHealthStatus.Degraded;
        var health = new BrokerHealth(Descriptor.ConnectorId, status, result.Message, timeProvider.GetUtcNow(), result.Snapshot.Latency);
        if (result.Snapshot.ConnectionState == MT5ConnectionState.Connected)
            metrics.IncrementCounter(TelemetryMetricNames.Mt5Connections, 1, Dimensions("Connect"));
        metrics.IncrementCounter(TelemetryMetricNames.Mt5Requests, 1, Dimensions("Heartbeat"));
        metrics.IncrementCounter(TelemetryMetricNames.Mt5Heartbeats, 1, Dimensions("Heartbeat"));
        activity.SetOutcome(result.Healthy ? TelemetryOutcome.Succeeded : TelemetryOutcome.Degraded);
        return new BrokerHealthResult(health, result.Error is null ? null : ToError(result.Error, context, "Health"));
    }

    public async Task<IReadOnlyList<BrokerAccount>> GetAccountsAsync(BrokerExecutionContext context, CancellationToken cancellationToken)
    {
        var response = await SendAsync("GetAccounts", "get-accounts", new Dictionary<string, string>(), cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, "GetAccounts");
        var payloads = MT5JsonSerializer.DeserializePayloads<MT5AccountPayload>(response.Fields["items"]);
        return payloads.Select(item => MT5AccountMapper.Map(item, Descriptor.ConnectorId, context.TenantId)).ToArray();
    }

    public async Task<BrokerInstrumentSpecification?> GetInstrumentAsync(BrokerExecutionContext context, BrokerInstrumentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var response = await SendAsync("GetInstrument", "get-instrument", new Dictionary<string, string> { ["instrument"] = query.Instrument }, cancellationToken).ConfigureAwait(false);
        if (!response.Success && response.Code == "INSTRUMENT_NOT_FOUND") return null;
        EnsureSuccess(response, "GetInstrument");
        return MT5InstrumentMapper.Map(MT5JsonSerializer.DeserializePayload<MT5InstrumentPayload>(response.Fields["instrument"]));
    }

    public async Task<BrokerOrderSubmissionResult> SubmitOrderAsync(BrokerExecutionContext context, BrokerOrderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (configuration.Mode.Equals(nameof(BrokerExecutionMode.Live), StringComparison.OrdinalIgnoreCase) || configuration.AllowLive)
            return new(null, null, Error("LIVE_UNSUPPORTED", BrokerErrorCategory.UnsupportedCapability, "Live MT5 execution is disabled.", context, request.ExecutionId));
        if (request.StopLoss is not null || request.TakeProfits.Count > 0)
            return new(null, null, Error("INVALID_STOPS", BrokerErrorCategory.Validation, "The demo smoke test does not support stop or target parameters.", context, request.ExecutionId));
        var fields = new Dictionary<string, string>
        {
            ["mode"] = configuration.Mode,
            ["account_id"] = request.AccountId.Value,
            ["client_order_id"] = request.ClientOrderId,
            ["instrument"] = request.Instrument,
            ["side"] = request.Side.ToString(),
            ["order_type"] = request.OrderType.ToString(),
            ["quantity"] = request.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["time_in_force"] = request.TimeInForce.ToString(),
            ["demo_confirmation"] = IsTrue(request.Metadata, "demo_confirmation") ? "true" : "false",
            ["risk_approved"] = !string.IsNullOrWhiteSpace(request.RiskAssessmentId) ? "true" : "false",
            ["trading_plan_valid"] = !string.IsNullOrWhiteSpace(request.TradingPlanId) ? "true" : "false",
            ["execution_session_valid"] = string.Equals(context.ExecutionSessionId, request.ExecutionSessionId, StringComparison.Ordinal) ? "true" : "false",
            ["permission"] = context.HasPermission(BrokerPermissionNames.ExecuteDemo) ? "true" : "false",
            ["capability"] = Descriptor.Capabilities.HasFlag(BrokerCapability.SubmitMarketOrders) ? "true" : "false",
            ["heartbeat_valid"] = "true"
        };
        if (request.RequestedPrice is { } requestedPrice) fields["requested_price"] = requestedPrice.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var response = await SendAsync("SubmitOrder", "submit-order", fields, cancellationToken).ConfigureAwait(false);
        if (!response.Success) return new(null, null, MT5ErrorMapper.FromResponse(response, context, timeProvider.GetUtcNow(), "SubmitOrder"));
        var order = MT5OrderMapper.Map(MT5JsonSerializer.DeserializePayload<MT5OrderPayload>(response.Fields["order"]), Descriptor.ConnectorId);
        var execution = response.Fields.TryGetValue("execution", out var executionPayload) ? MT5ExecutionMapper.Map(MT5JsonSerializer.DeserializePayload<MT5ExecutionPayload>(executionPayload), Descriptor.ConnectorId) : null;
        return new(order, execution, null);
    }

    public async Task<BrokerOrderModificationResult> ModifyOrderAsync(BrokerExecutionContext context, BrokerOrderModificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var fields = new Dictionary<string, string> { ["order_id"] = request.OrderId.Value };
        if (request.Quantity is { } quantity) fields["quantity"] = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (request.LimitPrice is { } price) fields["limit_price"] = price.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var response = await SendAsync("ModifyOrder", "modify-order", fields, cancellationToken).ConfigureAwait(false);
        if (!response.Success) return new(null, MT5ErrorMapper.FromResponse(response, context, timeProvider.GetUtcNow(), "ModifyOrder"));
        return new(MT5OrderMapper.Map(MT5JsonSerializer.DeserializePayload<MT5OrderPayload>(response.Fields["order"]), Descriptor.ConnectorId), null);
    }

    public async Task<BrokerOrderCancellationResult> CancelOrderAsync(BrokerExecutionContext context, BrokerOrderCancellationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await SendAsync("CancelOrder", "cancel-order", new Dictionary<string, string> { ["order_id"] = request.OrderId.Value }, cancellationToken).ConfigureAwait(false);
        if (!response.Success) return new(null, MT5ErrorMapper.FromResponse(response, context, timeProvider.GetUtcNow(), "CancelOrder"));
        return new(MT5OrderMapper.Map(MT5JsonSerializer.DeserializePayload<MT5OrderPayload>(response.Fields["order"]), Descriptor.ConnectorId), null);
    }

    public async Task<BrokerPositionCloseResult> ClosePositionAsync(BrokerExecutionContext context, BrokerPositionCloseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await SendAsync("ClosePosition", "close-position", new Dictionary<string, string> { ["position_id"] = request.PositionId.Value }, cancellationToken).ConfigureAwait(false);
        if (!response.Success) return new(null, null, MT5ErrorMapper.FromResponse(response, context, timeProvider.GetUtcNow(), "ClosePosition"));
        var position = MT5PositionMapper.Map(MT5JsonSerializer.DeserializePayload<MT5PositionPayload>(response.Fields["position"]), Descriptor.ConnectorId);
        var execution = MT5ExecutionMapper.Map(MT5JsonSerializer.DeserializePayload<MT5ExecutionPayload>(response.Fields["execution"]), Descriptor.ConnectorId);
        return new(position, execution, null);
    }

    public async Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(BrokerExecutionContext context, BrokerOrderQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var response = await SendAsync("GetOrders", "get-orders", new Dictionary<string, string>(), cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, "GetOrders");
        return MT5JsonSerializer.DeserializePayloads<MT5OrderPayload>(response.Fields["items"]).Select(item => MT5OrderMapper.Map(item, Descriptor.ConnectorId)).ToArray();
    }

    public async Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(BrokerExecutionContext context, BrokerPositionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var response = await SendAsync("GetPositions", "get-positions", new Dictionary<string, string>(), cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, "GetPositions");
        return MT5JsonSerializer.DeserializePayloads<MT5PositionPayload>(response.Fields["items"]).Select(item => MT5PositionMapper.Map(item, Descriptor.ConnectorId)).ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        using var activity = StartActivity("Disconnect");
        await connection.DisposeAsync().ConfigureAwait(false);
        activity.SetOutcome(TelemetryOutcome.Succeeded);
    }

    private async Task<MT5Response> SendAsync(string operation, string command, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        using var activity = StartActivity(operation);
        using var sendActivity = StartActivity("Send");
        var started = Stopwatch.GetTimestamp();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
                var response = await protocol.ExecuteAsync(connection, new MT5Request(command, fields), cancellationToken).ConfigureAwait(false);
                sendActivity.SetOutcome(TelemetryOutcome.Succeeded);
                using var receiveActivity = StartActivity("Receive");
                receiveActivity.SetOutcome(response.Success ? TelemetryOutcome.Succeeded : TelemetryOutcome.Rejected);
                metrics.IncrementCounter(TelemetryMetricNames.Mt5Requests, 1, Dimensions(operation));
                metrics.RecordDuration(TelemetryMetricNames.Mt5Latency, Stopwatch.GetElapsedTime(started), Dimensions(operation));
                activity.SetOutcome(response.Success ? TelemetryOutcome.Succeeded : TelemetryOutcome.Rejected);
                return response;
            }
            catch (OperationCanceledException)
            {
                activity.SetOutcome(TelemetryOutcome.Cancelled);
                throw;
            }
            catch (Exception exception) when (retryPolicy.ShouldRetry(attempt, exception))
            {
                metrics.IncrementCounter(TelemetryMetricNames.Mt5Failures, 1, Dimensions(operation));
                await Task.Delay(retryPolicy.GetDelay(attempt), timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "MT5 {Operation} failed.", operation);
                metrics.IncrementCounter(TelemetryMetricNames.Mt5Failures, 1, Dimensions(operation));
                activity.SetOutcome(TelemetryOutcome.Failed);
                throw;
            }
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (connection.State == MT5ConnectionState.Connected) return;
        using var activity = StartActivity("Connect");
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);
                metrics.IncrementCounter(TelemetryMetricNames.Mt5Connections, 1, Dimensions("Connect"));
                activity.SetOutcome(TelemetryOutcome.Succeeded);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) when (reconnectPolicy.ShouldReconnect(attempt, exception))
            {
                metrics.IncrementCounter(TelemetryMetricNames.Mt5Reconnects, 1, Dimensions("Reconnect"));
                await Task.Delay(reconnectPolicy.GetDelay(attempt), timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private ITradeMindActivity StartActivity(string operation) => telemetry.StartActivity(new TelemetryOperation($"TradeMind.MT5.{operation}", "TradeMind.MT5", TelemetryStage.Broker), TelemetryContext.System() with { Module = "TradeMind.MT5", Operation = operation });
    private MetricDimensions Dimensions(string operation) => new(Module: "MT5", Operation: operation, Stage: "Broker", Connector: Descriptor.ConnectorId.Value, Mode: Descriptor.Mode.ToString());
    private BrokerError ToError(Exception exception, BrokerExecutionContext context, string operation) => new("MT5_HEALTH_FAILED", BrokerErrorCategory.ConnectorUnavailable, $"The MT5 {operation} health check failed.", true, false, null, new BrokerTraceReference(context.CorrelationId, null, context.ExecutionSessionId), timeProvider.GetUtcNow());
    private BrokerError Error(string code, BrokerErrorCategory category, string message, BrokerExecutionContext context, BrokerExecutionId executionId) => new(code, category, message, false, false, null, new BrokerTraceReference(context.CorrelationId, null, context.ExecutionSessionId), timeProvider.GetUtcNow());
    private static void EnsureSuccess(MT5Response response, string operation)
    {
        if (!response.Success) throw new MT5ProtocolException(response.Code, $"The MT5 {operation} operation failed.");
    }

    private static bool IsTrue(IReadOnlyDictionary<string, string> values, string key) => values.TryGetValue(key, out var value) && value.Equals("true", StringComparison.OrdinalIgnoreCase);

    private static BrokerConnectorDescriptor CreateDescriptor(MT5Options options)
    {
        if (!Enum.TryParse<BrokerExecutionMode>(options.Mode, true, out var mode) || mode == BrokerExecutionMode.Live || options.AllowLive)
            throw new InvalidOperationException("Live MetaTrader 5 execution is disabled in Sprint 29.");
        var demo = mode == BrokerExecutionMode.Demo && options.AllowDemo;
        return new BrokerConnectorDescriptor(new("mt5"), new("metatrader5"), "MetaTrader 5 Adapter", "1.0.0", BrokerEnvironment.Test, mode,
            MT5CapabilityMapper.ToCapabilities(demo), [BrokerAssetClass.Forex], [BrokerOrderType.Market, BrokerOrderType.Limit, BrokerOrderType.Stop], supportsDemo: demo, supportsReconciliation: true, maximumConcurrentRequests: 1);
    }
}
