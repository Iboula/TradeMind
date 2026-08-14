using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.RiskEngine.Domain;
using TradeMind.AI.TradingDecisions.Domain;
using TradeMind.AI.TradingPlans.Domain;
using TradeMind.Brokers.Application;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Domain;
using TradeMind.Brokers.MetaTrader5.Bridge;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.Configuration;
using TradeMind.Brokers.MetaTrader5.Bridge.Client.DependencyInjection;
using TradeMind.Brokers.MetaTrader5.Configuration;
using TradeMind.Brokers.MetaTrader5.Connection;
using TradeMind.Brokers.MetaTrader5.Execution;
using TradeMind.Brokers.MetaTrader5.DependencyInjection;
using TradeMind.Brokers.MetaTrader5.Health;
using TradeMind.Brokers.MetaTrader5.Protocol;
using TradeMind.Brokers.MetaTrader5.Retry;
using TradeMind.Brokers.MetaTrader5.Serialization;
using TradeMind.Market.Abstractions;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.MetaTrader5.Tests;

public sealed class MT5AdapterTests
{
    [Fact]
    public async Task Connection_uses_explicit_state_machine_and_closes_deterministically()
    {
        var bridge = new TestBridge();
        await using var connection = CreateConnection(bridge);

        Assert.Equal(MT5ConnectionState.Disconnected, connection.State);
        await connection.ConnectAsync(CancellationToken.None);
        Assert.Equal(MT5ConnectionState.Connected, connection.State);
        await connection.DisconnectAsync(CancellationToken.None);
        Assert.Equal(MT5ConnectionState.Disconnected, connection.State);
        await connection.DisposeAsync();
        Assert.Equal(MT5ConnectionState.Closed, connection.State);
    }

    [Fact]
    public async Task Connection_reconnects_after_a_failed_connection()
    {
        var bridge = new TestBridge { FailFirstOpen = true };
        await using var connection = CreateConnection(bridge);

        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.ConnectAsync(CancellationToken.None));
        Assert.Equal(MT5ConnectionState.Faulted, connection.State);
        await connection.ConnectAsync(CancellationToken.None);

        Assert.Equal(MT5ConnectionState.Connected, connection.State);
        Assert.Equal(1, connection.Snapshot.ReconnectCount);
        Assert.NotNull(connection.Snapshot.LastReconnect);
    }

    [Fact]
    public async Task Connection_converts_bridge_cancellation_to_timeout()
    {
        var bridge = new TestBridge { BlockOpen = true };
        await using var connection = CreateConnection(bridge);

        await Assert.ThrowsAsync<TimeoutException>(() => connection.ConnectAsync(CancellationToken.None));
        Assert.Equal(MT5ConnectionState.Faulted, connection.State);
    }

    [Fact]
    public void Serializer_roundtrips_protocol_requests_and_responses_without_raw_packets()
    {
        var serializer = new MT5JsonSerializer();
        var request = new MT5Request("heartbeat", new Dictionary<string, string> { ["correlation"] = "corr-1" });
        var response = new MT5Response(true, "OK", "acknowledged", new Dictionary<string, string> { ["protocol_version"] = "1.0" });

        var requestRoundTrip = serializer.DeserializeRequest(serializer.SerializeRequest(request));
        var responseRoundTrip = serializer.DeserializeResponse(serializer.SerializeResponse(response));

        Assert.Equal(request.Command, requestRoundTrip.Command);
        Assert.Equal("corr-1", requestRoundTrip.Fields["correlation"]);
        Assert.Equal(response.Code, responseRoundTrip.Code);
        Assert.Equal("1.0", responseRoundTrip.Fields["protocol_version"]);
    }

    [Fact]
    public async Task Simulation_connector_maps_accounts_instruments_orders_positions_and_executions()
    {
        await using var provider = CreateProvider();
        var connector = provider.GetRequiredService<IBrokerConnector>();
        var context = Context();

        var accounts = await connector.GetAccountsAsync(context, CancellationToken.None);
        var instrument = await connector.GetInstrumentAsync(context, new BrokerInstrumentQuery("EURUSD"), CancellationToken.None);
        var submission = await connector.SubmitOrderAsync(context, Request(connector.Descriptor.ConnectorId, BrokerOrderType.Market, "client-market"), CancellationToken.None);
        var positions = await connector.GetPositionsAsync(context, new BrokerPositionQuery(), CancellationToken.None);

        Assert.Single(accounts);
        Assert.Equal("tenant-1", accounts[0].Metadata["tenant_id"]);
        Assert.NotNull(instrument);
        Assert.Equal(BrokerAssetClass.Forex, instrument!.AssetClass);
        Assert.Null(submission.Error);
        Assert.Equal(BrokerOrderStatus.Filled, submission.Order!.Status);
        Assert.Equal(BrokerOrderStatus.Filled, submission.Execution!.Status);
        Assert.Single(positions);
    }

    [Fact]
    public async Task Simulation_connector_supports_pending_order_lifecycle_and_close()
    {
        await using var provider = CreateProvider();
        var connector = provider.GetRequiredService<IBrokerConnector>();
        var context = Context();
        var submission = await connector.SubmitOrderAsync(context, Request(connector.Descriptor.ConnectorId, BrokerOrderType.Limit, "client-limit", 1.09m), CancellationToken.None);

        var modified = await connector.ModifyOrderAsync(context, new BrokerOrderModificationRequest(submission.Order!.OrderId, 2m, 1.08m, null, null), CancellationToken.None);
        var cancelled = await connector.CancelOrderAsync(context, new BrokerOrderCancellationRequest(modified.Order!.OrderId, "test"), CancellationToken.None);
        var market = await connector.SubmitOrderAsync(context, Request(connector.Descriptor.ConnectorId, BrokerOrderType.Market, "client-close"), CancellationToken.None);
        await connector.GetHealthAsync(context, CancellationToken.None);
        var closed = await connector.ClosePositionAsync(context, new BrokerPositionCloseRequest(market.Execution!.PositionId!, null), CancellationToken.None);

        Assert.Null(modified.Error);
        Assert.Equal(2m, modified.Order!.Quantity);
        Assert.Null(cancelled.Error);
        Assert.Equal(BrokerOrderStatus.Cancelled, cancelled.Order!.Status);
        Assert.Null(closed.Error);
        Assert.Equal(BrokerOrderStatus.Filled, closed.Execution!.Status);
    }

    [Fact]
    public async Task Close_position_sends_explicit_demo_safety_guards()
    {
        var options = new MT5Options { Mode = "Demo", AllowDemo = true, AllowLive = false };
        var protocol = new CapturingProtocol();
        var connectionFactory = new MT5ConnectionFactory(options, new TestBridge(), TimeProvider.System, NullLoggerFactory.Instance);
        var heartbeat = new MT5HeartbeatService(protocol, TimeProvider.System);
        await using var connector = new MT5BrokerConnector(
            Options.Create(options), connectionFactory, protocol, new MT5HealthService(heartbeat), new MT5ReconnectPolicy(options), new MT5RetryPolicy(options),
            new RecordingTelemetry(), new RecordingMetrics(), TimeProvider.System, NullLogger<MT5BrokerConnector>.Instance);
        await connector.GetHealthAsync(new BrokerExecutionContext(true, "actor-1", "User", "tenant-1", "org-1", [BrokerPermissionNames.ExecuteDemo], "session-1", "corr-1", "demo-confirmation"), CancellationToken.None);

        var result = await connector.ClosePositionAsync(
            new BrokerExecutionContext(true, "actor-1", "User", "tenant-1", "org-1", [BrokerPermissionNames.ExecuteDemo], "session-1", "corr-1", "demo-confirmation"),
            new BrokerPositionCloseRequest(new BrokerPositionId("position-1"), null), CancellationToken.None);

        Assert.Null(result.Error);
        Assert.NotNull(protocol.Request);
        Assert.Equal("close-position", protocol.Request!.Command);
        Assert.Equal("Demo", protocol.Request.Fields["mode"]);
        Assert.Equal("true", protocol.Request.Fields["demo_confirmation"]);
        Assert.Equal("true", protocol.Request.Fields["risk_approved"]);
        Assert.Equal("true", protocol.Request.Fields["trading_plan_valid"]);
        Assert.Equal("true", protocol.Request.Fields["execution_session_valid"]);
        Assert.Equal("true", protocol.Request.Fields["permission"]);
        Assert.Equal("true", protocol.Request.Fields["capability"]);
        Assert.Equal("true", protocol.Request.Fields["heartbeat_valid"]);
    }

    [Fact]
    public async Task Opt_in_real_demo_order_executes_once_and_cleans_up_without_logging_account_details()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("MT5_REAL_TESTS"), "true", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("MT5_REAL_WRITE_TESTS"), "true", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("MT5_REAL_DEMO_CONFIRMATION"), "I_CONFIRM_ONE_DEMO_ORDER", StringComparison.Ordinal)) return;

        var endpoint = Environment.GetEnvironmentVariable("MT5_BRIDGE_HOST_ENDPOINT");
        var symbol = Environment.GetEnvironmentVariable("MT5_REAL_TEST_SYMBOL");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(symbol)) return;
        Assert.Equal("XAUUSD-VIP", symbol, ignoreCase: true);

        var mt5Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{MT5Options.SectionName}:Mode"] = "Demo",
            [$"{MT5Options.SectionName}:AllowDemo"] = "true",
            [$"{MT5Options.SectionName}:AllowLive"] = "false",
            [$"{MT5Options.SectionName}:TimeoutSeconds"] = "30",
            [$"{MT5Options.SectionName}:ReconnectAttempts"] = "0"
        }).Build();
        var bridgeConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{MT5BridgeClientOptions.SectionName}:Endpoint"] = endpoint,
            [$"{MT5BridgeClientOptions.SectionName}:AuthenticationMode"] = nameof(BridgeAuthenticationMode.MutualTls),
            [$"{MT5BridgeClientOptions.SectionName}:AllowLive"] = "false",
            [$"{MT5BridgeClientOptions.SectionName}:RequireTls"] = "true",
            [$"{MT5BridgeClientOptions.SectionName}:RequestTimeoutSeconds"] = "30",
            [$"{MT5BridgeClientOptions.SectionName}:HandshakeTimeoutSeconds"] = "30"
        }).Build();
        var brokerConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TradeMind:Brokers:Enabled"] = "true",
            ["TradeMind:Brokers:DefaultMode"] = "Demo",
            ["TradeMind:Brokers:AllowDemoExecution"] = "true",
            ["TradeMind:Brokers:AllowLiveExecution"] = "false",
            ["TradeMind:Brokers:RequireTradingPlanAndRiskApproval"] = "true",
            ["TradeMind:Brokers:AllowedConnectors:0"] = "mt5",
            ["TradeMind:Brokers:OperationTimeoutSeconds"] = "30"
        }).Build();
        await using var provider = CreateRealProvider(mt5Configuration, bridgeConfiguration, brokerConfiguration);
        var connector = provider.GetRequiredService<IBrokerConnector>();
        var executionService = provider.GetRequiredService<IBrokerExecutionService>();
        var executionSessionId = "demo-session-" + Guid.NewGuid().ToString("N");
        var context = new BrokerExecutionContext(true, "demo-smoke-test", "Test", "demo-tenant", "demo-org", [BrokerPermissionNames.ExecuteDemo], executionSessionId, "demo-correlation", Environment.GetEnvironmentVariable("MT5_REAL_DEMO_CONFIRMATION"));

        var health = await connector.GetHealthAsync(context, CancellationToken.None);
        Assert.Equal(BrokerHealthStatus.Healthy, health.Health.Status);
        var accounts = await connector.GetAccountsAsync(context, CancellationToken.None);
        var account = Assert.Single(accounts);
        Assert.Equal(BrokerEnvironment.Test, account.Environment);
        Assert.True(account.TradingEnabled);
        Assert.False(account.ReadOnly);
        Assert.True(account.FreeMargin > 0);
        var instrument = await connector.GetInstrumentAsync(context, new BrokerInstrumentQuery(symbol), CancellationToken.None);
        Assert.NotNull(instrument);
        Assert.Equal(symbol, instrument!.Instrument, ignoreCase: true);
        Assert.Equal(BrokerMarketStatus.Open, instrument.MarketStatus);
        Assert.True(instrument.MinimumQuantity > 0);
        Assert.True(instrument.MaximumQuantity >= instrument.MinimumQuantity);
        Assert.True(instrument.QuantityStep > 0);
        Assert.True(instrument.TickSize > 0);
        Assert.True(instrument.TickValue > 0);
        Assert.True(instrument.ContractSize > 0);
        Assert.True(instrument.MinimumStopDistance >= 0);
        Assert.Contains(BrokerOrderType.Market, instrument.SupportedOrderTypes);
        var quantity = instrument.MinimumQuantity;
        var existingOrders = await connector.GetOrdersAsync(context, new BrokerOrderQuery(), CancellationToken.None);
        var existingPositions = await connector.GetPositionsAsync(context, new BrokerPositionQuery(), CancellationToken.None);
        var pendingOrders = existingOrders.Where(order => order.Instrument.Equals(symbol, StringComparison.OrdinalIgnoreCase) && order.Status == BrokerOrderStatus.Pending).ToArray();
        var smokeOrders = existingOrders.Where(order => order.Instrument.Equals(symbol, StringComparison.OrdinalIgnoreCase) && order.ClientOrderId.StartsWith("tm-demo-smoke-", StringComparison.Ordinal)).ToArray();
        var symbolPositions = existingPositions.Where(position => position.Instrument.Equals(symbol, StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.Empty(pendingOrders);
        Assert.Empty(smokeOrders);
        Assert.Empty(symbolPositions);

        var now = DateTimeOffset.UtcNow;
        var marketContextId = MarketContextId.New();
        var decisionId = TradingDecisionId.New();
        var riskAssessmentId = RiskAssessmentId.New();
        var plan = ValidTradingPlan(symbol, marketContextId, decisionId, riskAssessmentId, now);
        var risk = ValidRiskAssessment(symbol, marketContextId, decisionId, riskAssessmentId, now);
        var clientOrderId = "tm-demo-smoke-" + Guid.NewGuid().ToString("N");
        var idempotencyKey = "tm-demo-smoke-key-" + Guid.NewGuid().ToString("N");
        var request = new BrokerOrderRequest(new("demo-execution-" + Guid.NewGuid().ToString("N")), executionSessionId, connector.Descriptor.ConnectorId, account.AccountId, clientOrderId, symbol, BrokerOrderSide.Buy, BrokerOrderType.Market, quantity, null, null, null, [], BrokerTimeInForce.Day, null, idempotencyKey, "demo-correlation", "demo-tenant", "demo-org", "demo-smoke-test", plan.PlanId.ToString(), risk.AssessmentId.ToString(), new Dictionary<string, string> { ["demo_confirmation"] = "true" }, now);
        var command = new BrokerOrderExecutionCommand(request, plan, risk, BrokerExecutionMode.Demo);
        BrokerPositionId? positionId = null;
        var closed = false;
        try
        {
            var submission = await executionService.SubmitOrderAsync(context, command, CancellationToken.None);
            Assert.Equal(BrokerExecutionResultStatus.Accepted, submission.Status);
            Assert.Null(submission.Error);
            Assert.NotNull(submission.Order);
            Assert.NotNull(submission.Execution);
            Assert.Equal(BrokerOrderStatus.Filled, submission.Order!.Status);
            Assert.Equal(request.ClientOrderId, submission.Order.ClientOrderId);
            Assert.Equal(symbol, submission.Order.Instrument, ignoreCase: true);
            Assert.Equal(request.Quantity, submission.Execution!.FilledQuantity);
            Assert.Equal(submission.Order.OrderId, submission.Execution.OrderId);
            Assert.Equal(BrokerOrderStatus.Filled, submission.Execution.Status);
            Assert.NotNull(submission.Execution.AveragePrice);
            positionId = submission.Execution.PositionId;
            Assert.NotNull(positionId);

            var positionsAfterFill = (await connector.GetPositionsAsync(context, new BrokerPositionQuery(symbol), CancellationToken.None))
                .Where(position => position.Instrument.Equals(symbol, StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.Single(positionsAfterFill);
            Assert.Equal(request.Quantity, positionsAfterFill[0].Quantity);
            Assert.Equal(positionId, positionsAfterFill[0].PositionId);

            var replay = await executionService.SubmitOrderAsync(context, command, CancellationToken.None);
            Assert.Equal(BrokerExecutionResultStatus.Replayed, replay.Status);
            Assert.Equal(submission.Order.OrderId, replay.Order?.OrderId);
            Assert.Equal(submission.Execution.ExecutionId, replay.Execution?.ExecutionId);

            var conflictingRequest = new BrokerOrderRequest(
                new("demo-conflict-" + Guid.NewGuid().ToString("N")), executionSessionId, connector.Descriptor.ConnectorId, account.AccountId,
                clientOrderId + "-conflict", symbol, BrokerOrderSide.Buy, BrokerOrderType.Market, quantity + instrument.QuantityStep, null, null, null, [],
                BrokerTimeInForce.Day, null, idempotencyKey, "demo-correlation", "demo-tenant", "demo-org", "demo-smoke-test", plan.PlanId.ToString(),
                risk.AssessmentId.ToString(), new Dictionary<string, string> { ["demo_confirmation"] = "true" }, now);
            var conflict = await executionService.SubmitOrderAsync(context, new BrokerOrderExecutionCommand(conflictingRequest, plan, risk, BrokerExecutionMode.Demo), CancellationToken.None);
            Assert.Equal(BrokerExecutionResultStatus.Conflict, conflict.Status);
            Assert.Equal("IDEMPOTENCY_CONFLICT", conflict.Error?.Code);

            var positionsBeforeClose = (await connector.GetPositionsAsync(context, new BrokerPositionQuery(symbol), CancellationToken.None))
                .Where(position => position.Instrument.Equals(symbol, StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.Single(positionsBeforeClose);
        }
        finally
        {
            if (positionId is not null)
            {
                var cleanup = await executionService.ClosePositionAsync(context, connector.Descriptor.ConnectorId, new BrokerPositionCloseRequest(positionId, null), CancellationToken.None);
                Assert.Null(cleanup.Error);
                Assert.NotNull(cleanup.Execution);
                closed = true;
            }

            var residualPositions = (await connector.GetPositionsAsync(context, new BrokerPositionQuery(symbol), CancellationToken.None))
                .Where(position => position.Instrument.Equals(symbol, StringComparison.OrdinalIgnoreCase)).ToArray();
            var residualPendingOrders = (await connector.GetOrdersAsync(context, new BrokerOrderQuery(), CancellationToken.None))
                .Where(order => order.Instrument.Equals(symbol, StringComparison.OrdinalIgnoreCase) && order.Status == BrokerOrderStatus.Pending).ToArray();
            Assert.Empty(residualPositions);
            Assert.Empty(residualPendingOrders);
            Assert.True(closed || positionId is null);
        }
    }

    private static TradingPlanResult ValidTradingPlan(string symbol, MarketContextId contextId, TradingDecisionId decisionId, RiskAssessmentId riskAssessmentId, DateTimeOffset now) =>
        new(TradingPlanId.New(), decisionId, riskAssessmentId, contextId, new(symbol), Timeframe.H1, TradingPlanDirection.Long, TradingPlanStrategy.DecisionAligned,
            TradingPlanStatus.Succeeded, TradingPlanType.ExecutableCandidate, RiskVerdict.Approved, null, [], null, null, [], null, [], [], [], new PreTradeChecklist([]), [], [], [], [], [], [],
            "Validated Demo smoke-test plan.", now.AddMinutes(-1), now, now.AddMinutes(10));

    private static RiskAssessmentResult ValidRiskAssessment(string symbol, MarketContextId contextId, TradingDecisionId decisionId, RiskAssessmentId riskAssessmentId, DateTimeOffset now) =>
        new(riskAssessmentId, decisionId, contextId, new(symbol), Timeframe.H1, RiskStrategy.Conservative, RiskAssessmentStatus.Succeeded, RiskVerdict.Approved,
            null, null, null, [], null, null, [], [], [], [], [], [], now.AddMinutes(-1), now);

    [Fact]
    public async Task Health_exposes_bridge_protocol_terminal_latency_heartbeat_and_reconnect_data()
    {
        await using var provider = CreateProvider();
        var factory = provider.GetRequiredService<IMT5ConnectionFactory>();
        var health = provider.GetRequiredService<IMT5HealthService>();
        await using var connection = factory.Create();

        var result = await health.CheckAsync(connection, CancellationToken.None);

        Assert.True(result.Healthy);
        Assert.Equal("simulation-1.0", result.Snapshot.BridgeVersion);
        Assert.Equal("1.0", result.Snapshot.ProtocolVersion);
        Assert.Equal("simulation-terminal", result.Snapshot.TerminalVersion);
        Assert.True(result.Snapshot.Heartbeat);
        Assert.Equal(MT5ConnectionState.Connected, result.Snapshot.ConnectionState);
        Assert.NotNull(result.Snapshot.LastHeartbeat);
    }

    [Fact]
    public async Task Demo_mode_is_explicit_and_live_mode_is_rejected()
    {
        await using var provider = CreateProvider(mode: "Demo", allowDemo: true);
        var connector = provider.GetRequiredService<IBrokerConnector>();
        Assert.Equal(BrokerExecutionMode.Demo, connector.Descriptor.Mode);
        Assert.True(connector.Descriptor.SupportsDemo);
        Assert.False(connector.Descriptor.SupportsLive);
        Assert.True(connector.Descriptor.Capabilities.Supports(BrokerCapability.DemoTrading));

        var validation = new MT5OptionsValidator().Validate(null, new MT5Options { Mode = "Live", AllowLive = true });
        Assert.False(validation.Succeeded);
        Assert.Contains("Live MetaTrader 5 execution is not implemented", validation.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task External_cancellation_is_propagated_without_retrying()
    {
        var bridge = new TestBridge { BlockOpen = true };
        await using var connection = CreateConnection(bridge);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connection.ConnectAsync(cancellation.Token));
        Assert.Equal(0, bridge.OpenCalls);
    }

    [Fact]
    public async Task Retry_policy_is_bounded_and_telemetry_records_safe_mt5_operations()
    {
        var policy = new MT5RetryPolicy(new MT5Options { ReconnectAttempts = 2 });
        Assert.True(policy.ShouldRetry(0, new TimeoutException()));
        Assert.False(policy.ShouldRetry(2, new TimeoutException()));
        Assert.False(policy.ShouldRetry(0, new OperationCanceledException()));

        await using var provider = CreateProvider();
        var connector = provider.GetRequiredService<IBrokerConnector>();
        await connector.GetHealthAsync(Context(), CancellationToken.None);
        var metrics = provider.GetRequiredService<RecordingMetrics>();

        Assert.Contains(TelemetryMetricNames.Mt5Connections, metrics.Counters);
        Assert.Contains(TelemetryMetricNames.Mt5Heartbeats, metrics.Counters);
        Assert.Contains(TelemetryMetricNames.Mt5Requests, metrics.Counters);
        Assert.DoesNotContain(metrics.Dimensions, item => item.Contains("tenant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Api_can_discover_and_health_check_adapter_through_neutral_registry()
    {
        await using var factory = new MT5ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TradeMind-Test-Actor", "actor=actor-1;organization=org-1;tenant=tenant-1;permissions=TradeMind.Brokers.Read");

        var list = await client.GetAsync("/api/v1/brokers");
        var health = await client.GetAsync("/api/v1/brokers/mt5/health");
        var listBody = await list.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("mt5", listBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public void Core_references_do_not_depend_on_the_adapter_or_an_external_mt5_sdk()
    {
        var coreAssemblies = new[]
        {
            typeof(IBrokerConnector).Assembly,
            typeof(BrokerId).Assembly,
            typeof(TradeMind.Observability.Abstractions.ITradeMindTelemetry).Assembly
        };

        foreach (var assembly in coreAssemblies)
        {
            var names = assembly.GetReferencedAssemblies().Select(item => item.Name ?? "");
            Assert.DoesNotContain(names, name => name.Contains("MetaTrader", StringComparison.OrdinalIgnoreCase) || name.Contains("MT5", StringComparison.OrdinalIgnoreCase));
        }

        var adapterReferences = typeof(TradeMind.Brokers.MetaTrader5.Execution.MT5BrokerConnector).Assembly.GetReferencedAssemblies().Select(item => item.Name ?? "");
        Assert.DoesNotContain(adapterReferences, name => name.Contains("MetaTrader", StringComparison.OrdinalIgnoreCase) || name.Contains("MT5Sdk", StringComparison.OrdinalIgnoreCase));
    }

    private static MT5Connection CreateConnection(IMT5Bridge bridge) => new(
        new MT5Options { TimeoutSeconds = 1, ReconnectAttempts = 1 }, bridge, TimeProvider.System, NullLogger<MT5Connection>.Instance);

    private static ServiceProvider CreateProvider(string mode = "Simulation", bool allowDemo = false)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{MT5Options.SectionName}:Mode"] = mode,
            [$"{MT5Options.SectionName}:AllowDemo"] = allowDemo.ToString(),
            [$"{MT5Options.SectionName}:AllowLive"] = "false",
            [$"{MT5Options.SectionName}:TimeoutSeconds"] = "10",
            [$"{MT5Options.SectionName}:ReconnectAttempts"] = "1"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITradeMindTelemetry, RecordingTelemetry>();
        services.AddSingleton<RecordingMetrics>();
        services.AddSingleton<ITradeMindMetrics>(provider => provider.GetRequiredService<RecordingMetrics>());
        services.AddTradeMindMetaTrader5(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static ServiceProvider CreateRealProvider(IConfiguration mt5Configuration, IConfiguration bridgeConfiguration, IConfiguration brokerConfiguration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITradeMindTelemetry, RecordingTelemetry>();
        services.AddSingleton<ITradeMindMetrics, RecordingMetrics>();
        services.AddTradeMindBrokersApplication(brokerConfiguration);
        services.AddTradeMindMetaTrader5(mt5Configuration);
        services.AddTradeMindMetaTrader5BridgeClient(bridgeConfiguration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static BrokerExecutionContext Context() => new(true, "actor-1", "User", "tenant-1", "org-1", [BrokerPermissionNames.ExecuteSimulation], "session-1", "corr-1", "simulation-confirmation");

    private static BrokerOrderRequest Request(BrokerConnectorId connectorId, BrokerOrderType orderType, string clientOrderId, decimal? requestedPrice = null) =>
        new(new("execution-" + clientOrderId), "session-1", connectorId, new("mt5-demo-account"), clientOrderId, "EURUSD", BrokerOrderSide.Buy,
            orderType, 1m, requestedPrice, null, null, [], BrokerTimeInForce.Day, null, "key-" + clientOrderId, "corr-1", "tenant-1", "org-1", "actor-1", null, null, null, DateTimeOffset.UtcNow);

    private sealed class TestBridge : IMT5Bridge
    {
        private readonly TaskCompletionSource<bool> openRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool FailFirstOpen { get; init; }
        public bool BlockOpen { get; init; }
        public int OpenCalls { get; private set; }
        public string BridgeVersion => "test-bridge";

        public async Task OpenAsync(CancellationToken cancellationToken)
        {
            OpenCalls++;
            if (FailFirstOpen && OpenCalls == 1) throw new InvalidOperationException("first connection failed");
            if (BlockOpen) await openRelease.Task.WaitAsync(cancellationToken);
        }

        public Task AuthenticateAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<string> SendAsync(string payload, CancellationToken cancellationToken) => Task.FromResult("{}");
    }

    private sealed class CapturingProtocol : IMT5Protocol
    {
        public MT5Request? Request { get; private set; }

        public Task<MT5Response> ExecuteAsync(IMT5Connection connection, MT5Request request, CancellationToken cancellationToken)
        {
            Request = request;
            if (request.Command == "heartbeat") return Task.FromResult(new MT5Response(true, "HEARTBEAT", "Heartbeat acknowledged"));
            const string position = "{\"positionId\":\"position-1\",\"accountId\":\"account-1\",\"instrument\":\"XAUUSD-VIP\",\"side\":\"Buy\",\"quantity\":1,\"averagePrice\":2000,\"openedAtUtc\":\"2026-08-13T22:00:00Z\",\"updatedAtUtc\":\"2026-08-13T22:00:00Z\"}";
            const string execution = "{\"executionId\":\"execution-1\",\"orderId\":\"order-1\",\"accountId\":\"account-1\",\"status\":\"Filled\",\"filledQuantity\":1,\"averagePrice\":2000,\"occurredAtUtc\":\"2026-08-13T22:00:01Z\",\"positionId\":\"position-1\"}";
            return Task.FromResult(new MT5Response(true, "POSITION_CLOSED", "Position closed", new Dictionary<string, string> { ["position"] = position, ["execution"] = execution }));
        }
    }

    private sealed class RecordingTelemetry : ITradeMindTelemetry
    {
        public ITradeMindActivity StartActivity(TelemetryOperation operation, TelemetryContext? context = null, IReadOnlyCollection<TelemetryLink>? links = null) => new ActivitySpy();
        public void EnrichCurrent(TelemetryContext context) { }
    }

    private sealed class RecordingMetrics : ITradeMindMetrics
    {
        public List<string> Counters { get; } = [];
        public List<string> Dimensions { get; } = [];
        public void IncrementCounter(string name, long value, MetricDimensions dimensions)
        {
            Counters.Add(name);
            Dimensions.Add($"{dimensions.Connector}|{dimensions.Mode}|{dimensions.Operation}");
        }
        public void RecordDuration(string name, TimeSpan duration, MetricDimensions dimensions) { }
        public void SetActiveExecutionSessions(long value) { }
    }

    private sealed class ActivitySpy : ITradeMindActivity
    {
        public bool IsRecording => true;
        public string? TraceId => "trace";
        public string? SpanId => "span";
        public void Dispose() { }
        public void SetTag(string name, string? value) { }
        public void SetTag(string name, long? value) { }
        public void SetOutcome(TelemetryOutcome outcome) { }
        public void RecordException(Exception exception) { }
    }
}

public sealed class MT5ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("TradeMind:Identity:Enabled", "true");
        builder.UseSetting("TradeMind:Identity:Jwt:Enabled", "false");
        builder.UseSetting("TradeMind:Identity:Development:EnableTestAuthentication", "true");
        builder.UseSetting("TradeMind:Identity:RateLimiting:Enabled", "false");
        builder.UseSetting("TradeMind:Api:Security:EnableHttpsRedirection", "false");
        builder.UseSetting("ConnectionStrings:KnowledgeHub", string.Empty);
        builder.UseSetting("ConnectionStrings:MarketConnectors", string.Empty);
        builder.UseSetting("ConnectionStrings:Identity", string.Empty);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnowledgeHub"] = string.Empty,
            ["ConnectionStrings:MarketConnectors"] = string.Empty,
            ["ConnectionStrings:Identity"] = string.Empty,
            [$"{MT5Options.SectionName}:Mode"] = "Simulation",
            [$"{MT5Options.SectionName}:AllowLive"] = "false"
        }));
        builder.ConfigureServices(services =>
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{MT5Options.SectionName}:Mode"] = "Simulation",
                [$"{MT5Options.SectionName}:AllowLive"] = "false"
            }).Build();
            services.AddTradeMindMetaTrader5(configuration);
        });
    }
}
