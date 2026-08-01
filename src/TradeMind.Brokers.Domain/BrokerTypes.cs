namespace TradeMind.Brokers.Domain;

public enum BrokerExecutionMode
{
    Simulation,
    Demo,
    Live
}

public enum BrokerEnvironment
{
    Development,
    Test,
    Production
}

public enum BrokerAssetClass
{
    Unknown,
    Forex,
    Equity,
    Crypto,
    Future,
    Index,
    Commodity,
    Option
}

public enum BrokerAccountType
{
    Unknown,
    Cash,
    Margin,
    Demo
}

public enum BrokerAccountStatus
{
    Unknown,
    Active,
    Suspended,
    Closed
}

public enum BrokerMarketStatus
{
    Unknown,
    Open,
    Closed,
    Halted
}

public enum BrokerOrderType
{
    Market,
    Limit,
    Stop,
    StopLimit
}

public enum BrokerOrderSide
{
    Buy,
    Sell
}

public enum BrokerOrderStatus
{
    Created,
    Validated,
    Submitted,
    Accepted,
    PartiallyFilled,
    Filled,
    Pending,
    CancelRequested,
    Cancelled,
    Rejected,
    Expired,
    Failed,
    Unknown
}

public enum BrokerTimeInForce
{
    GoodTillCancelled,
    Day,
    ImmediateOrCancel,
    FillOrKill,
    GoodTillDate
}

public enum BrokerHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}

public enum BrokerErrorCategory
{
    Validation,
    Authentication,
    Authorization,
    TenantMismatch,
    UnsupportedCapability,
    ConnectorUnavailable,
    AccountUnavailable,
    InstrumentUnavailable,
    MarketClosed,
    InsufficientFunds,
    InsufficientMargin,
    InvalidQuantity,
    InvalidPrice,
    InvalidStops,
    DuplicateRequest,
    Conflict,
    RateLimited,
    Timeout,
    Cancelled,
    RejectedByBroker,
    TransportFailure,
    ProtocolFailure,
    ReconciliationRequired,
    Unknown
}

[Flags]
public enum BrokerCapability
{
    None = 0,
    ReadAccounts = 1 << 0,
    ReadInstruments = 1 << 1,
    ReadOrders = 1 << 2,
    ReadPositions = 1 << 3,
    SubmitMarketOrders = 1 << 4,
    SubmitLimitOrders = 1 << 5,
    SubmitStopOrders = 1 << 6,
    ModifyOrders = 1 << 7,
    CancelOrders = 1 << 8,
    ClosePositions = 1 << 9,
    PartialClose = 1 << 10,
    StopLoss = 1 << 11,
    TakeProfit = 1 << 12,
    MultipleTargets = 1 << 13,
    TrailingStop = 1 << 14,
    StreamingPrices = 1 << 15,
    StreamingOrders = 1 << 16,
    StreamingPositions = 1 << 17,
    Reconciliation = 1 << 18,
    IdempotentClientOrderIds = 1 << 19,
    DemoTrading = 1 << 20,
    LiveTrading = 1 << 21
}
