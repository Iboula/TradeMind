using System.Globalization;
using TradeMind.Brokers.Domain;
using TradeMind.Brokers.MetaTrader5.Protocol;
using TradeMind.Brokers.MetaTrader5.Serialization;

namespace TradeMind.Brokers.MetaTrader5.Mapping;

internal static class MT5Mapping
{
    public static T Enum<T>(string value, T fallback = default) where T : struct, System.Enum =>
        System.Enum.TryParse<T>(value, true, out var parsed) ? parsed : fallback;

    public static DateTimeOffset Utc(string value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
        ? parsed
        : DateTimeOffset.UnixEpoch;

    public static decimal Decimal(string value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
}

internal static class MT5AccountMapper
{
    public static BrokerAccount Map(MT5AccountPayload payload, BrokerConnectorId connectorId, string? tenantId) => new(
        new BrokerAccountId(payload.AccountId), connectorId, payload.AccountId, payload.Name, payload.Currency,
        MT5Mapping.Enum(payload.AccountType, BrokerAccountType.Unknown), MT5Mapping.Enum(payload.Environment, BrokerEnvironment.Test),
        payload.Balance, payload.Equity, payload.Margin, payload.FreeMargin, payload.MarginLevel, payload.Leverage,
        payload.TradingEnabled, payload.ReadOnly, MT5Mapping.Enum(payload.Status, BrokerAccountStatus.Unknown), payload.UpdatedAtUtc,
        tenantId is null ? null : new Dictionary<string, string> { ["tenant_id"] = tenantId });
}

internal static class MT5OrderMapper
{
    public static BrokerOrder Map(MT5OrderPayload payload, BrokerConnectorId connectorId) => new(
        new BrokerOrderId(payload.OrderId), new BrokerExecutionId(payload.ExecutionId), connectorId, new BrokerAccountId(payload.AccountId),
        payload.ClientOrderId, payload.Instrument, MT5Mapping.Enum(payload.Side, BrokerOrderSide.Buy), MT5Mapping.Enum(payload.OrderType, BrokerOrderType.Market),
        payload.Quantity, payload.FilledQuantity, payload.RequestedPrice, payload.AverageFillPrice,
        MT5Mapping.Enum(payload.Status, BrokerOrderStatus.Unknown), MT5Mapping.Enum(payload.TimeInForce, BrokerTimeInForce.Day), payload.CreatedAtUtc, payload.UpdatedAtUtc);
}

internal static class MT5PositionMapper
{
    public static BrokerPosition Map(MT5PositionPayload payload, BrokerConnectorId connectorId) => new(
        new BrokerPositionId(payload.PositionId), connectorId, new BrokerAccountId(payload.AccountId), payload.Instrument,
        MT5Mapping.Enum(payload.Side, BrokerOrderSide.Buy), payload.Quantity, payload.AveragePrice, payload.OpenedAtUtc, payload.UpdatedAtUtc);
}

internal static class MT5ExecutionMapper
{
    public static BrokerExecution Map(MT5ExecutionPayload payload, BrokerConnectorId connectorId) => new(
        new BrokerExecutionId(payload.ExecutionId), new BrokerOrderId(payload.OrderId), connectorId, new BrokerAccountId(payload.AccountId),
        MT5Mapping.Enum(payload.Status, BrokerOrderStatus.Unknown), payload.FilledQuantity, payload.AveragePrice, payload.OccurredAtUtc,
        positionId: payload.PositionId is null ? null : new BrokerPositionId(payload.PositionId));
}

internal static class MT5InstrumentMapper
{
    public static BrokerInstrumentSpecification Map(MT5InstrumentPayload payload) => new(
        payload.Instrument, payload.BrokerSymbol, MT5Mapping.Enum(payload.AssetClass, BrokerAssetClass.Unknown), payload.BaseCurrency, payload.QuoteCurrency,
        payload.PricePrecision, payload.QuantityPrecision, payload.TickSize, payload.TickValue, payload.ContractSize, payload.MinimumQuantity,
        payload.MaximumQuantity, payload.QuantityStep, payload.MinimumStopDistance, [], MT5Mapping.Enum(payload.MarketStatus, BrokerMarketStatus.Unknown),
        payload.SupportedOrderTypes.Select(item => MT5Mapping.Enum(item, BrokerOrderType.Market)), payload.UpdatedAtUtc);
}

internal static class MT5CapabilityMapper
{
    public static BrokerCapability ToCapabilities(bool demoEnabled) =>
        BrokerCapability.ReadAccounts |
        BrokerCapability.ReadInstruments |
        BrokerCapability.ReadOrders |
        BrokerCapability.ReadPositions |
        BrokerCapability.SubmitMarketOrders |
        BrokerCapability.SubmitLimitOrders |
        BrokerCapability.SubmitStopOrders |
        BrokerCapability.ModifyOrders |
        BrokerCapability.CancelOrders |
        BrokerCapability.ClosePositions |
        BrokerCapability.StopLoss |
        BrokerCapability.TakeProfit |
        BrokerCapability.Reconciliation |
        BrokerCapability.IdempotentClientOrderIds |
        (demoEnabled ? BrokerCapability.DemoTrading : BrokerCapability.None);
}

internal static class MT5ErrorMapper
{
    public static BrokerError FromResponse(MT5Response response, BrokerExecutionContext context, DateTimeOffset now, string? operation = null)
    {
        var category = response.Code switch
        {
            "ORDER_REJECTED" => BrokerErrorCategory.RejectedByBroker,
            "INVALID_VOLUME" => BrokerErrorCategory.InvalidQuantity,
            "INVALID_STOPS" => BrokerErrorCategory.InvalidStops,
            "MARKET_CLOSED" => BrokerErrorCategory.MarketClosed,
            "INSUFFICIENT_MARGIN" => BrokerErrorCategory.InsufficientMargin,
            "NO_CONNECTION" => BrokerErrorCategory.ConnectorUnavailable,
            "DEMO_ONLY" or "DEMO_EXECUTION_REQUIRED" or "LIVE_MODE_FORBIDDEN" or "DEMO_CONFIRMATION_REQUIRED" or "EXECUTION_GUARDS_REQUIRED" => BrokerErrorCategory.Authorization,
            "DUPLICATE_EXECUTION" => BrokerErrorCategory.DuplicateRequest,
            "CLEANUP_FAILED" => BrokerErrorCategory.ReconciliationRequired,
            "TIMEOUT" => BrokerErrorCategory.Timeout,
            "CANCELLED" => BrokerErrorCategory.Cancelled,
            "INVALID_REQUEST" or "INVALID_QUANTITY" or "POSITION_LIMIT" => BrokerErrorCategory.Validation,
            "ORDER_NOT_FOUND" or "POSITION_NOT_FOUND" => BrokerErrorCategory.AccountUnavailable,
            "INSTRUMENT_NOT_FOUND" => BrokerErrorCategory.InstrumentUnavailable,
            "UNKNOWN_COMMAND" => BrokerErrorCategory.UnsupportedCapability,
            _ => BrokerErrorCategory.ProtocolFailure
        };
        var message = operation is null ? response.Message : $"The MT5 {operation} operation failed.";
        return new BrokerError(response.Code, category, message, category is BrokerErrorCategory.ProtocolFailure or BrokerErrorCategory.ConnectorUnavailable or BrokerErrorCategory.Timeout, category == BrokerErrorCategory.ReconciliationRequired, response.Code,
            new BrokerTraceReference(context.CorrelationId, null, context.ExecutionSessionId), now);
    }
}

public sealed class MT5ProtocolException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
