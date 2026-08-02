namespace TradeMind.Brokers.MetaTrader5.Serialization;

internal sealed record MT5AccountPayload(
    string AccountId,
    string Name,
    string Currency,
    string AccountType,
    string Environment,
    decimal Balance,
    decimal Equity,
    decimal Margin,
    decimal FreeMargin,
    decimal? MarginLevel,
    int? Leverage,
    bool TradingEnabled,
    bool ReadOnly,
    string Status,
    DateTimeOffset UpdatedAtUtc);

internal sealed record MT5InstrumentPayload(
    string Instrument,
    string BrokerSymbol,
    string AssetClass,
    string BaseCurrency,
    string QuoteCurrency,
    int PricePrecision,
    int QuantityPrecision,
    decimal TickSize,
    decimal TickValue,
    decimal ContractSize,
    decimal MinimumQuantity,
    decimal MaximumQuantity,
    decimal QuantityStep,
    decimal MinimumStopDistance,
    string MarketStatus,
    IReadOnlyList<string> SupportedOrderTypes,
    DateTimeOffset UpdatedAtUtc);

internal sealed record MT5OrderPayload(
    string OrderId,
    string ExecutionId,
    string AccountId,
    string ClientOrderId,
    string Instrument,
    string Side,
    string OrderType,
    decimal Quantity,
    decimal FilledQuantity,
    decimal? RequestedPrice,
    decimal? AverageFillPrice,
    string Status,
    string TimeInForce,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

internal sealed record MT5PositionPayload(
    string PositionId,
    string AccountId,
    string Instrument,
    string Side,
    decimal Quantity,
    decimal AveragePrice,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset UpdatedAtUtc);

internal sealed record MT5ExecutionPayload(
    string ExecutionId,
    string OrderId,
    string AccountId,
    string Status,
    decimal FilledQuantity,
    decimal? AveragePrice,
    DateTimeOffset OccurredAtUtc,
    string? PositionId);
