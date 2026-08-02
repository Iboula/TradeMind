namespace TradeMind.Api.Contracts.Brokers;

public enum BrokerApiExecutionMode
{
    Simulation,
    Demo,
    Live
}

public enum BrokerApiOrderSide
{
    Buy,
    Sell
}

public enum BrokerApiOrderType
{
    Market,
    Limit,
    Stop,
    StopLimit
}

public enum BrokerApiTimeInForce
{
    GoodTillCancelled,
    Day,
    ImmediateOrCancel,
    FillOrKill,
    GoodTillDate
}

public sealed record BrokerOrderApiRequest(
    string ConnectorId,
    string AccountId,
    string ClientOrderId,
    string Instrument,
    BrokerApiOrderSide Side,
    BrokerApiOrderType OrderType,
    decimal Quantity,
    decimal? RequestedPrice,
    decimal? StopPrice,
    decimal? StopLoss,
    IReadOnlyList<decimal>? TakeProfits,
    BrokerApiTimeInForce TimeInForce,
    DateTimeOffset? ExpirationUtc,
    string IdempotencyKey,
    string? TradingPlanId,
    string? RiskAssessmentId,
    BrokerApiExecutionMode Mode = BrokerApiExecutionMode.Simulation);

public sealed record BrokerOrderModificationApiRequest(string ConnectorId, decimal? Quantity, decimal? LimitPrice, decimal? StopPrice, DateTimeOffset? ExpirationUtc);
public sealed record BrokerOrderCancellationApiRequest(string ConnectorId, string Reason);
public sealed record BrokerPositionCloseApiRequest(string ConnectorId, decimal? Quantity);
public sealed record BrokerReconciliationApiRequest(string AccountId);

public sealed record BrokerConnectorApiResponse(
    string ConnectorId,
    string BrokerId,
    string Name,
    string Version,
    string Environment,
    string Mode,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> SupportedAssetClasses,
    IReadOnlyList<string> SupportedOrderTypes,
    bool SupportsStreaming,
    bool SupportsDemo,
    bool SupportsLive,
    bool SupportsReconciliation,
    int MaximumConcurrentRequests,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record BrokerHealthApiResponse(string ConnectorId, string Status, string Message, DateTimeOffset CheckedAtUtc, TimeSpan Duration, string? ErrorCode, string? SafeMessage);
public sealed record BrokerAccountApiResponse(string AccountId, string ConnectorId, string ExternalAccountReference, string Name, string Currency, string AccountType, string Environment, decimal Balance, decimal Equity, decimal Margin, decimal FreeMargin, decimal? MarginLevel, int? Leverage, bool TradingEnabled, bool ReadOnly, string Status, DateTimeOffset UpdatedAtUtc, IReadOnlyDictionary<string, string> Metadata);
public sealed record BrokerTradingSessionApiResponse(string Day, string OpensAtUtc, string ClosesAtUtc);
public sealed record BrokerInstrumentApiResponse(string Instrument, string BrokerSymbol, string AssetClass, string BaseCurrency, string QuoteCurrency, int PricePrecision, int QuantityPrecision, decimal TickSize, decimal TickValue, decimal ContractSize, decimal MinimumQuantity, decimal MaximumQuantity, decimal QuantityStep, decimal MinimumStopDistance, IReadOnlyList<BrokerTradingSessionApiResponse> TradingSessions, string MarketStatus, IReadOnlyList<string> SupportedOrderTypes, DateTimeOffset UpdatedAtUtc);
public sealed record BrokerOrderApiResponse(string OrderId, string ExecutionId, string ConnectorId, string AccountId, string ClientOrderId, string Instrument, string Side, string OrderType, decimal Quantity, decimal FilledQuantity, decimal? RequestedPrice, decimal? AverageFillPrice, string Status, string TimeInForce, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record BrokerPositionApiResponse(string PositionId, string ConnectorId, string AccountId, string Instrument, string Side, decimal Quantity, decimal AveragePrice, DateTimeOffset OpenedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record BrokerReconciliationMismatchApiResponse(string Type, string Reference, string Description);
public sealed record BrokerReconciliationApiResponse(string ReconciliationId, string ConnectorId, string AccountId, DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc, bool IsConsistent, IReadOnlyList<BrokerReconciliationMismatchApiResponse> Mismatches, string? ErrorCode, string? SafeMessage);

public sealed record BrokerExecutionApiResponse(
    string ExecutionId,
    string Status,
    string Operation,
    string? OrderId,
    string? PositionId,
    string? ErrorCode,
    string? ErrorCategory,
    string? SafeMessage,
    DateTimeOffset CompletedAtUtc,
    bool IsLive);
