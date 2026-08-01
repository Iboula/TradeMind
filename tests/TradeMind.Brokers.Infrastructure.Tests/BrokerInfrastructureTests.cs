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

    private static BrokerExecutionContext Context() => new(true, "actor", "User", "tenant", "org", [BrokerPermissionNames.ExecuteSimulation], "session", "corr");
    private static BrokerOrderRequest Request(BrokerConnectorId connectorId) => new(new("exec"), "session", connectorId, new("account"), "client", "EURUSD", BrokerOrderSide.Buy, BrokerOrderType.Market, 1, null, null, null, [], BrokerTimeInForce.Day, null, "key", "corr", "tenant", "org", "actor", null, null, null, DateTimeOffset.UtcNow);
    private static BrokerExecutionResult Result() => new(new("exec"), BrokerExecutionResultStatus.Accepted, null, null, null, "SubmitOrder", DateTimeOffset.UtcNow);
    private sealed class FixedClock : IBrokerClock { public DateTimeOffset UtcNow => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); }
    private sealed class TestFactory(string connectionString) : IDbContextFactory<BrokerDbContext>
    {
        public BrokerDbContext CreateDbContext() => new(new DbContextOptionsBuilder<BrokerDbContext>().UseNpgsql(connectionString, options => options.MigrationsAssembly(typeof(BrokerDbContext).Assembly.FullName)).Options);
        public Task<BrokerDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
