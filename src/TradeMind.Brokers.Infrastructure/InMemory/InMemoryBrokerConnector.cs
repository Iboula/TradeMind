using System.Security.Cryptography;
using System.Text;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Infrastructure.InMemory;

public sealed class InMemoryBrokerConnector(InMemoryBrokerState state, IBrokerClock clock) : IBrokerConnector
{
    public BrokerConnectorDescriptor Descriptor { get; } = new(
        new BrokerConnectorId("in-memory"),
        new BrokerId("trademind-simulation"),
        "TradeMind deterministic simulation connector",
        "1.0.0",
        BrokerEnvironment.Development,
        BrokerExecutionMode.Simulation,
        BrokerCapability.ReadAccounts | BrokerCapability.ReadInstruments | BrokerCapability.ReadOrders | BrokerCapability.ReadPositions |
        BrokerCapability.SubmitMarketOrders | BrokerCapability.SubmitLimitOrders | BrokerCapability.SubmitStopOrders |
        BrokerCapability.ModifyOrders | BrokerCapability.CancelOrders | BrokerCapability.ClosePositions |
        BrokerCapability.StopLoss | BrokerCapability.TakeProfit | BrokerCapability.Reconciliation | BrokerCapability.IdempotentClientOrderIds,
        [BrokerAssetClass.Forex, BrokerAssetClass.Equity, BrokerAssetClass.Crypto],
        [BrokerOrderType.Market, BrokerOrderType.Limit, BrokerOrderType.Stop, BrokerOrderType.StopLimit],
        supportsReconciliation: true);

    public Task<BrokerHealthResult> GetHealthAsync(BrokerExecutionContext context, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return Task.FromResult(new BrokerHealthResult(new BrokerHealth(Descriptor.ConnectorId, BrokerHealthStatus.Healthy, "Simulation connector is available.", now, TimeSpan.Zero), null));
    }

    public Task<IReadOnlyList<BrokerAccount>> GetAccountsAsync(BrokerExecutionContext context, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BrokerAccount>>(state.Accounts.Values.Where(item => IsTenantAllowed(context, item.AccountId)).OrderBy(item => item.AccountId.Value, StringComparer.Ordinal).ToArray());

    public Task<BrokerInstrumentSpecification?> GetInstrumentAsync(BrokerExecutionContext context, BrokerInstrumentQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(state.Instruments.TryGetValue(query.Instrument, out var specification) ? specification : null);

    public Task<BrokerOrderSubmissionResult> SubmitOrderAsync(BrokerExecutionContext context, BrokerOrderRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (state.Rejections.TryGetValue("SubmitOrder", out var rejection)) return Task.FromResult(new BrokerOrderSubmissionResult(null, null, rejection));
        var now = clock.UtcNow < request.CreatedAtUtc ? request.CreatedAtUtc : clock.UtcNow;
        var orderId = new BrokerOrderId("ord-" + StableId(request.ClientOrderId));
        if (state.Orders.TryGetValue(orderId.Value, out var existing)) return Task.FromResult(new BrokerOrderSubmissionResult(existing, null, new BrokerError("DUPLICATE_ORDER", BrokerErrorCategory.DuplicateRequest, "The client order id already exists.", false, false, null, new BrokerTraceReference(request.CorrelationId, null, request.ExecutionSessionId), now)));
        var filled = request.OrderType == BrokerOrderType.Market;
        var status = filled ? BrokerOrderStatus.Filled : BrokerOrderStatus.Accepted;
        var price = request.RequestedPrice ?? ResolvePrice(request.Instrument);
        var order = new BrokerOrder(orderId, request.ExecutionId, request.ConnectorId, request.AccountId, request.ClientOrderId, request.Instrument, request.Side, request.OrderType, request.Quantity, filled ? request.Quantity : 0, request.RequestedPrice, filled ? price : null, status, request.TimeInForce, request.CreatedAtUtc, now);
        state.Orders[orderId.Value] = order;
        BrokerExecution? execution = null;
        if (filled)
        {
            var positionId = new BrokerPositionId("pos-" + StableId($"{request.AccountId.Value}|{request.Instrument}|{request.Side}"));
            var position = new BrokerPosition(positionId, request.ConnectorId, request.AccountId, request.Instrument, request.Side, request.Quantity, price, now, now);
            state.Positions[positionId.Value] = position;
            execution = new BrokerExecution(request.ExecutionId, orderId, request.ConnectorId, request.AccountId, BrokerOrderStatus.Filled, request.Quantity, price, now, null, positionId);
        }

        return Task.FromResult(new BrokerOrderSubmissionResult(order, execution, null));
    }

    public Task<BrokerOrderModificationResult> ModifyOrderAsync(BrokerExecutionContext context, BrokerOrderModificationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!state.Orders.TryGetValue(request.OrderId.Value, out var order)) return Task.FromResult(new BrokerOrderModificationResult(null, Error("ORDER_NOT_FOUND", BrokerErrorCategory.AccountUnavailable, "The order was not found.")));
        var quantity = request.Quantity ?? order.Quantity;
        if (quantity < order.FilledQuantity || quantity <= 0) return Task.FromResult(new BrokerOrderModificationResult(null, Error("INVALID_QUANTITY", BrokerErrorCategory.InvalidQuantity, "The modified quantity is invalid.")));
        var updated = new BrokerOrder(order.OrderId, order.ExecutionId, order.ConnectorId, order.AccountId, order.ClientOrderId, order.Instrument, order.Side, order.OrderType, quantity, order.FilledQuantity, request.LimitPrice ?? order.RequestedPrice, order.AverageFillPrice, order.Status, order.TimeInForce, order.CreatedAtUtc, clock.UtcNow);
        state.Orders[order.OrderId.Value] = updated;
        return Task.FromResult(new BrokerOrderModificationResult(updated, null));
    }

    public Task<BrokerOrderCancellationResult> CancelOrderAsync(BrokerExecutionContext context, BrokerOrderCancellationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!state.Orders.TryGetValue(request.OrderId.Value, out var order)) return Task.FromResult(new BrokerOrderCancellationResult(null, Error("ORDER_NOT_FOUND", BrokerErrorCategory.AccountUnavailable, "The order was not found.")));
        if (order.Status is BrokerOrderStatus.Filled or BrokerOrderStatus.Cancelled) return Task.FromResult(new BrokerOrderCancellationResult(order, Error("ORDER_NOT_CANCELLABLE", BrokerErrorCategory.Conflict, "The order cannot be cancelled in its current state.")));
        var cancelled = new BrokerOrder(order.OrderId, order.ExecutionId, order.ConnectorId, order.AccountId, order.ClientOrderId, order.Instrument, order.Side, order.OrderType, order.Quantity, order.FilledQuantity, order.RequestedPrice, order.AverageFillPrice, BrokerOrderStatus.Cancelled, order.TimeInForce, order.CreatedAtUtc, clock.UtcNow);
        state.Orders[order.OrderId.Value] = cancelled;
        return Task.FromResult(new BrokerOrderCancellationResult(cancelled, null));
    }

    public Task<BrokerPositionCloseResult> ClosePositionAsync(BrokerExecutionContext context, BrokerPositionCloseRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!state.Positions.TryRemove(request.PositionId.Value, out var position)) return Task.FromResult(new BrokerPositionCloseResult(null, null, Error("POSITION_NOT_FOUND", BrokerErrorCategory.AccountUnavailable, "The position was not found.")));
        var execution = new BrokerExecution(new BrokerExecutionId("close-" + StableId(request.PositionId.Value)), new BrokerOrderId("close-order-" + StableId(request.PositionId.Value)), position.ConnectorId, position.AccountId, BrokerOrderStatus.Filled, request.Quantity ?? position.Quantity, ResolvePrice(position.Instrument), clock.UtcNow, null, position.PositionId);
        return Task.FromResult(new BrokerPositionCloseResult(position, execution, null));
    }

    public Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(BrokerExecutionContext context, BrokerOrderQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BrokerOrder>>(state.Orders.Values.Where(item => IsTenantAllowed(context, item.AccountId)).Where(item => query.ClientOrderId is null || item.ClientOrderId == query.ClientOrderId).Where(item => query.Status is null || item.Status == query.Status).OrderBy(item => item.OrderId.Value, StringComparer.Ordinal).ToArray());

    public Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(BrokerExecutionContext context, BrokerPositionQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BrokerPosition>>(state.Positions.Values.Where(item => IsTenantAllowed(context, item.AccountId)).Where(item => query.Instrument is null || item.Instrument == query.Instrument).OrderBy(item => item.PositionId.Value, StringComparer.Ordinal).ToArray());

    private decimal ResolvePrice(string instrument) => state.Instruments.TryGetValue(instrument, out var specification) ? specification.TickSize : 1m;
    private bool IsTenantAllowed(BrokerExecutionContext context, BrokerAccountId accountId) => !state.Accounts.TryGetValue(accountId.Value, out var account) || !account.Metadata.TryGetValue("tenant_id", out var tenant) || string.Equals(tenant, context.TenantId, StringComparison.Ordinal);
    private BrokerError Error(string code, BrokerErrorCategory category, string message) => new(code, category, message, false, false, null, new BrokerTraceReference(null, null, null), clock.UtcNow);
    private static string StableId(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..16];
}
