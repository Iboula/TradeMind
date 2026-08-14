using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Application.Options;
using TradeMind.Brokers.Domain;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.Application.Tests;

public sealed class BrokerExecutionHardeningTests
{
    [Fact]
    public async Task Ambiguous_submit_reconciles_without_a_second_connector_invocation()
    {
        var connector = new StatefulConnector { TimeoutAfterPersist = true };
        var service = CreateService(connector, out _);

        var result = await service.SubmitOrderAsync(Context("tenant-a"), Command("tenant-a"), CancellationToken.None);
        var replay = await service.SubmitOrderAsync(Context("tenant-a"), Command("tenant-a"), CancellationToken.None);

        Assert.Equal(BrokerExecutionResultStatus.Accepted, result.Status);
        Assert.Equal(BrokerExecutionResultStatus.Replayed, replay.Status);
        Assert.NotNull(result.Execution);
        Assert.Equal(1, connector.SubmitCalls);
        Assert.Equal(1, connector.OrderQueryCalls);
    }

    [Fact]
    public async Task Tenant_scopes_do_not_collide_on_the_same_idempotency_key()
    {
        var connector = new StatefulConnector();
        var service = CreateService(connector, out _);

        var first = await service.SubmitOrderAsync(Context("tenant-a"), Command("tenant-a"), CancellationToken.None);
        var second = await service.SubmitOrderAsync(Context("tenant-b"), Command("tenant-b"), CancellationToken.None);

        Assert.Equal(BrokerExecutionResultStatus.Accepted, first.Status);
        Assert.Equal(BrokerExecutionResultStatus.Accepted, second.Status);
        Assert.Equal(2, connector.SubmitCalls);
    }

    [Fact]
    public async Task Reconciliation_failure_triggers_cleanup_and_preserves_original_failure()
    {
        var connector = new StatefulConnector();
        var service = CreateService(connector, out var safetyState);
        var reconciliation = new ThrowingReconciliationService();
        var lifecycle = CreateLifecycle(service, reconciliation, safetyState);

        var result = await lifecycle.ExecuteAndReconcileAsync(Context("tenant-a"), Command("tenant-a"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.CleanupCritical);
        Assert.Equal("RECONCILIATION_FAILED", result.OriginalFailure?.Code);
        Assert.NotNull(result.OriginalException);
        Assert.Null(result.Cleanup?.Error);
        Assert.Empty(await connector.GetPositionsAsync(Context("tenant-a"), new BrokerPositionQuery(), CancellationToken.None));
        Assert.True(safetyState.Snapshot.WritesAllowed);
    }

    [Fact]
    public async Task Deterministic_simulated_lifecycle_reconciles_closes_and_leaves_no_residuals()
    {
        var connector = new StatefulConnector();
        var service = CreateService(connector, out var safetyState);
        var lifecycle = CreateLifecycle(service, new ConsistentReconciliationService(), safetyState);

        var result = await lifecycle.ExecuteAndReconcileAsync(Context("tenant-a"), Command("tenant-a"), CancellationToken.None);
        var close = await service.ClosePositionAsync(Context("tenant-a"), connector.Descriptor.ConnectorId, new BrokerPositionCloseRequest(result.Execution.Execution!.PositionId!, null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Reconciliation!.IsConsistent);
        Assert.Null(result.Cleanup);
        Assert.Null(close.Error);
        Assert.Empty(await connector.GetPositionsAsync(Context("tenant-a"), new BrokerPositionQuery(), CancellationToken.None));
        Assert.Empty(await connector.GetOrdersAsync(Context("tenant-a"), new BrokerOrderQuery(Status: BrokerOrderStatus.Accepted), CancellationToken.None));
        Assert.True(safetyState.Snapshot.WritesAllowed);
    }

    [Fact]
    public async Task Cleanup_failure_is_critical_and_blocks_subsequent_writes()
    {
        var connector = new StatefulConnector { CleanupFails = true };
        var service = CreateService(connector, out var safetyState);
        var lifecycle = CreateLifecycle(service, new ThrowingReconciliationService(), safetyState);

        var failed = await lifecycle.ExecuteAndReconcileAsync(Context("tenant-a"), Command("tenant-a"), CancellationToken.None);
        var blocked = await service.SubmitOrderAsync(Context("tenant-a"), Command("tenant-a", "second"), CancellationToken.None);

        Assert.True(failed.CleanupCritical);
        Assert.Equal("CLEANUP_FAILED", safetyState.Snapshot.Code);
        Assert.Equal(BrokerExecutionResultStatus.Rejected, blocked.Status);
        Assert.Equal("CLEANUP_FAILED", blocked.Error?.Code);
        Assert.Equal(1, connector.SubmitCalls);
    }

    [Fact]
    public async Task Idempotency_conflict_and_execution_are_audited_with_bounded_metrics()
    {
        var connector = new StatefulConnector();
        var service = CreateService(connector, out _, out var audit, out var metrics);

        await service.SubmitOrderAsync(Context("tenant-a"), Command("tenant-a"), CancellationToken.None);
        var conflict = await service.SubmitOrderAsync(Context("tenant-a"), Command("tenant-a", "conflict"), CancellationToken.None);

        Assert.Equal(BrokerExecutionResultStatus.Conflict, conflict.Status);
        Assert.Contains(audit.Entries, entry => entry.Operation == "SubmitOrder" && entry.Outcome == "Accepted");
        Assert.Contains(audit.Entries, entry => entry.Operation == "SubmitOrder" && entry.Outcome == "Conflict");
        Assert.Contains(TelemetryMetricNames.BrokerIdempotencyConflicts, metrics.Counters);
    }

    private static BrokerExecutionService CreateService(StatefulConnector connector, out InMemoryBrokerExecutionSafetyState safetyState)
        => CreateService(connector, out safetyState, out _, out _);

    private static BrokerExecutionService CreateService(StatefulConnector connector, out InMemoryBrokerExecutionSafetyState safetyState, out RecordingAudit audit, out RecordingMetrics metrics)
    {
        var registry = new BrokerConnectorRegistry([connector]);
        var options = Microsoft.Extensions.Options.Options.Create(new BrokerOptions
        {
            Enabled = true,
            RequireTradingPlanAndRiskApproval = false,
            MaximumConcurrentExecutions = 2,
            OperationTimeoutSeconds = 1
        });
        var clock = new FixedClock();
        var authorization = new BrokerAuthorizationPolicy(options);
        safetyState = new InMemoryBrokerExecutionSafetyState();
        audit = new RecordingAudit();
        metrics = new RecordingMetrics();
        return new BrokerExecutionService(
            registry,
            new BrokerExecutionValidator(authorization, clock, options),
            authorization,
            new InMemoryBrokerIdempotencyStore(),
            new NoOpBrokerExecutionRecordWriter(),
            audit,
            clock,
            new NoOpTelemetry(),
            metrics,
            options,
            NullLogger<BrokerExecutionService>.Instance,
            safetyState);
    }

    private static BrokerExecutionLifecycleService CreateLifecycle(BrokerExecutionService service, IBrokerReconciliationService reconciliation, InMemoryBrokerExecutionSafetyState safetyState) =>
        new(service, reconciliation, safetyState, new RecordingAudit(), new FixedClock(), new NoOpTelemetry(), new NoOpMetrics(), NullLogger<BrokerExecutionLifecycleService>.Instance);

    private static BrokerExecutionContext Context(string tenant) => new(true, "actor", "User", tenant, "org", [BrokerPermissionNames.ExecuteSimulation], "session-" + tenant, "correlation-" + tenant, "simulation-confirmation");

    private static BrokerOrderExecutionCommand Command(string tenant, string? clientOrderId = null) =>
        new(new BrokerOrderRequest(
            new("execution-" + tenant + "-" + (clientOrderId ?? "one")), "session-" + tenant, new("simulation"), new("account"), clientOrderId ?? "same-client",
            "EURUSD", BrokerOrderSide.Buy, BrokerOrderType.Market, 1m, null, null, null, [], BrokerTimeInForce.Day, null, "same-key",
            "correlation-" + tenant, tenant, "org", "actor", null, null, null, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)), null, null, BrokerExecutionMode.Simulation);

    private sealed class FixedClock : IBrokerClock { public DateTimeOffset UtcNow => new(2026, 1, 1, 0, 0, 1, TimeSpan.Zero); }

    private sealed class ThrowingReconciliationService : IBrokerReconciliationService
    {
        public Task<BrokerReconciliationReport> ReconcileAsync(BrokerExecutionContext context, BrokerConnectorId connectorId, BrokerAccountId accountId, CancellationToken cancellationToken) => throw new InvalidOperationException("simulated reconciliation failure");
    }

    private sealed class ConsistentReconciliationService : IBrokerReconciliationService
    {
        public Task<BrokerReconciliationReport> ReconcileAsync(BrokerExecutionContext context, BrokerConnectorId connectorId, BrokerAccountId accountId, CancellationToken cancellationToken) =>
            Task.FromResult(new BrokerReconciliationReport(new("reconciliation-1"), connectorId, accountId, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, [], null));
    }

    private sealed class StatefulConnector : IBrokerConnector
    {
        private readonly Dictionary<string, BrokerOrder> orders = new(StringComparer.Ordinal);
        private readonly Dictionary<string, BrokerPosition> positions = new(StringComparer.Ordinal);
        public BrokerConnectorDescriptor Descriptor { get; } = new(new("simulation"), new("simulation"), "Deterministic simulation", "1", BrokerEnvironment.Test, BrokerExecutionMode.Simulation,
            BrokerCapability.ReadAccounts | BrokerCapability.ReadInstruments | BrokerCapability.ReadOrders | BrokerCapability.ReadPositions | BrokerCapability.SubmitMarketOrders | BrokerCapability.ClosePositions | BrokerCapability.Reconciliation,
            [BrokerAssetClass.Forex], [BrokerOrderType.Market], supportsReconciliation: true);
        public bool TimeoutAfterPersist { get; init; }
        public bool CleanupFails { get; init; }
        public int SubmitCalls => Volatile.Read(ref submitCalls);
        public int OrderQueryCalls => Volatile.Read(ref orderQueryCalls);
        private int submitCalls;
        private int orderQueryCalls;
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<BrokerHealthResult> GetHealthAsync(BrokerExecutionContext context, CancellationToken cancellationToken) => Task.FromResult(new BrokerHealthResult(new(Descriptor.ConnectorId, BrokerHealthStatus.Healthy, "ok", DateTimeOffset.UnixEpoch, TimeSpan.Zero), null));
        public Task<IReadOnlyList<BrokerAccount>> GetAccountsAsync(BrokerExecutionContext context, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrokerAccount>>([new(new("account"), Descriptor.ConnectorId, "account", "Simulation", "USD", BrokerAccountType.Demo, BrokerEnvironment.Test, 1000, 1000, 0, 1000, null, 100, true, false, BrokerAccountStatus.Active, DateTimeOffset.UnixEpoch)]);
        public Task<BrokerInstrumentSpecification?> GetInstrumentAsync(BrokerExecutionContext context, BrokerInstrumentQuery query, CancellationToken cancellationToken) => Task.FromResult<BrokerInstrumentSpecification?>(new("EURUSD", "EURUSD", BrokerAssetClass.Forex, "EUR", "USD", 5, 2, 0.00001m, 1m, 100000m, 0.01m, 100m, 0.01m, 0m, [], BrokerMarketStatus.Open, [BrokerOrderType.Market], DateTimeOffset.UnixEpoch));
        public async Task<BrokerOrderSubmissionResult> SubmitOrderAsync(BrokerExecutionContext context, BrokerOrderRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref submitCalls);
            var order = new BrokerOrder(new("order-" + request.ClientOrderId), request.ExecutionId, request.ConnectorId, request.AccountId, request.ClientOrderId, request.Instrument, request.Side, request.OrderType, request.Quantity, request.Quantity, null, 1m, BrokerOrderStatus.Filled, request.TimeInForce, request.CreatedAtUtc, request.CreatedAtUtc);
            var position = new BrokerPosition(new("position-" + request.ClientOrderId), request.ConnectorId, request.AccountId, request.Instrument, request.Side, request.Quantity, 1m, request.CreatedAtUtc, request.CreatedAtUtc);
            orders[order.OrderId.Value] = order;
            positions[position.PositionId.Value] = position;
            if (TimeoutAfterPersist) await release.Task.WaitAsync(cancellationToken);
            return new(order, new BrokerExecution(request.ExecutionId, order.OrderId, request.ConnectorId, request.AccountId, BrokerOrderStatus.Filled, request.Quantity, 1m, request.CreatedAtUtc, positionId: position.PositionId), null);
        }
        public Task<BrokerOrderModificationResult> ModifyOrderAsync(BrokerExecutionContext context, BrokerOrderModificationRequest request, CancellationToken cancellationToken) => Task.FromResult(new BrokerOrderModificationResult(null, null));
        public Task<BrokerOrderCancellationResult> CancelOrderAsync(BrokerExecutionContext context, BrokerOrderCancellationRequest request, CancellationToken cancellationToken) => Task.FromResult(new BrokerOrderCancellationResult(null, null));
        public Task<BrokerPositionCloseResult> ClosePositionAsync(BrokerExecutionContext context, BrokerPositionCloseRequest request, CancellationToken cancellationToken)
        {
            if (CleanupFails) return Task.FromResult(new BrokerPositionCloseResult(null, null, new BrokerError("CLEANUP_FAILED", BrokerErrorCategory.ReconciliationRequired, "cleanup failed", false, true, null, new(null, null, context.ExecutionSessionId), DateTimeOffset.UnixEpoch)));
            if (!positions.Remove(request.PositionId.Value, out var position)) return Task.FromResult(new BrokerPositionCloseResult(null, null, new BrokerError("POSITION_NOT_FOUND", BrokerErrorCategory.AccountUnavailable, "position missing", false, false, null, new(null, null, context.ExecutionSessionId), DateTimeOffset.UnixEpoch)));
            return Task.FromResult(new BrokerPositionCloseResult(position, new BrokerExecution(new("close-" + position.PositionId.Value), new("close-order-" + position.PositionId.Value), position.ConnectorId, position.AccountId, BrokerOrderStatus.Filled, position.Quantity, 1m, position.UpdatedAtUtc, positionId: position.PositionId), null));
        }
        public Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(BrokerExecutionContext context, BrokerOrderQuery query, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref orderQueryCalls);
            return Task.FromResult<IReadOnlyList<BrokerOrder>>(orders.Values
                .Where(item => query.ClientOrderId is null || item.ClientOrderId == query.ClientOrderId)
                .Where(item => query.Status is null || item.Status == query.Status)
                .ToArray());
        }
        public Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(BrokerExecutionContext context, BrokerPositionQuery query, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrokerPosition>>(positions.Values.Where(item => query.Instrument is null || item.Instrument == query.Instrument).ToArray());
    }

    private sealed class RecordingAudit : IBrokerAuditWriter
    {
        public List<BrokerAuditEntry> Entries { get; } = [];
        public Task WriteAsync(BrokerAuditEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return Task.CompletedTask; }
    }
    private sealed class RecordingMetrics : ITradeMindMetrics
    {
        public List<string> Counters { get; } = [];
        public void IncrementCounter(string name, long value, MetricDimensions dimensions) => Counters.Add(name);
        public void RecordDuration(string name, TimeSpan duration, MetricDimensions dimensions) { }
        public void SetActiveExecutionSessions(long value) { }
    }
    private sealed class NoOpTelemetry : ITradeMindTelemetry { public ITradeMindActivity StartActivity(TelemetryOperation operation, TelemetryContext? context = null, IReadOnlyCollection<TelemetryLink>? links = null) => new Activity(); public void EnrichCurrent(TelemetryContext context) { } private sealed class Activity : ITradeMindActivity { public bool IsRecording => false; public string? TraceId => null; public string? SpanId => null; public void Dispose() { } public void RecordException(Exception exception) { } public void SetOutcome(TelemetryOutcome outcome) { } public void SetTag(string name, string? value) { } public void SetTag(string name, long? value) { } } }
    private sealed class NoOpMetrics : ITradeMindMetrics { public void IncrementCounter(string name, long value, MetricDimensions dimensions) { } public void RecordDuration(string name, TimeSpan duration, MetricDimensions dimensions) { } public void SetActiveExecutionSessions(long value) { } }
}
