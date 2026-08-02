using System.Security.Cryptography;
using System.Text;
using TradeMind.Brokers.MetaTrader5.Configuration;
using TradeMind.Brokers.MetaTrader5.Protocol;
using TradeMind.Brokers.MetaTrader5.Serialization;

namespace TradeMind.Brokers.MetaTrader5.Bridge;

public sealed class MT5SimulationBridge(MT5Options options, IMT5Serializer serializer, TimeProvider timeProvider) : IMT5Bridge
{
    private readonly object sync = new();
    private readonly Dictionary<string, MT5OrderPayload> orders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MT5PositionPayload> positions = new(StringComparer.Ordinal);
    private bool opened;

    public string BridgeVersion => "simulation-1.0";

    public Task OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.Mode.Equals("Live", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Live mode is disabled.");
        opened = true;
        return Task.CompletedTask;
    }

    public Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!opened) throw new InvalidOperationException("The MT5 bridge is not open.");
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        opened = false;
        return Task.CompletedTask;
    }

    public Task<string> SendAsync(string payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!opened) throw new InvalidOperationException("The MT5 bridge is not open.");
        var request = serializer.DeserializeRequest(payload);
        var response = Handle(request);
        return Task.FromResult(serializer.SerializeResponse(response));
    }

    private MT5Response Handle(MT5Request request)
    {
        lock (sync)
        {
            return request.Command switch
            {
                "heartbeat" => Success("HEARTBEAT", "Heartbeat acknowledged", new Dictionary<string, string>
                {
                    ["bridge_version"] = BridgeVersion,
                    ["protocol_version"] = "1.0",
                    ["terminal_version"] = "simulation-terminal",
                    ["heartbeat_at_utc"] = timeProvider.GetUtcNow().ToString("O")
                }),
                "get-accounts" => Success("ACCOUNTS", "Accounts returned", new Dictionary<string, string>
                {
                    ["items"] = MT5JsonSerializer.SerializePayload(new[] { DefaultAccount(request.Fields.TryGetValue("account_id", out var id) ? id : "mt5-demo-account") })
                }),
                "get-instrument" => Instrument(request),
                "get-orders" => Success("ORDERS", "Orders returned", new Dictionary<string, string>
                {
                    ["items"] = MT5JsonSerializer.SerializePayload(orders.Values.OrderBy(item => item.OrderId, StringComparer.Ordinal).ToArray())
                }),
                "get-positions" => Success("POSITIONS", "Positions returned", new Dictionary<string, string>
                {
                    ["items"] = MT5JsonSerializer.SerializePayload(positions.Values.OrderBy(item => item.PositionId, StringComparer.Ordinal).ToArray())
                }),
                "submit-order" => Submit(request),
                "modify-order" => Modify(request),
                "cancel-order" => Cancel(request),
                "close-position" => Close(request),
                _ => Failure("UNKNOWN_COMMAND", "The MT5 command is not supported.")
            };
        }
    }

    private MT5Response Instrument(MT5Request request)
    {
        var instrument = request.Fields.TryGetValue("instrument", out var value) ? value : "";
        return instrument.ToUpperInvariant() switch
        {
            "EURUSD" => Success("INSTRUMENT", "Instrument returned", new Dictionary<string, string> { ["instrument"] = MT5JsonSerializer.SerializePayload(DefaultInstrument("EURUSD", "EUR", "USD")) }),
            "GBPUSD" => Success("INSTRUMENT", "Instrument returned", new Dictionary<string, string> { ["instrument"] = MT5JsonSerializer.SerializePayload(DefaultInstrument("GBPUSD", "GBP", "USD")) }),
            _ => Failure("INSTRUMENT_NOT_FOUND", "The instrument is not available in the simulation terminal.")
        };
    }

    private MT5Response Submit(MT5Request request)
    {
        if (request.Fields.TryGetValue("reject", out var reason) && !string.IsNullOrWhiteSpace(reason)) return Failure("ORDER_REJECTED", "The simulated terminal rejected the order.");
        var now = timeProvider.GetUtcNow();
        var executionId = "mt5-execution-" + StableId(request.Fields["client_order_id"]);
        var orderId = "mt5-order-" + StableId(request.Fields["client_order_id"]);
        var orderType = request.Fields["order_type"];
        var filled = string.Equals(orderType, "Market", StringComparison.OrdinalIgnoreCase);
        var status = filled ? "Filled" : "Pending";
        var price = request.Fields.TryGetValue("requested_price", out var requested) && decimal.TryParse(requested, out var requestedPrice) ? requestedPrice : 1.10000m;
        var order = new MT5OrderPayload(orderId, executionId, request.Fields["account_id"], request.Fields["client_order_id"], request.Fields["instrument"], request.Fields["side"], orderType, decimal.Parse(request.Fields["quantity"]), filled ? decimal.Parse(request.Fields["quantity"]) : 0, filled ? null : price, filled ? price : null, status, request.Fields["time_in_force"], now, now);
        orders[orderId] = order;
        var fields = new Dictionary<string, string> { ["order"] = MT5JsonSerializer.SerializePayload(order) };
        if (filled)
        {
            var positionId = "mt5-position-" + StableId(request.Fields["client_order_id"]);
            var position = new MT5PositionPayload(positionId, order.AccountId, order.Instrument, order.Side, order.Quantity, price, now, now);
            positions[positionId] = position;
            fields["position"] = MT5JsonSerializer.SerializePayload(position);
            fields["execution"] = MT5JsonSerializer.SerializePayload(new MT5ExecutionPayload(executionId, orderId, order.AccountId, status, order.Quantity, price, now, positionId));
        }
        return Success("ORDER_SUBMITTED", "The simulated order was accepted.", fields);
    }

    private MT5Response Modify(MT5Request request)
    {
        if (!orders.TryGetValue(request.Fields["order_id"], out var order)) return Failure("ORDER_NOT_FOUND", "The order was not found.");
        var updated = order with
        {
            Quantity = request.Fields.TryGetValue("quantity", out var quantity) && decimal.TryParse(quantity, out var parsedQuantity) ? parsedQuantity : order.Quantity,
            RequestedPrice = request.Fields.TryGetValue("limit_price", out var price) && decimal.TryParse(price, out var parsedPrice) ? parsedPrice : order.RequestedPrice,
            UpdatedAtUtc = timeProvider.GetUtcNow()
        };
        orders[order.OrderId] = updated;
        return Success("ORDER_MODIFIED", "The simulated order was modified.", new Dictionary<string, string> { ["order"] = MT5JsonSerializer.SerializePayload(updated) });
    }

    private MT5Response Cancel(MT5Request request)
    {
        if (!orders.TryGetValue(request.Fields["order_id"], out var order)) return Failure("ORDER_NOT_FOUND", "The order was not found.");
        var updated = order with { Status = "Cancelled", UpdatedAtUtc = timeProvider.GetUtcNow() };
        orders[order.OrderId] = updated;
        return Success("ORDER_CANCELLED", "The simulated order was cancelled.", new Dictionary<string, string> { ["order"] = MT5JsonSerializer.SerializePayload(updated) });
    }

    private MT5Response Close(MT5Request request)
    {
        if (!positions.Remove(request.Fields["position_id"], out var position)) return Failure("POSITION_NOT_FOUND", "The position was not found.");
        var executionId = "mt5-close-execution-" + StableId(position.PositionId);
        var orderId = "mt5-close-order-" + StableId(position.PositionId);
        var execution = new MT5ExecutionPayload(executionId, orderId, position.AccountId, "Filled", position.Quantity, position.AveragePrice, timeProvider.GetUtcNow(), position.PositionId);
        return Success("POSITION_CLOSED", "The simulated position was closed.", new Dictionary<string, string>
        {
            ["position"] = MT5JsonSerializer.SerializePayload(position),
            ["execution"] = MT5JsonSerializer.SerializePayload(execution)
        });
    }

    private static MT5AccountPayload DefaultAccount(string accountId) => new(accountId, "MT5 Simulation", "USD", "Demo", "Test", 100000m, 100000m, 0m, 100000m, null, 100, true, false, "Active", DateTimeOffset.UnixEpoch);

    private static MT5InstrumentPayload DefaultInstrument(string instrument, string baseCurrency, string quoteCurrency) => new(instrument, instrument, "Forex", baseCurrency, quoteCurrency, 5, 2, 0.00001m, 1m, 100000m, 0.01m, 100m, 0.01m, 0m, "Open", ["Market", "Limit", "Stop"], DateTimeOffset.UnixEpoch);

    private static MT5Response Success(string code, string message, IReadOnlyDictionary<string, string>? fields = null) => new(true, code, message, fields);
    private static MT5Response Failure(string code, string message) => new(false, code, message);
    private static string StableId(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..16];
}
