using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Domain;
using TradeMind.Brokers.MetaTrader5.Bridge;
using TradeMind.Brokers.MetaTrader5.Configuration;
using TradeMind.Brokers.MetaTrader5.Connection;
using TradeMind.Brokers.MetaTrader5.Execution;
using TradeMind.Brokers.MetaTrader5.Health;
using TradeMind.Brokers.MetaTrader5.Protocol;
using TradeMind.Brokers.MetaTrader5.Retry;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.MetaTrader5.Tests;

public sealed class MT5ClosePositionSafetyTests
{
    [Fact]
    public async Task Valid_demo_close_is_allowed_after_recent_heartbeat()
    {
        var protocol = new GuardProtocol();
        await using var connector = CreateConnector(protocol, Snapshot());
        var result = await connector.ClosePositionAsync(Context(), new BrokerPositionCloseRequest(new("position-1"), null), CancellationToken.None);

        Assert.Equal("POSITION_NOT_FOUND", result.Error?.Code);
        Assert.Equal(1, protocol.Calls);
    }

    [Theory]
    [InlineData("missing-confirmation")]
    [InlineData("invalid-session")]
    [InlineData("invalid-permission")]
    [InlineData("stale-heartbeat")]
    [InlineData("disconnected")]
    [InlineData("non-demo-account")]
    public async Task Invalid_close_guard_is_rejected_before_terminal_invocation(string scenario)
    {
        var protocol = new GuardProtocol();
        var snapshot = scenario switch
        {
            "stale-heartbeat" => Snapshot(lastHeartbeat: DateTimeOffset.UtcNow.AddMinutes(-10)),
            "disconnected" => Snapshot(connectionState: MT5ConnectionState.Disconnected),
            "non-demo-account" => Snapshot(accountEnvironment: "Live"),
            _ => Snapshot()
        };
        await using var connector = CreateConnector(protocol, snapshot);
        var context = scenario switch
        {
            "missing-confirmation" => Context(confirmation: null),
            "invalid-session" => Context(session: null),
            "invalid-permission" => Context(permission: BrokerPermissionNames.Read),
            _ => Context()
        };

        var result = await connector.ClosePositionAsync(context, new BrokerPositionCloseRequest(new("position-1"), null), CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.Equal(0, protocol.Calls);
        Assert.Contains(result.Error!.Code, new[] { "DEMO_CONFIRMATION_REQUIRED", "EXECUTION_SESSION_REQUIRED", "FORBIDDEN", "HEARTBEAT_INVALID", "TERMINAL_UNAVAILABLE", "DEMO_EXECUTION_REQUIRED" });
    }

    [Fact]
    public async Task Position_not_found_is_a_controlled_result()
    {
        var protocol = new GuardProtocol { Response = new MT5Response(false, "POSITION_NOT_FOUND", "Position was not found") };
        await using var connector = CreateConnector(protocol, Snapshot());

        var result = await connector.ClosePositionAsync(Context(), new BrokerPositionCloseRequest(new("missing"), null), CancellationToken.None);

        Assert.Equal("POSITION_NOT_FOUND", result.Error?.Code);
        Assert.Null(result.Execution);
        Assert.Equal(1, protocol.Calls);
    }

    [Fact]
    public void Live_configuration_is_rejected_before_connector_creation()
    {
        var options = new MT5Options { Mode = "Demo", AllowDemo = true, AllowLive = true };
        Assert.Throws<InvalidOperationException>(() => new MT5BrokerConnector(
            Options.Create(options), new StaticConnectionFactory(new GuardConnection(Snapshot())), new GuardProtocol(), new NoopHealthService(),
            new MT5ReconnectPolicy(options), new MT5RetryPolicy(options), new NoopTelemetry(), new NoopMetrics(), TimeProvider.System,
            NullLogger<MT5BrokerConnector>.Instance));
    }

    private static MT5BrokerConnector CreateConnector(GuardProtocol protocol, MT5ConnectionSnapshot snapshot)
    {
        var options = new MT5Options { Mode = "Demo", AllowDemo = true, AllowLive = false, HeartbeatSeconds = 30 };
        return new MT5BrokerConnector(
            Options.Create(options), new StaticConnectionFactory(new GuardConnection(snapshot)), protocol, new NoopHealthService(),
            new MT5ReconnectPolicy(options), new MT5RetryPolicy(options), new NoopTelemetry(), new NoopMetrics(), TimeProvider.System,
            NullLogger<MT5BrokerConnector>.Instance);
    }

    private static BrokerExecutionContext Context(string? session = "session-1", string? permission = BrokerPermissionNames.ExecuteDemo, string? confirmation = "demo-confirmation") =>
        new(true, "actor", "User", "tenant", "org", permission is null ? [] : [permission], session, "correlation", confirmation);

    private static MT5ConnectionSnapshot Snapshot(
        MT5ConnectionState connectionState = MT5ConnectionState.Connected,
        string accountEnvironment = "Demo",
        DateTimeOffset? lastHeartbeat = null) => new(
        "bridge", "1.0", "terminal", TimeSpan.FromMilliseconds(1), true, 0, connectionState,
        lastHeartbeat ?? DateTimeOffset.UtcNow, null, accountEnvironment, true, false);

    private sealed class StaticConnectionFactory(IMT5Connection connection) : IMT5ConnectionFactory
    {
        public IMT5Connection Create() => connection;
    }

    private sealed class GuardConnection(MT5ConnectionSnapshot initialSnapshot) : IMT5Connection
    {
        public MT5ConnectionState State => initialSnapshot.ConnectionState;
        public MT5ConnectionSnapshot Snapshot => initialSnapshot;
        public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<string> SendRawAsync(string payload, CancellationToken cancellationToken) => Task.FromResult("{}");
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class GuardProtocol : IMT5Protocol
    {
        public int Calls { get; private set; }
        public MT5Response Response { get; init; } = new(false, "POSITION_NOT_FOUND", "Position was not found");
        public Task<MT5Response> ExecuteAsync(IMT5Connection connection, MT5Request request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Response);
        }
    }

    private sealed class NoopHealthService : IMT5HealthService
    {
        public Task<MT5HealthResult> CheckAsync(IMT5Connection connection, CancellationToken cancellationToken) => Task.FromResult(new MT5HealthResult(true, connection.Snapshot, "ok"));
    }

    private sealed class NoopTelemetry : ITradeMindTelemetry
    {
        public ITradeMindActivity StartActivity(TelemetryOperation operation, TelemetryContext? context = null, IReadOnlyCollection<TelemetryLink>? links = null) => new Activity();
        public void EnrichCurrent(TelemetryContext context) { }
        private sealed class Activity : ITradeMindActivity
        {
            public bool IsRecording => false;
            public string? TraceId => null;
            public string? SpanId => null;
            public void Dispose() { }
            public void RecordException(Exception exception) { }
            public void SetOutcome(TelemetryOutcome outcome) { }
            public void SetTag(string name, string? value) { }
            public void SetTag(string name, long? value) { }
        }
    }

    private sealed class NoopMetrics : ITradeMindMetrics
    {
        public void IncrementCounter(string name, long value, MetricDimensions dimensions) { }
        public void RecordDuration(string name, TimeSpan duration, MetricDimensions dimensions) { }
        public void SetActiveExecutionSessions(long value) { }
    }
}
