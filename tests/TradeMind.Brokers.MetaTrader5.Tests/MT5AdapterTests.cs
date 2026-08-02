using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Domain;
using TradeMind.Brokers.MetaTrader5.Bridge;
using TradeMind.Brokers.MetaTrader5.Configuration;
using TradeMind.Brokers.MetaTrader5.Connection;
using TradeMind.Brokers.MetaTrader5.DependencyInjection;
using TradeMind.Brokers.MetaTrader5.Health;
using TradeMind.Brokers.MetaTrader5.Protocol;
using TradeMind.Brokers.MetaTrader5.Retry;
using TradeMind.Brokers.MetaTrader5.Serialization;
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
        var closed = await connector.ClosePositionAsync(context, new BrokerPositionCloseRequest(market.Execution!.PositionId!, null), CancellationToken.None);

        Assert.Null(modified.Error);
        Assert.Equal(2m, modified.Order!.Quantity);
        Assert.Null(cancelled.Error);
        Assert.Equal(BrokerOrderStatus.Cancelled, cancelled.Order!.Status);
        Assert.Null(closed.Error);
        Assert.Equal(BrokerOrderStatus.Filled, closed.Execution!.Status);
    }

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

    private static BrokerExecutionContext Context() => new(true, "actor-1", "User", "tenant-1", "org-1", [], "session-1", "corr-1");

    private static BrokerOrderRequest Request(BrokerConnectorId connectorId, BrokerOrderType orderType, string clientOrderId, decimal? requestedPrice = null) =>
        new(new("execution-" + clientOrderId), "session-1", connectorId, new("mt5-demo-account"), clientOrderId, "EURUSD", BrokerOrderSide.Buy,
            orderType, 1m, requestedPrice, null, null, [], BrokerTimeInForce.Day, null, "key-" + clientOrderId, "corr-1", "tenant-1", "org-1", "actor-1", null, null, null, DateTimeOffset.UtcNow);

    private sealed class TestBridge : IMT5Bridge
    {
        public bool FailFirstOpen { get; init; }
        public bool BlockOpen { get; init; }
        public int OpenCalls { get; private set; }
        public string BridgeVersion => "test-bridge";

        public async Task OpenAsync(CancellationToken cancellationToken)
        {
            OpenCalls++;
            if (FailFirstOpen && OpenCalls == 1) throw new InvalidOperationException("first connection failed");
            if (BlockOpen) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public Task AuthenticateAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<string> SendAsync(string payload, CancellationToken cancellationToken) => Task.FromResult("{}");
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
