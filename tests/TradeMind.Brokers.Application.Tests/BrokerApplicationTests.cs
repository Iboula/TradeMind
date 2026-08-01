using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Application.Options;
using TradeMind.Brokers.Domain;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.Application.Tests;

public sealed class BrokerApplicationTests
{
    [Fact]
    public void Registry_is_deterministic_and_rejects_duplicates()
    {
        var first = new FakeConnector("b");
        var second = new FakeConnector("a");
        var registry = new BrokerConnectorRegistry([first, second]);
        Assert.Equal(["a", "b"], registry.Descriptors.Select(item => item.ConnectorId.Value));
        Assert.Throws<InvalidOperationException>(() => new BrokerConnectorRegistry([first, new FakeConnector("b")]));
    }

    [Fact]
    public void Authorization_rejects_live_by_default()
    {
        var policy = new BrokerAuthorizationPolicy(Microsoft.Extensions.Options.Options.Create(new BrokerOptions { Enabled = true }));
        var context = Context(BrokerPermissionNames.ExecuteLive, confirmation: "confirmed");
        var result = policy.Evaluate(context, BrokerExecutionMode.Live, "SubmitOrder");
        Assert.False(result.IsAllowed);
        Assert.Equal("LIVE_DISABLED", result.Code);
    }

    [Fact]
    public async Task Validation_failure_does_not_invoke_connector()
    {
        var connector = new FakeConnector("simulation");
        var service = CreateService(connector, requirePlan: false);
        var result = await service.SubmitOrderAsync(new BrokerExecutionContext(false, null, null, null, null, [], "session", "corr"), Command(), CancellationToken.None);
        Assert.Equal(BrokerExecutionResultStatus.Rejected, result.Status);
        Assert.Equal(0, connector.SubmitCalls);
    }

    [Fact]
    public async Task Tenant_mismatch_is_rejected_before_connector_invocation()
    {
        var connector = new FakeConnector("simulation");
        var service = CreateService(connector, requirePlan: false);
        var result = await service.SubmitOrderAsync(Context(BrokerPermissionNames.ExecuteSimulation, tenant: "other"), Command(), CancellationToken.None);
        Assert.Equal(BrokerExecutionResultStatus.Rejected, result.Status);
        Assert.Equal(BrokerErrorCategory.TenantMismatch, result.Error!.Category);
        Assert.Equal(0, connector.SubmitCalls);
    }

    [Fact]
    public async Task Same_idempotency_key_replays_without_second_connector_submission()
    {
        var connector = new FakeConnector("simulation");
        var service = CreateService(connector, requirePlan: false);
        var context = Context(BrokerPermissionNames.ExecuteSimulation);
        var first = await service.SubmitOrderAsync(context, Command(), CancellationToken.None);
        var second = await service.SubmitOrderAsync(context, Command(), CancellationToken.None);
        Assert.Equal(BrokerExecutionResultStatus.Accepted, first.Status);
        Assert.Equal(BrokerExecutionResultStatus.Replayed, second.Status);
        Assert.Equal(1, connector.SubmitCalls);
    }

    [Fact]
    public async Task Same_idempotency_key_with_different_request_is_conflict()
    {
        var connector = new FakeConnector("simulation");
        var service = CreateService(connector, requirePlan: false);
        var context = Context(BrokerPermissionNames.ExecuteSimulation);
        await service.SubmitOrderAsync(context, Command(), CancellationToken.None);
        var conflict = await service.SubmitOrderAsync(context, Command(clientOrderId: "different"), CancellationToken.None);
        Assert.Equal(BrokerExecutionResultStatus.Conflict, conflict.Status);
        Assert.Equal(1, connector.SubmitCalls);
    }

    [Fact]
    public async Task Cancellation_propagates_and_connector_is_not_reported_as_success()
    {
        var connector = new FakeConnector("simulation") { Cancel = true };
        var service = CreateService(connector, requirePlan: false);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SubmitOrderAsync(Context(BrokerPermissionNames.ExecuteSimulation), Command(), new CancellationToken(true)));
    }

    [Fact]
    public async Task Concurrent_same_key_execution_invokes_operation_once()
    {
        var store = new InMemoryBrokerIdempotencyStore();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task<BrokerExecutionResult> Operation(CancellationToken cancellationToken) => ExecuteAsync(cancellationToken);
        async Task<BrokerExecutionResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            started.SetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return new BrokerExecutionResult(new("exec"), BrokerExecutionResultStatus.Accepted, null, null, null, "SubmitOrder", DateTimeOffset.UtcNow);
        }

        var owner = store.ExecuteAsync("key", "hash", Operation, CancellationToken.None);
        await started.Task;
        var follower = store.ExecuteAsync("key", "hash", Operation, CancellationToken.None);
        release.SetResult(true);
        var results = await Task.WhenAll(owner, follower);

        Assert.Equal(1, calls);
        Assert.Contains(results, item => item.IsReplay);
    }

    private static BrokerExecutionService CreateService(FakeConnector connector, bool requirePlan)
    {
        var registry = new BrokerConnectorRegistry([connector]);
        var options = Microsoft.Extensions.Options.Options.Create(new BrokerOptions { Enabled = true, RequireTradingPlanAndRiskApproval = requirePlan, AllowDemoExecution = false, MaximumConcurrentExecutions = 2 });
        var clock = new FixedClock();
        var authorization = new BrokerAuthorizationPolicy(options);
        return new BrokerExecutionService(registry, new BrokerExecutionValidator(authorization, clock, options), authorization, new InMemoryBrokerIdempotencyStore(), new NoOpBrokerExecutionRecordWriter(), new RecordingAudit(), clock, new NoOpTelemetry(), new NoOpMetrics(), options, NullLogger<BrokerExecutionService>.Instance);
    }

    private static BrokerExecutionContext Context(string permission, string? confirmation = null, string tenant = "tenant") => new(true, "actor", "User", tenant, "org", [permission], "session", "corr", confirmation);
    private static BrokerOrderExecutionCommand Command(string clientOrderId = "client") => new(new BrokerOrderRequest(new("exec-1"), "session", new("simulation"), new("account"), clientOrderId, "EURUSD", BrokerOrderSide.Buy, BrokerOrderType.Market, 1, null, null, null, [], BrokerTimeInForce.Day, null, "same-key", "corr", "tenant", "org", "actor", null, null, null, DateTimeOffset.UtcNow), null, null);

    private sealed class FixedClock : IBrokerClock { public DateTimeOffset UtcNow => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); }
    private sealed class RecordingAudit : IBrokerAuditWriter { public Task WriteAsync(BrokerAuditEntry entry, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class NoOpTelemetry : ITradeMindTelemetry { public ITradeMindActivity StartActivity(TelemetryOperation operation, TelemetryContext? context = null, IReadOnlyCollection<TelemetryLink>? links = null) => new ActivityStub(); public void EnrichCurrent(TelemetryContext context) { } }
    private sealed class ActivityStub : ITradeMindActivity { public bool IsRecording => false; public string? TraceId => null; public string? SpanId => null; public void Dispose() { } public void RecordException(Exception exception) { } public void SetOutcome(TelemetryOutcome outcome) { } public void SetTag(string name, string? value) { } public void SetTag(string name, long? value) { } }
    private sealed class NoOpMetrics : ITradeMindMetrics { public void IncrementCounter(string name, long value, MetricDimensions dimensions) { } public void RecordDuration(string name, TimeSpan duration, MetricDimensions dimensions) { } public void SetActiveExecutionSessions(long value) { } }

    private sealed class FakeConnector : IBrokerConnector
    {
        public FakeConnector(string id)
        {
            Descriptor = new BrokerConnectorDescriptor(new(id), new("test"), "Test", "1", BrokerEnvironment.Test, BrokerExecutionMode.Simulation, BrokerCapability.ReadAccounts | BrokerCapability.ReadInstruments | BrokerCapability.SubmitMarketOrders, [BrokerAssetClass.Forex], [BrokerOrderType.Market]);
        }
        public BrokerConnectorDescriptor Descriptor { get; }
        public int SubmitCalls { get; private set; }
        public bool Cancel { get; init; }
        public Task<BrokerHealthResult> GetHealthAsync(BrokerExecutionContext context, CancellationToken cancellationToken) => Task.FromResult(new BrokerHealthResult(new BrokerHealth(Descriptor.ConnectorId, BrokerHealthStatus.Healthy, "ok", DateTimeOffset.UtcNow, TimeSpan.Zero), null));
        public Task<IReadOnlyList<BrokerAccount>> GetAccountsAsync(BrokerExecutionContext context, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrokerAccount>>([new BrokerAccount(new("account"), Descriptor.ConnectorId, "external", "Test", "USD", BrokerAccountType.Demo, BrokerEnvironment.Test, 1000, 1000, 0, 1000, null, 100, true, false, BrokerAccountStatus.Active, DateTimeOffset.UtcNow)]);
        public Task<BrokerInstrumentSpecification?> GetInstrumentAsync(BrokerExecutionContext context, BrokerInstrumentQuery query, CancellationToken cancellationToken) => Task.FromResult<BrokerInstrumentSpecification?>(new BrokerInstrumentSpecification("EURUSD", "EURUSD", BrokerAssetClass.Forex, "EUR", "USD", 5, 2, 0.01m, 1, 100000, 0.01m, 100, 0.01m, 0, [], BrokerMarketStatus.Open, [BrokerOrderType.Market], DateTimeOffset.UtcNow));
        public Task<BrokerOrderSubmissionResult> SubmitOrderAsync(BrokerExecutionContext context, BrokerOrderRequest request, CancellationToken cancellationToken)
        {
            SubmitCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            var order = new BrokerOrder(new("order"), request.ExecutionId, request.ConnectorId, request.AccountId, request.ClientOrderId, request.Instrument, request.Side, request.OrderType, request.Quantity, request.Quantity, null, 1, BrokerOrderStatus.Filled, request.TimeInForce, request.CreatedAtUtc, DateTimeOffset.UtcNow);
            return Task.FromResult(new BrokerOrderSubmissionResult(order, new BrokerExecution(request.ExecutionId, order.OrderId, request.ConnectorId, request.AccountId, BrokerOrderStatus.Filled, request.Quantity, 1, DateTimeOffset.UtcNow), null));
        }
        public Task<BrokerOrderModificationResult> ModifyOrderAsync(BrokerExecutionContext context, BrokerOrderModificationRequest request, CancellationToken cancellationToken) => Task.FromResult(new BrokerOrderModificationResult(null, null));
        public Task<BrokerOrderCancellationResult> CancelOrderAsync(BrokerExecutionContext context, BrokerOrderCancellationRequest request, CancellationToken cancellationToken) => Task.FromResult(new BrokerOrderCancellationResult(null, null));
        public Task<BrokerPositionCloseResult> ClosePositionAsync(BrokerExecutionContext context, BrokerPositionCloseRequest request, CancellationToken cancellationToken) => Task.FromResult(new BrokerPositionCloseResult(null, null, null));
        public Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(BrokerExecutionContext context, BrokerOrderQuery query, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrokerOrder>>([]);
        public Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(BrokerExecutionContext context, BrokerPositionQuery query, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrokerPosition>>([]);
    }
}
