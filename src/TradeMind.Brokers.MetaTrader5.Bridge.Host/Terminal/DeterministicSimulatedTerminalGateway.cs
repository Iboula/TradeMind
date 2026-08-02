using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;

internal sealed class SimulatedMT5TerminalGateway(TimeProvider timeProvider) : IMT5TerminalGateway
{
    private readonly object sync = new();
    private readonly Dictionary<string, SimulatedOrder> orders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SimulatedPosition> positions = new(StringComparer.Ordinal);
    private bool started;

    public string TerminalVersion => "simulation-terminal-1.0";
    public bool IsAvailable => started;
    public MT5TerminalGatewaySnapshot Snapshot => new(
        started ? MT5TerminalConnectionState.Connected : MT5TerminalConnectionState.Disconnected,
        TerminalVersion,
        0,
        "simulation",
        "1.0",
        "Demo",
        true,
        false,
        TimeSpan.FromMilliseconds(1),
        started ? timeProvider.GetUtcNow() : null,
        null,
        0);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync) started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync) started = false;
        return Task.CompletedTask;
    }

    public Task<TerminalCommandResult> ExecuteAsync(TerminalCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (!started) return Task.FromResult(Failure("TERMINAL_UNAVAILABLE", "The simulated terminal is unavailable."));
            return Task.FromResult(command.Command switch
            {
                "heartbeat" => Success("HEARTBEAT", "Heartbeat acknowledged", new Dictionary<string, string>
                {
                    ["terminal_version"] = TerminalVersion,
                    ["heartbeat_at_utc"] = timeProvider.GetUtcNow().ToString("O")
                }),
                "get-accounts" => Success("ACCOUNTS", "Accounts returned", new Dictionary<string, string> { ["items"] = Json(new[] { new SimulatedAccount("mt5-demo-account", "MT5 Simulation", "USD", "Demo", "Test", 100000m, 100000m, 0m, 100000m, null, 100, true, false, "Active", DateTimeOffset.UnixEpoch) }) }),
                "get-instrument" => GetInstrument(command),
                "get-orders" => Success("ORDERS", "Orders returned", new Dictionary<string, string> { ["items"] = Json(orders.Values.OrderBy(item => item.OrderId, StringComparer.Ordinal).ToArray()) }),
                "get-positions" => Success("POSITIONS", "Positions returned", new Dictionary<string, string> { ["items"] = Json(positions.Values.OrderBy(item => item.PositionId, StringComparer.Ordinal).ToArray()) }),
                "submit-order" => Submit(command),
                "modify-order" => Modify(command),
                "cancel-order" => Cancel(command),
                "close-position" => Close(command),
                _ => Failure("UNKNOWN_COMMAND", "The requested operation is not supported.")
            });
        }
    }

    private TerminalCommandResult GetInstrument(TerminalCommand command)
    {
        var instrument = command.Fields.TryGetValue("instrument", out var value) ? value.ToUpperInvariant() : string.Empty;
        return instrument switch
        {
            "EURUSD" => Success("INSTRUMENT", "Instrument returned", new Dictionary<string, string> { ["instrument"] = Json(new SimulatedInstrument("EURUSD", "EURUSD", "Forex", "EUR", "USD", 5, 2, 0.00001m, 1m, 100000m, 0.01m, 100m, 0.01m, 0m, "Open", ["Market", "Limit", "Stop"], DateTimeOffset.UnixEpoch)) }),
            "GBPUSD" => Success("INSTRUMENT", "Instrument returned", new Dictionary<string, string> { ["instrument"] = Json(new SimulatedInstrument("GBPUSD", "GBPUSD", "Forex", "GBP", "USD", 5, 2, 0.00001m, 1m, 100000m, 0.01m, 100m, 0.01m, 0m, "Open", ["Market", "Limit", "Stop"], DateTimeOffset.UnixEpoch)) }),
            _ => Failure("INSTRUMENT_NOT_FOUND", "The instrument is not available in the demo terminal.")
        };
    }

    private TerminalCommandResult Submit(TerminalCommand command)
    {
        if (command.Fields.TryGetValue("reject", out var rejection) && !string.IsNullOrWhiteSpace(rejection)) return Failure("ORDER_REJECTED", "The demo terminal rejected the order.");
        var now = timeProvider.GetUtcNow();
        var clientOrderId = command.Fields["client_order_id"];
        var stable = StableId(clientOrderId);
        var quantity = decimal.Parse(command.Fields["quantity"], System.Globalization.CultureInfo.InvariantCulture);
        var orderType = command.Fields["order_type"];
        var market = orderType.Equals("Market", StringComparison.OrdinalIgnoreCase);
        var price = command.Fields.TryGetValue("requested_price", out var requested) && decimal.TryParse(requested, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 1.1m;
        var order = new SimulatedOrder($"mt5-order-{stable}", $"mt5-execution-{stable}", command.Fields["account_id"], clientOrderId, command.Fields["instrument"], command.Fields["side"], orderType, quantity, market ? quantity : 0, market ? null : price, market ? price : null, market ? "Filled" : "Pending", command.Fields["time_in_force"], now, now);
        orders[order.OrderId] = order;
        var fields = new Dictionary<string, string> { ["order"] = Json(order) };
        if (market)
        {
            var position = new SimulatedPosition($"mt5-position-{stable}", order.AccountId, order.Instrument, order.Side, order.Quantity, price, now, now);
            positions[position.PositionId] = position;
            fields["position"] = Json(position);
            fields["execution"] = Json(new SimulatedExecution(order.ExecutionId, order.OrderId, order.AccountId, "Filled", order.Quantity, price, now, position.PositionId));
        }
        return Success("ORDER_SUBMITTED", "The demo order was accepted.", fields);
    }

    private TerminalCommandResult Modify(TerminalCommand command)
    {
        if (!orders.TryGetValue(command.Fields["order_id"], out var order)) return Failure("ORDER_NOT_FOUND", "The order was not found.");
        var quantity = command.Fields.TryGetValue("quantity", out var quantityText) && decimal.TryParse(quantityText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsedQuantity) ? parsedQuantity : order.Quantity;
        var price = command.Fields.TryGetValue("limit_price", out var priceText) && decimal.TryParse(priceText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsedPrice) ? parsedPrice : order.RequestedPrice;
        var updated = order with { Quantity = quantity, RequestedPrice = price, UpdatedAtUtc = timeProvider.GetUtcNow() };
        orders[order.OrderId] = updated;
        return Success("ORDER_MODIFIED", "The demo order was modified.", new Dictionary<string, string> { ["order"] = Json(updated) });
    }

    private TerminalCommandResult Cancel(TerminalCommand command)
    {
        if (!orders.TryGetValue(command.Fields["order_id"], out var order)) return Failure("ORDER_NOT_FOUND", "The order was not found.");
        var updated = order with { Status = "Cancelled", UpdatedAtUtc = timeProvider.GetUtcNow() };
        orders[order.OrderId] = updated;
        return Success("ORDER_CANCELLED", "The demo order was cancelled.", new Dictionary<string, string> { ["order"] = Json(updated) });
    }

    private TerminalCommandResult Close(TerminalCommand command)
    {
        if (!positions.Remove(command.Fields["position_id"], out var position)) return Failure("POSITION_NOT_FOUND", "The position was not found.");
        var now = timeProvider.GetUtcNow();
        var executionId = $"mt5-close-execution-{StableId(position.PositionId)}";
        return Success("POSITION_CLOSED", "The demo position was closed.", new Dictionary<string, string>
        {
            ["position"] = Json(position),
            ["execution"] = Json(new SimulatedExecution(executionId, $"mt5-close-order-{StableId(position.PositionId)}", position.AccountId, "Filled", position.Quantity, position.AveragePrice, now, position.PositionId))
        });
    }

    private static TerminalCommandResult Success(string code, string message, IReadOnlyDictionary<string, string>? fields = null) => new(true, code, message, fields ?? new Dictionary<string, string>());
    private static TerminalCommandResult Failure(string code, string message) => new(false, code, message, new Dictionary<string, string>());
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static string StableId(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..16];

    private sealed record SimulatedAccount(string AccountId, string Name, string Currency, string AccountType, string Environment, decimal Balance, decimal Equity, decimal Margin, decimal FreeMargin, decimal? MarginLevel, int? Leverage, bool TradingEnabled, bool ReadOnly, string Status, DateTimeOffset UpdatedAtUtc);

    private sealed record SimulatedInstrument(string Instrument, string BrokerSymbol, string AssetClass, string BaseCurrency, string QuoteCurrency, int PricePrecision, int QuantityPrecision, decimal TickSize, decimal TickValue, decimal ContractSize, decimal MinimumQuantity, decimal MaximumQuantity, decimal QuantityStep, decimal MinimumStopDistance, string MarketStatus, IReadOnlyList<string> SupportedOrderTypes, DateTimeOffset UpdatedAtUtc);

    private sealed record SimulatedOrder(string OrderId, string ExecutionId, string AccountId, string ClientOrderId, string Instrument, string Side, string OrderType, decimal Quantity, decimal FilledQuantity, decimal? RequestedPrice, decimal? AverageFillPrice, string Status, string TimeInForce, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
    private sealed record SimulatedPosition(string PositionId, string AccountId, string Instrument, string Side, decimal Quantity, decimal AveragePrice, DateTimeOffset OpenedAtUtc, DateTimeOffset UpdatedAtUtc);
    private sealed record SimulatedExecution(string ExecutionId, string OrderId, string AccountId, string Status, decimal FilledQuantity, decimal? AveragePrice, DateTimeOffset OccurredAtUtc, string? PositionId);
}
