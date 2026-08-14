using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Application.Options;
using TradeMind.Brokers.Domain;
using TradeMind.Brokers.Infrastructure.Idempotency;
using TradeMind.Brokers.Infrastructure.InMemory;
using TradeMind.Brokers.Infrastructure.Persistence;
using TradeMind.Brokers.Infrastructure.Persistence.Entities;
using TradeMind.Brokers.Infrastructure.Health;
using TradeMind.Brokers.Infrastructure.Reconciliation;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.Infrastructure.Tests;

public sealed class BrokerInfrastructureTests(PostgreSqlBrokerFixture fixture) : IClassFixture<PostgreSqlBrokerFixture>
{
    [Fact]
    public async Task Migration_creates_expected_tables_and_indexes()
    {
        await using var context = fixture.CreateContext();
        var tables = await context.Database.SqlQuery<string>($"select table_name from information_schema.tables where table_schema = 'public' and table_name like 'broker_%' order by table_name").ToListAsync();
        Assert.Contains("broker_idempotency", tables);
        Assert.Contains("broker_audit", tables);
        Assert.Contains("broker_fills", tables);
        Assert.Contains("broker_orders", tables);
    }

    [Fact]
    public async Task Durable_idempotency_replays_result()
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("delete from broker_idempotency");
        var factory = new TestFactory(fixture.ConnectionString);
        var store = new PostgreSqlBrokerIdempotencyStore(factory, TimeProvider.System, Microsoft.Extensions.Options.Options.Create(new BrokerOptions { IdempotencyTtlMinutes = 60 }));
        var calls = 0;
        var first = await store.ExecuteAsync("key-hash", "request-hash", _ => { calls++; return Task.FromResult(Result()); }, CancellationToken.None);
        var second = await store.ExecuteAsync("key-hash", "request-hash", _ => { calls++; return Task.FromResult(Result()); }, CancellationToken.None);
        Assert.False(first.IsReplay);
        Assert.True(second.IsReplay);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task In_memory_connector_fills_market_order_and_supports_reconciliation()
    {
        var state = new InMemoryBrokerState();
        var clock = new FixedClock();
        var connector = new InMemoryBrokerConnector(state, clock);
        state.ConfigureAccount(new BrokerAccount(new("account"), connector.Descriptor.ConnectorId, "external", "Simulation", "USD", BrokerAccountType.Demo, BrokerEnvironment.Test, 1000, 1000, 0, 1000, null, 100, true, false, BrokerAccountStatus.Active, clock.UtcNow));
        state.ConfigureInstrument(new BrokerInstrumentSpecification("EURUSD", "EURUSD", BrokerAssetClass.Forex, "EUR", "USD", 5, 2, 0.01m, 1, 100000, 0.01m, 100, 0.01m, 0, [], BrokerMarketStatus.Open, [BrokerOrderType.Market], clock.UtcNow));
        var result = await connector.SubmitOrderAsync(Context(), Request(connector.Descriptor.ConnectorId), CancellationToken.None);
        Assert.Null(result.Error);
        Assert.Equal(BrokerOrderStatus.Filled, result.Order!.Status);
        Assert.NotNull(result.Execution!.PositionId);
        Assert.Single(await connector.GetPositionsAsync(Context(), new BrokerPositionQuery(), CancellationToken.None));

        var close = await connector.ClosePositionAsync(Context(), new BrokerPositionCloseRequest(result.Execution.PositionId!, null), CancellationToken.None);

        Assert.Null(close.Error);
        Assert.Empty(await connector.GetPositionsAsync(Context(), new BrokerPositionQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Normalized_execution_persists_execution_order_fill_and_position()
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("delete from broker_fills; delete from broker_positions; delete from broker_orders; delete from broker_executions;");
        var request = Request(new BrokerConnectorId("in-memory"));
        var order = new BrokerOrder(new("order-1"), request.ExecutionId, request.ConnectorId, request.AccountId, request.ClientOrderId, request.Instrument, request.Side, request.OrderType, request.Quantity, request.Quantity, null, 1m, BrokerOrderStatus.Filled, request.TimeInForce, request.CreatedAtUtc, request.CreatedAtUtc);
        var execution = new BrokerExecution(request.ExecutionId, order.OrderId, request.ConnectorId, request.AccountId, BrokerOrderStatus.Filled, request.Quantity, 1m, request.CreatedAtUtc, positionId: new BrokerPositionId("position-1"));
        var result = new BrokerExecutionResult(request.ExecutionId, BrokerExecutionResultStatus.Accepted, order, execution, null, "SubmitOrder", request.CreatedAtUtc);
        var writer = new BrokerExecutionRecordWriter(new TestFactory(fixture.ConnectionString));

        await writer.WriteAsync(new BrokerOrderExecutionCommand(request, null, null), result, CancellationToken.None);

        await using var read = fixture.CreateContext();
        Assert.Equal(1, await read.Executions.CountAsync(item => item.Id == request.ExecutionId.Value));
        Assert.Equal(1, await read.Orders.CountAsync(item => item.Id == order.OrderId.Value));
        Assert.Equal(1, await read.Fills.CountAsync(item => item.ExecutionId == request.ExecutionId.Value));
        Assert.Equal(1, await read.Positions.CountAsync(item => item.Id == "position-1"));
    }

    [Fact]
    public async Task Orphan_detector_classifies_known_and_unknown_positions_without_remediation()
    {
        var (connector, context) = CreateSimulationConnector();
        var submission = await connector.SubmitOrderAsync(context, Request(connector.Descriptor.ConnectorId), CancellationToken.None);
        var position = (await connector.GetPositionsAsync(context, new BrokerPositionQuery(), CancellationToken.None)).Single();
        var registry = new BrokerConnectorRegistry([connector]);
        var safety = new InMemoryBrokerExecutionSafetyState();
        var detector = new BrokerOrphanPositionDetector(registry, new FixedClock(), Options.Create(new BrokerOptions { OrphanPositionStaleAfterSeconds = 300 }), new NoOpTelemetry(), new NoOpMetrics(), new NoOpAudit(), safety);

        var known = await detector.DetectAsync(context, connector.Descriptor.ConnectorId, new BrokerAccountId("account"), [new BrokerKnownPositionReference(position.PositionId, "session", false, position.UpdatedAtUtc)], CancellationToken.None);
        Assert.True(known.IsSafeForExecution);
        Assert.Equal(BrokerPositionClassification.Known, Assert.Single(known.Alerts).Classification);
        Assert.Single(await connector.GetPositionsAsync(context, new BrokerPositionQuery(), CancellationToken.None));
        await connector.SubmitOrderAsync(context, Request(connector.Descriptor.ConnectorId, "unknown-client"), CancellationToken.None);
        var unknown = await detector.DetectAsync(context, connector.Descriptor.ConnectorId, new BrokerAccountId("account"), [], CancellationToken.None);

        Assert.False(unknown.IsSafeForExecution);
        Assert.Equal(BrokerPositionClassification.UnknownExternal, Assert.Single(unknown.Alerts).Classification);
        Assert.Equal(BrokerExecutionReadiness.Blocked, safety.Snapshot.Readiness);
    }

    [Fact]
    public async Task Broker_readiness_blocks_writes_when_a_position_is_unknown_but_reads_remain_available()
    {
        var (connector, context) = CreateSimulationConnector();
        await connector.SubmitOrderAsync(context, Request(connector.Descriptor.ConnectorId), CancellationToken.None);
        var registry = new BrokerConnectorRegistry([connector]);
        var safety = new InMemoryBrokerExecutionSafetyState();
        var detector = new BrokerOrphanPositionDetector(registry, new FixedClock(), Options.Create(new BrokerOptions()), new NoOpTelemetry(), new NoOpMetrics(), new NoOpAudit(), safety);
        var check = new BrokerConnectorHealthCheck(registry, detector, safety);

        var result = await check.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext(), CancellationToken.None);

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, result.Status);
        Assert.Equal(BrokerExecutionReadiness.Blocked, safety.Snapshot.Readiness);
        Assert.Single(await connector.GetPositionsAsync(context, new BrokerPositionQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Orphan_detector_classifies_orphaned_unreconciled_and_stale_positions()
    {
        var cases = new (BrokerPositionClassification Classification, BrokerKnownPositionReference Reference, IBrokerClock Clock, int StaleAfterSeconds)[]
        {
            (BrokerPositionClassification.Orphaned, new BrokerKnownPositionReference(new BrokerPositionId("position"), null, false, DateTimeOffset.UnixEpoch), new FixedClock(), 300),
            (BrokerPositionClassification.Unreconciled, new BrokerKnownPositionReference(new BrokerPositionId("position"), "session", true, DateTimeOffset.UnixEpoch), new FixedClock(), 300),
            (BrokerPositionClassification.Stale, new BrokerKnownPositionReference(new BrokerPositionId("position"), "session", false, DateTimeOffset.UnixEpoch), new OffsetClock(TimeSpan.FromSeconds(301)), 300)
        };

        foreach (var item in cases)
        {
            var (connector, context) = CreateSimulationConnector();
            var submission = await connector.SubmitOrderAsync(context, Request(connector.Descriptor.ConnectorId, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)), CancellationToken.None);
            var position = (await connector.GetPositionsAsync(context, new BrokerPositionQuery(), CancellationToken.None)).Single();
            var detector = new BrokerOrphanPositionDetector(
                new BrokerConnectorRegistry([connector]),
                item.Clock,
                Options.Create(new BrokerOptions { OrphanPositionStaleAfterSeconds = item.StaleAfterSeconds }),
                new NoOpTelemetry(),
                new NoOpMetrics(),
                new NoOpAudit(),
                new InMemoryBrokerExecutionSafetyState());

            var report = await detector.DetectAsync(context, connector.Descriptor.ConnectorId, new BrokerAccountId("account"), [item.Reference with { PositionId = position.PositionId }], CancellationToken.None);

            Assert.Equal(item.Classification, Assert.Single(report.Alerts).Classification);
        }
    }

    private static (InMemoryBrokerConnector Connector, BrokerExecutionContext Context) CreateSimulationConnector()
    {
        var state = new InMemoryBrokerState();
        var clock = new FixedClock();
        var connector = new InMemoryBrokerConnector(state, clock);
        state.ConfigureAccount(new BrokerAccount(new("account"), connector.Descriptor.ConnectorId, "external", "Simulation", "USD", BrokerAccountType.Demo, BrokerEnvironment.Test, 1000, 1000, 0, 1000, null, 100, true, false, BrokerAccountStatus.Active, clock.UtcNow));
        state.ConfigureInstrument(new BrokerInstrumentSpecification("EURUSD", "EURUSD", BrokerAssetClass.Forex, "EUR", "USD", 5, 2, 0.01m, 1, 100000, 0.01m, 100, 0.01m, 0, [], BrokerMarketStatus.Open, [BrokerOrderType.Market], clock.UtcNow));
        return (connector, Context());
    }

    private static BrokerExecutionContext Context() => new(true, "actor", "User", "tenant", "org", [BrokerPermissionNames.ExecuteSimulation], "session", "corr");
    private static BrokerOrderRequest Request(BrokerConnectorId connectorId) => Request(connectorId, DateTimeOffset.UtcNow);
    private static BrokerOrderRequest Request(BrokerConnectorId connectorId, DateTimeOffset createdAtUtc) => new(new("exec"), "session", connectorId, new("account"), "client", "EURUSD", BrokerOrderSide.Buy, BrokerOrderType.Market, 1, null, null, null, [], BrokerTimeInForce.Day, null, "key", "corr", "tenant", "org", "actor", null, null, null, createdAtUtc);
    private static BrokerOrderRequest Request(BrokerConnectorId connectorId, string clientOrderId) => new(new("exec-" + clientOrderId), "session", connectorId, new("account"), clientOrderId, "EURUSD", BrokerOrderSide.Buy, BrokerOrderType.Market, 1, null, null, null, [], BrokerTimeInForce.Day, null, "key-" + clientOrderId, "corr", "tenant", "org", "actor", null, null, null, DateTimeOffset.UtcNow);
    private static BrokerExecutionResult Result() => new(new("exec"), BrokerExecutionResultStatus.Accepted, null, null, null, "SubmitOrder", DateTimeOffset.UtcNow);
    private sealed class FixedClock : IBrokerClock { public DateTimeOffset UtcNow => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); }
    private sealed class OffsetClock(TimeSpan offset) : IBrokerClock { public DateTimeOffset UtcNow => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).Add(offset); }
    private sealed class NoOpAudit : IBrokerAuditWriter { public Task WriteAsync(BrokerAuditEntry entry, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class NoOpTelemetry : ITradeMindTelemetry { public ITradeMindActivity StartActivity(TelemetryOperation operation, TelemetryContext? context = null, IReadOnlyCollection<TelemetryLink>? links = null) => new Activity(); public void EnrichCurrent(TelemetryContext context) { } private sealed class Activity : ITradeMindActivity { public bool IsRecording => false; public string? TraceId => null; public string? SpanId => null; public void Dispose() { } public void RecordException(Exception exception) { } public void SetOutcome(TelemetryOutcome outcome) { } public void SetTag(string name, string? value) { } public void SetTag(string name, long? value) { } } }
    private sealed class NoOpMetrics : ITradeMindMetrics { public void IncrementCounter(string name, long value, MetricDimensions dimensions) { } public void RecordDuration(string name, TimeSpan duration, MetricDimensions dimensions) { } public void SetActiveExecutionSessions(long value) { } }
    private sealed class TestFactory(string connectionString) : IDbContextFactory<BrokerDbContext>
    {
        public BrokerDbContext CreateDbContext() => new(new DbContextOptionsBuilder<BrokerDbContext>().UseNpgsql(connectionString, options => options.MigrationsAssembly(typeof(BrokerDbContext).Assembly.FullName)).Options);
        public Task<BrokerDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
