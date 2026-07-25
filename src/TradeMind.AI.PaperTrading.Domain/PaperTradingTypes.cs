using System.Globalization;
using TradeMind.AI.TradingPlans.Domain;
using TradeMind.AI.TradingWorkspace.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.PaperTrading.Domain;

public sealed record PaperTradingSimulationId
{
    public PaperTradingSimulationId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Simulation id cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public Guid Id => Value;
    public static PaperTradingSimulationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public sealed record PaperTradingSessionId
{
    public PaperTradingSessionId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Paper trading session id cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public Guid Id => Value;
    public static PaperTradingSessionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public sealed record PaperTradeId
{
    public PaperTradeId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Paper trade id cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public sealed record PaperTradingPositionId
{
    public PaperTradingPositionId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Position id cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public Guid Id => Value;
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public sealed record PaperTradingOrderId
{
    public PaperTradingOrderId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Order id cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public Guid Id => Value;
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public sealed record PaperTradingExecutionId
{
    public PaperTradingExecutionId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Execution id cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public Guid Id => Value;
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public sealed record PaperOrderId
{
    public PaperOrderId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Paper order id cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public PaperTradingOrderId TradingId => new(Value);
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public sealed record PaperFillId
{
    public PaperFillId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Paper fill id cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public PaperTradingExecutionId ExecutionId => new(Value);
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public enum PaperTradingStatus
{
    Succeeded,
    PartiallySucceeded,
    NoTrade,
    NoFill,
    Expired,
    Rejected,
    Blocked,
    Invalid,
    TimedOut,
    Cancelled,
    Failed
}

public enum PaperTradingSimulationState
{
    Created,
    Running,
    Completed,
    PartiallyCompleted,
    Rejected,
    Blocked,
    Cancelled,
    TimedOut,
    Failed
}

public enum PaperOrderType
{
    Entry,
    MarketEntry = Entry,
    LimitEntry,
    StopEntry,
    StopLoss,
    TakeProfit
}

public enum PaperOrderSide { Buy, Sell }

public enum PaperOrderStatus
{
    Pending,
    PartiallyFilled,
    Filled,
    Expired,
    Cancelled,
    Rejected
}

public enum PaperPositionStatus { Open, PartiallyClosed, Closed, Cancelled }

public enum PaperExitReason
{
    StopLoss,
    TakeProfit,
    EndOfSimulation,
    Expired,
    Cancelled
}

public enum PaperTradingEventType
{
    SessionStarted,
    SimulationStarted = SessionStarted,
    EntryOrderCreated,
    EntryFilled,
    PositionOpened,
    PartialTargetHit,
    StopLossTriggered,
    TakeProfitTriggered,
    FinalTargetHit,
    ExitFilled,
    PositionClosed,
    UnrealizedPnlUpdated,
    EntryExpired,
    PlanExpired,
    AmbiguousTriggerResolved,
    SimulationRejected,
    SimulationBlocked,
    SimulationCancelled,
    SimulationCompleted,
    SessionCompleted = SimulationCompleted,
    Warning,
    Error
}

public enum PaperTriggerTieBreakPolicy
{
    StopLossFirst,
    TakeProfitFirst,
    RejectAmbiguousTick
}

public enum PaperAmbiguousTriggerPolicy
{
    ConservativeStopFirst,
    TargetFirst,
    RejectAmbiguousTick
}

public enum PaperTradingFillBehavior { Touch, MarketOnNextTick }
public enum PaperSpreadHandling { UseBidAsk, UseMidPrice }

public sealed record PaperTargetAllocation
{
    public PaperTargetAllocation(int ordinal, decimal quantity)
    {
        if (ordinal < 0) throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Ordinal = ordinal;
        Quantity = quantity;
    }

    public int Ordinal { get; }
    public decimal Quantity { get; }
}

public sealed record PaperTradingSimulationOptions
{
    public TimeSpan? StartAtUtc { get; init; }
    public TimeSpan? EndAtUtc { get; init; }
    public decimal PointValue { get; init; } = 1m;
    public decimal CommissionPerUnit { get; init; }
    public decimal CommissionRate { get; init; }
    public decimal SlippagePerUnit { get; init; }
    public decimal SlippageTicks { get; init; }
    public decimal TickSize { get; init; }
    public int? PriceDecimals { get; init; }
    public int? QuantityDecimals { get; init; }
    public PaperTradingFillBehavior FillBehavior { get; init; } = PaperTradingFillBehavior.Touch;
    public PaperSpreadHandling SpreadHandling { get; init; } = PaperSpreadHandling.UseBidAsk;
    public PaperAmbiguousTriggerPolicy AmbiguousTriggerPolicy { get; init; } = PaperAmbiguousTriggerPolicy.ConservativeStopFirst;
    public bool AllowPartialTargetExits { get; init; } = true;
    public IReadOnlyCollection<PaperTargetAllocation> TargetAllocations { get; init; } = [];

    public void Validate()
    {
        if (PointValue <= 0 || CommissionPerUnit < 0 || CommissionRate < 0 || SlippagePerUnit < 0 || SlippageTicks < 0 || TickSize < 0)
            throw new ArgumentOutOfRangeException(nameof(PointValue), "Simulation price and cost options are invalid.");
        if (PriceDecimals is < 0 or > 18 || QuantityDecimals is < 0 or > 18)
            throw new ArgumentOutOfRangeException(nameof(PriceDecimals), "Precision must be between zero and eighteen decimals.");
        if (TargetAllocations is null || TargetAllocations.GroupBy(item => item.Ordinal).Any(group => group.Count() > 1))
            throw new ArgumentException("Target allocations must have unique ordinals.", nameof(TargetAllocations));
    }
}

/// <summary>One immutable bid/ask observation used as the paper market path.</summary>
public sealed record PaperPricePoint
{
    public PaperPricePoint(DateTimeOffset timestampUtc, decimal bid, decimal ask)
    {
        Validate(timestampUtc, bid, ask);
        TimestampUtc = timestampUtc;
        Bid = bid;
        Ask = ask;
    }

    public DateTimeOffset TimestampUtc { get; }
    public decimal Bid { get; }
    public decimal Ask { get; }
    public decimal Mid => (Bid + Ask) / 2m;

    private static void Validate(DateTimeOffset timestampUtc, decimal bid, decimal ask)
    {
        if (timestampUtc == default || timestampUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be a non-default UTC timestamp.", nameof(timestampUtc));
        if (bid <= 0) throw new ArgumentOutOfRangeException(nameof(bid), "Bid must be positive.");
        if (ask <= 0) throw new ArgumentOutOfRangeException(nameof(ask), "Ask must be positive.");
        if (bid > ask) throw new ArgumentException("Bid cannot be greater than ask.", nameof(ask));
    }
}

public sealed record PaperTradingMarketTick
{
    public PaperTradingMarketTick(
        Instrument instrument,
        DateTimeOffset timestampUtc,
        decimal bid,
        decimal ask,
        long sequence = 0,
        decimal? volume = null,
        decimal? spread = null,
        decimal? high = null,
        decimal? low = null)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        if (timestampUtc == default || timestampUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be a non-default UTC timestamp.", nameof(timestampUtc));
        if (bid <= 0 || ask <= 0 || bid > ask) throw new ArgumentOutOfRangeException(nameof(bid), "Bid and ask must be positive and ordered.");
        if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (volume is < 0) throw new ArgumentOutOfRangeException(nameof(volume));
        if (spread is < 0) throw new ArgumentOutOfRangeException(nameof(spread));
        if (high is <= 0 || low is <= 0) throw new ArgumentOutOfRangeException(nameof(high));
        if (high is { } highValue && highValue < Math.Max(bid, ask)) throw new ArgumentException("High cannot be below bid or ask.", nameof(high));
        if (low is { } lowValue && lowValue > Math.Min(bid, ask)) throw new ArgumentException("Low cannot be above bid or ask.", nameof(low));
        Instrument = instrument;
        TimestampUtc = timestampUtc;
        Bid = bid;
        Ask = ask;
        Sequence = sequence;
        Volume = volume;
        Spread = spread;
        High = high;
        Low = low;
    }

    public Instrument Instrument { get; }
    public DateTimeOffset TimestampUtc { get; }
    public decimal Bid { get; }
    public decimal Ask { get; }
    public long Sequence { get; }
    public decimal? Volume { get; }
    public decimal? Spread { get; }
    public decimal? SpreadMetadata => Spread;
    public decimal? High { get; }
    public decimal? Low { get; }
    public decimal Mid => (Bid + Ask) / 2m;
}

/// <summary>Immutable request for one deterministic paper simulation.</summary>
public sealed record PaperTradingRequest
{
    public const int CurrentVersion = 1;

    public PaperTradingRequest(
        PaperTradingSessionId sessionId,
        TradingWorkspaceResult workspace,
        TradingPlanResult plan,
        IReadOnlyCollection<PaperPricePoint> pricePath,
        decimal initialEquity,
        TimeSpan? timeout = null,
        int version = CurrentVersion)
        : this(new PaperTradingSimulationId(sessionId?.Value ?? throw new ArgumentNullException(nameof(sessionId))), workspace, plan,
            CreateTicks(plan, pricePath), initialEquity, timeout, null, version, null, null)
    {
        SessionId = sessionId;
    }

    public PaperTradingRequest(
        PaperTradingSimulationId simulationId,
        TradingWorkspaceResult workspace,
        TradingPlanResult plan,
        IReadOnlyCollection<PaperTradingMarketTick> marketTicks,
        decimal? initialBalance = null,
        TimeSpan? timeout = null,
        PaperTradingSimulationOptions? options = null,
        int version = CurrentVersion,
        DateTimeOffset? startAtUtc = null,
        DateTimeOffset? endAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(simulationId);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(marketTicks);
        if (initialBalance is < 0) throw new ArgumentOutOfRangeException(nameof(initialBalance));
        if (timeout is { } timeoutValue && timeoutValue <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        if (startAtUtc is { } start && (start == default || start.Offset != TimeSpan.Zero)) throw new ArgumentException("Start must be UTC.", nameof(startAtUtc));
        if (endAtUtc is { } end && (end == default || end.Offset != TimeSpan.Zero)) throw new ArgumentException("End must be UTC.", nameof(endAtUtc));
        if (startAtUtc.HasValue && endAtUtc.HasValue && startAtUtc > endAtUtc) throw new ArgumentException("Simulation interval is invalid.");

        var ticks = NormalizeTicks(marketTicks.ToArray());
        if (ticks.GroupBy(tick => tick.Sequence).Any(group => group.Count() > 1)) throw new ArgumentException("Market tick sequence identifiers must be unique.", nameof(marketTicks));
        if (ticks.Any(tick => tick.Instrument != plan.Instrument)) throw new ArgumentException("Every market tick must use the plan instrument.", nameof(marketTicks));
        if (startAtUtc.HasValue && ticks.Any(tick => tick.TimestampUtc < startAtUtc.Value)) throw new ArgumentException("A market tick is before the simulation interval.", nameof(marketTicks));
        if (endAtUtc.HasValue && ticks.Any(tick => tick.TimestampUtc > endAtUtc.Value)) throw new ArgumentException("A market tick is after the simulation interval.", nameof(marketTicks));

        SimulationId = simulationId;
        SessionId = new PaperTradingSessionId(simulationId.Value);
        Workspace = workspace;
        Plan = plan;
        MarketTicks = Array.AsReadOnly(ticks.OrderBy(tick => tick.TimestampUtc).ThenBy(tick => tick.Sequence).ToArray());
        PricePath = Array.AsReadOnly(MarketTicks.Select(tick => new PaperPricePoint(tick.TimestampUtc, tick.Bid, tick.Ask)).ToArray());
        InitialBalance = initialBalance ?? 0m;
        InitialEquity = InitialBalance;
        Timeout = timeout;
        var selectedOptions = options ?? new PaperTradingSimulationOptions();
        selectedOptions.Validate();
        Options = selectedOptions with { TargetAllocations = Array.AsReadOnly(selectedOptions.TargetAllocations.ToArray()) };
        Version = version;
        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;
    }

    public int Version { get; }
    public PaperTradingSimulationId SimulationId { get; }
    public PaperTradingSessionId SessionId { get; }
    public TradingWorkspaceResult Workspace { get; }
    public TradingPlanResult Plan { get; }
    public IReadOnlyList<PaperTradingMarketTick> MarketTicks { get; }
    public IReadOnlyList<PaperPricePoint> PricePath { get; }
    public decimal InitialBalance { get; }
    public decimal InitialEquity { get; }
    public TimeSpan? Timeout { get; }
    public PaperTradingSimulationOptions Options { get; }
    public DateTimeOffset? StartAtUtc { get; }
    public DateTimeOffset? EndAtUtc { get; }

    private static IReadOnlyCollection<PaperTradingMarketTick> CreateTicks(TradingPlanResult? plan, IReadOnlyCollection<PaperPricePoint>? pricePath)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(pricePath);
        return pricePath.OrderBy(point => point.TimestampUtc).ThenBy(point => point.Bid).ThenBy(point => point.Ask).Select((point, index) => new PaperTradingMarketTick(plan.Instrument, point.TimestampUtc, point.Bid, point.Ask, index)).ToArray();
    }

    private static PaperTradingMarketTick[] NormalizeTicks(PaperTradingMarketTick[] ticks)
    {
        if (ticks.Length <= 1 || ticks.Any(tick => tick.Sequence != 0)) return ticks;
        return ticks.OrderBy(tick => tick.TimestampUtc).ThenBy(tick => tick.Bid).ThenBy(tick => tick.Ask)
            .Select((tick, index) => new PaperTradingMarketTick(tick.Instrument, tick.TimestampUtc, tick.Bid, tick.Ask, index, tick.Volume, tick.Spread, tick.High, tick.Low))
            .ToArray();
    }
}

public sealed record PaperOrder
{
    public PaperOrder(PaperOrderId orderId, PaperTradeId tradeId, PaperOrderType type, PaperOrderSide side, decimal quantity, decimal requestedPrice, DateTimeOffset createdAtUtc, PaperOrderStatus status, PaperFillId? fillId = null)
    {
        ArgumentNullException.ThrowIfNull(orderId);
        ArgumentNullException.ThrowIfNull(tradeId);
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (requestedPrice <= 0) throw new ArgumentOutOfRangeException(nameof(requestedPrice));
        if (createdAtUtc == default || createdAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Order timestamp must be UTC.", nameof(createdAtUtc));
        OrderId = orderId; TradeId = tradeId; Type = type; Side = side; Quantity = quantity; RequestedPrice = requestedPrice; CreatedAtUtc = createdAtUtc; Status = status; FillId = fillId;
    }

    public PaperOrderId OrderId { get; }
    public PaperTradingOrderId TradingOrderId => OrderId.TradingId;
    public PaperTradeId TradeId { get; }
    public PaperOrderType Type { get; }
    public PaperOrderSide Side { get; }
    public decimal Quantity { get; }
    public decimal RequestedPrice { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public PaperOrderStatus Status { get; }
    public PaperFillId? FillId { get; }
}

public sealed record PaperFill
{
    public PaperFill(PaperFillId fillId, PaperOrderId orderId, PaperTradeId tradeId, PaperOrderSide side, decimal quantity, decimal price, DateTimeOffset timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(fillId); ArgumentNullException.ThrowIfNull(orderId); ArgumentNullException.ThrowIfNull(tradeId);
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
        if (timestampUtc == default || timestampUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Fill timestamp must be UTC.", nameof(timestampUtc));
        FillId = fillId; OrderId = orderId; TradeId = tradeId; Side = side; Quantity = quantity; Price = price; TimestampUtc = timestampUtc;
    }

    public PaperFillId FillId { get; }
    public PaperTradingExecutionId ExecutionId => FillId.ExecutionId;
    public PaperOrderId OrderId { get; }
    public PaperTradeId TradeId { get; }
    public PaperOrderSide Side { get; }
    public decimal Quantity { get; }
    public decimal Price { get; }
    public DateTimeOffset TimestampUtc { get; }
}

public sealed record PaperExecution
{
    public PaperExecution(PaperTradingExecutionId executionId, PaperTradingOrderId orderId, PaperTradingPositionId positionId, PaperOrderSide side, decimal quantity, decimal price, DateTimeOffset timestampUtc, decimal commission = 0m)
    {
        ArgumentNullException.ThrowIfNull(executionId); ArgumentNullException.ThrowIfNull(orderId); ArgumentNullException.ThrowIfNull(positionId);
        if (quantity <= 0 || price <= 0 || commission < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        ExecutionId = executionId; OrderId = orderId; PositionId = positionId; Side = side; Quantity = quantity; Price = price; TimestampUtc = timestampUtc; Commission = commission;
    }

    public PaperTradingExecutionId ExecutionId { get; }
    public PaperTradingOrderId OrderId { get; }
    public PaperTradingPositionId PositionId { get; }
    public PaperOrderSide Side { get; }
    public decimal Quantity { get; }
    public decimal Price { get; }
    public DateTimeOffset TimestampUtc { get; }
    public decimal Commission { get; }
}

public sealed record PaperPositionSnapshot
{
    public PaperPositionSnapshot(PaperTradeId tradeId, TradingPlanDirection direction, decimal quantity, decimal entryPrice, DateTimeOffset entryTimestampUtc, decimal markPrice, decimal unrealizedPnl, PaperPositionStatus status = PaperPositionStatus.Open, decimal maximumFavorableExcursion = 0m, decimal maximumAdverseExcursion = 0m, DateTimeOffset? exitTimestampUtc = null)
    {
        ArgumentNullException.ThrowIfNull(tradeId);
        if (direction is not (TradingPlanDirection.Long or TradingPlanDirection.Short)) throw new ArgumentException("Position direction must be directional.", nameof(direction));
        if (quantity <= 0 || entryPrice <= 0 || markPrice <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (exitTimestampUtc is { } exit && exit < entryTimestampUtc) throw new ArgumentException("Position exit cannot precede entry.", nameof(exitTimestampUtc));
        TradeId = tradeId; PositionId = new PaperTradingPositionId(tradeId.Value); Direction = direction; Quantity = quantity; EntryPrice = entryPrice; EntryTimestampUtc = entryTimestampUtc; MarkPrice = markPrice; UnrealizedPnl = unrealizedPnl; Status = status; MaximumFavorableExcursion = maximumFavorableExcursion; MaximumAdverseExcursion = maximumAdverseExcursion; ExitTimestampUtc = exitTimestampUtc;
    }

    public PaperTradeId TradeId { get; }
    public PaperTradingPositionId PositionId { get; }
    public TradingPlanDirection Direction { get; }
    public decimal Quantity { get; }
    public decimal EntryPrice { get; }
    public DateTimeOffset EntryTimestampUtc { get; }
    public decimal MarkPrice { get; }
    public decimal UnrealizedPnl { get; }
    public PaperPositionStatus Status { get; }
    public decimal MaximumFavorableExcursion { get; }
    public decimal MaximumAdverseExcursion { get; }
    public DateTimeOffset? ExitTimestampUtc { get; }
    public TimeSpan Duration => ExitTimestampUtc is { } exit ? exit - EntryTimestampUtc : TimeSpan.Zero;
}

public sealed record PaperPosition
{
    public PaperPosition(PaperTradingPositionId positionId, TradingPlanDirection direction, decimal quantity, decimal entryPrice, DateTimeOffset entryTimestampUtc, PaperPositionStatus status)
    {
        ArgumentNullException.ThrowIfNull(positionId);
        if (quantity <= 0 || entryPrice <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        PositionId = positionId; Direction = direction; Quantity = quantity; EntryPrice = entryPrice; EntryTimestampUtc = entryTimestampUtc; Status = status;
    }

    public PaperTradingPositionId PositionId { get; }
    public TradingPlanDirection Direction { get; }
    public decimal Quantity { get; }
    public decimal EntryPrice { get; }
    public DateTimeOffset EntryTimestampUtc { get; }
    public PaperPositionStatus Status { get; }
}

public sealed record PaperTradingPnL
{
    public PaperTradingPnL(decimal realized, decimal unrealized, decimal commission, decimal gross)
    {
        Realized = realized; Unrealized = unrealized; Commission = commission; Gross = gross;
    }

    public decimal Realized { get; }
    public decimal Unrealized { get; }
    public decimal RealizedPnl => Realized;
    public decimal UnrealizedPnl => Unrealized;
    public decimal Commission { get; }
    public decimal Gross { get; }
    public decimal Net => Realized + Unrealized;
}

public sealed record PaperEquityPoint
{
    public PaperEquityPoint(DateTimeOffset timestampUtc, decimal equity, decimal realizedPnl, decimal unrealizedPnl, decimal balance = 0m)
    {
        TimestampUtc = timestampUtc; Equity = equity; RealizedPnl = realizedPnl; UnrealizedPnl = unrealizedPnl; Balance = balance;
    }

    public DateTimeOffset TimestampUtc { get; }
    public decimal Equity { get; }
    public decimal Balance { get; }
    public decimal RealizedPnl { get; }
    public decimal UnrealizedPnl { get; }
}

public sealed record PaperTradingEquityPoint(DateTimeOffset TimestampUtc, decimal Balance, decimal Equity, decimal UnrealizedPnl);

public sealed record PaperTradeJournalEntry
{
    public PaperTradeJournalEntry(PaperTradeId tradeId, TradingPlanDirection direction, decimal quantity, decimal entryPrice, DateTimeOffset entryTimestampUtc, decimal? exitPrice, DateTimeOffset? exitTimestampUtc, PaperExitReason? exitReason, decimal realizedPnl, decimal maximumFavorableExcursion = 0m, decimal maximumAdverseExcursion = 0m, decimal commission = 0m)
    {
        ArgumentNullException.ThrowIfNull(tradeId);
        if (quantity <= 0 || entryPrice <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (direction is not (TradingPlanDirection.Long or TradingPlanDirection.Short)) throw new ArgumentException("Journal direction must be directional.", nameof(direction));
        TradeId = tradeId; Direction = direction; Quantity = quantity; EntryPrice = entryPrice; EntryTimestampUtc = entryTimestampUtc; ExitPrice = exitPrice; ExitTimestampUtc = exitTimestampUtc; ExitReason = exitReason; RealizedPnl = realizedPnl; MaximumFavorableExcursion = maximumFavorableExcursion; MaximumAdverseExcursion = maximumAdverseExcursion; Commission = commission;
    }

    public PaperTradeId TradeId { get; }
    public TradingPlanDirection Direction { get; }
    public decimal Quantity { get; }
    public decimal EntryPrice { get; }
    public DateTimeOffset EntryTimestampUtc { get; }
    public decimal? ExitPrice { get; }
    public DateTimeOffset? ExitTimestampUtc { get; }
    public PaperExitReason? ExitReason { get; }
    public decimal RealizedPnl { get; }
    public decimal MaximumFavorableExcursion { get; }
    public decimal MaximumAdverseExcursion { get; }
    public decimal Commission { get; }
    public TimeSpan Duration => ExitTimestampUtc is { } exit ? exit - EntryTimestampUtc : TimeSpan.Zero;
    public bool IsClosed => ExitPrice.HasValue && ExitTimestampUtc.HasValue && ExitReason.HasValue;
}

public sealed record PaperTradingStatistics
{
    public PaperTradingStatistics(int totalTrades, int closedTrades, int winningTrades, int losingTrades, int breakevenTrades, decimal grossProfit, decimal grossLoss, decimal netRealizedPnl, decimal maxDrawdown, decimal averageClosedPnl, decimal maximumFavorableExcursion = 0m, decimal maximumAdverseExcursion = 0m)
    {
        if (totalTrades < 0 || closedTrades < 0 || winningTrades < 0 || losingTrades < 0 || breakevenTrades < 0) throw new ArgumentOutOfRangeException(nameof(totalTrades));
        TotalTrades = totalTrades; ClosedTrades = closedTrades; WinningTrades = winningTrades; LosingTrades = losingTrades; BreakevenTrades = breakevenTrades; GrossProfit = grossProfit; GrossLoss = grossLoss; NetRealizedPnl = netRealizedPnl; MaxDrawdown = maxDrawdown; AverageClosedPnl = averageClosedPnl; MaximumFavorableExcursion = maximumFavorableExcursion; MaximumAdverseExcursion = maximumAdverseExcursion;
    }

    public int TotalTrades { get; }
    public int ClosedTrades { get; }
    public int WinningTrades { get; }
    public int LosingTrades { get; }
    public int BreakevenTrades { get; }
    public decimal GrossProfit { get; }
    public decimal GrossLoss { get; }
    public decimal NetRealizedPnl { get; }
    public decimal MaxDrawdown { get; }
    public decimal AverageClosedPnl { get; }
    public decimal MaximumFavorableExcursion { get; }
    public decimal MaximumAdverseExcursion { get; }
    public decimal WinRate => ClosedTrades == 0 ? 0 : (decimal)WinningTrades / ClosedTrades;
    public decimal ProfitFactor => GrossLoss == 0 ? (GrossProfit == 0 ? 0 : decimal.MaxValue) : GrossProfit / GrossLoss;
}

public sealed record PaperTradingEvent
{
    public PaperTradingEvent(int sequence, PaperTradingEventType type, DateTimeOffset timestampUtc, string sourceId, string description, PaperOrderId? orderId = null, PaperFillId? fillId = null, PaperTradeId? tradeId = null)
    {
        if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId); ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Sequence = sequence; Type = type; TimestampUtc = timestampUtc; SourceId = sourceId.Trim(); Description = description.Trim(); OrderId = orderId; FillId = fillId; TradeId = tradeId;
    }

    public int Sequence { get; }
    public PaperTradingEventType Type { get; }
    public DateTimeOffset TimestampUtc { get; }
    public string SourceId { get; }
    public string Description { get; }
    public PaperOrderId? OrderId { get; }
    public PaperFillId? FillId { get; }
    public PaperTradeId? TradeId { get; }
}

public sealed record PaperTradingTimelineEntry
{
    public PaperTradingTimelineEntry(int sequence, PaperTradingEventType eventType, DateTimeOffset timestampUtc, string code, string message)
    {
        if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        ArgumentException.ThrowIfNullOrWhiteSpace(code); ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Sequence = sequence;
        EventType = eventType;
        TimestampUtc = timestampUtc;
        Code = code.Trim();
        Message = message.Trim();
    }

    public int Sequence { get; }
    public PaperTradingEventType EventType { get; }
    public PaperTradingEventType Type => EventType;
    public DateTimeOffset TimestampUtc { get; }
    public string Code { get; }
    public string Message { get; }
}

public sealed record PaperTradingWarning
{
    public PaperTradingWarning(string code, string message) { ArgumentException.ThrowIfNullOrWhiteSpace(code); ArgumentException.ThrowIfNullOrWhiteSpace(message); Code = code.Trim(); Message = message.Trim(); }
    public string Code { get; }
    public string Message { get; }
}

public sealed record PaperTradingError
{
    public PaperTradingError(string code, string message, bool blocking = true) { ArgumentException.ThrowIfNullOrWhiteSpace(code); ArgumentException.ThrowIfNullOrWhiteSpace(message); Code = code.Trim(); Message = message.Trim(); Blocking = blocking; }
    public string Code { get; }
    public string Message { get; }
    public bool Blocking { get; }
}

public sealed record PaperTradingBlocker
{
    public PaperTradingBlocker(string code, string message) { ArgumentException.ThrowIfNullOrWhiteSpace(code); ArgumentException.ThrowIfNullOrWhiteSpace(message); Code = code.Trim(); Message = message.Trim(); }
    public string Code { get; }
    public string Message { get; }
}

public sealed record PaperTradingTraceReference
{
    public PaperTradingTraceReference(string sourceType, string sourceId, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType); ArgumentException.ThrowIfNullOrWhiteSpace(sourceId); ArgumentException.ThrowIfNullOrWhiteSpace(description);
        SourceType = sourceType.Trim(); SourceId = sourceId.Trim(); Description = description.Trim();
    }
    public string SourceType { get; }
    public string SourceId { get; }
    public string Description { get; }
}

/// <summary>Complete immutable output of a paper trading simulation.</summary>
public sealed record PaperTradingResult
{
    public const int CurrentSchemaVersion = 1;

    public PaperTradingResult(
        PaperTradingSessionId sessionId,
        TradingWorkspaceResult workspace,
        TradingPlanResult plan,
        PaperTradingStatus status,
        IReadOnlyCollection<PaperOrder> orders,
        IReadOnlyCollection<PaperFill> fills,
        PaperPositionSnapshot? openPosition,
        decimal realizedPnl,
        decimal unrealizedPnl,
        IReadOnlyCollection<PaperEquityPoint> equityCurve,
        IReadOnlyCollection<PaperTradeJournalEntry> journal,
        PaperTradingStatistics statistics,
        IReadOnlyCollection<PaperTradingEvent> timeline,
        IReadOnlyCollection<PaperTradingWarning> warnings,
        IReadOnlyCollection<PaperTradingError> errors,
        string replayFingerprint,
        string summary,
        DateTimeOffset createdAtUtc,
        DateTimeOffset completedAtUtc,
        int schemaVersion = CurrentSchemaVersion,
        PaperTradingSimulationState simulationState = PaperTradingSimulationState.Completed,
        decimal? initialBalance = null,
        decimal? finalBalance = null,
        decimal? finalEquity = null,
        PaperTradingPnL? pnl = null,
        IReadOnlyCollection<PaperExecution>? executions = null,
        IReadOnlyCollection<PaperPositionSnapshot>? positionSnapshots = null,
        IReadOnlyCollection<PaperTradingBlocker>? blockers = null,
        IReadOnlyCollection<PaperTradingTraceReference>? traces = null,
        IReadOnlyCollection<PaperTradingTimelineEntry>? timelineEntries = null)
    {
        ArgumentNullException.ThrowIfNull(sessionId); ArgumentNullException.ThrowIfNull(workspace); ArgumentNullException.ThrowIfNull(plan); ArgumentNullException.ThrowIfNull(orders); ArgumentNullException.ThrowIfNull(fills); ArgumentNullException.ThrowIfNull(equityCurve); ArgumentNullException.ThrowIfNull(journal); ArgumentNullException.ThrowIfNull(statistics); ArgumentNullException.ThrowIfNull(timeline); ArgumentNullException.ThrowIfNull(warnings); ArgumentNullException.ThrowIfNull(errors);
        ArgumentException.ThrowIfNullOrWhiteSpace(replayFingerprint); ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        if (completedAtUtc < createdAtUtc) throw new ArgumentException("Simulation completion cannot precede creation.", nameof(completedAtUtc));
        if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        SessionId = sessionId; SimulationId = new PaperTradingSimulationId(sessionId.Value); Workspace = workspace; Plan = plan; Status = status; SimulationState = simulationState; Orders = Array.AsReadOnly(orders.ToArray()); Fills = Array.AsReadOnly(fills.ToArray()); Executions = Array.AsReadOnly((executions ?? []).ToArray()); OpenPosition = openPosition; Position = openPosition is null ? null : new PaperPosition(openPosition.PositionId, openPosition.Direction, openPosition.Quantity, openPosition.EntryPrice, openPosition.EntryTimestampUtc, openPosition.Status); Positions = Array.AsReadOnly(openPosition is null ? Array.Empty<PaperPosition>() : [Position!]); PositionSnapshots = Array.AsReadOnly((positionSnapshots ?? (openPosition is null ? [] : [openPosition])).ToArray()); RealizedPnl = realizedPnl; UnrealizedPnl = unrealizedPnl; InitialBalance = initialBalance ?? 0m; FinalBalance = finalBalance ?? InitialBalance + realizedPnl; FinalEquity = finalEquity ?? FinalBalance + unrealizedPnl; Pnl = pnl ?? new PaperTradingPnL(realizedPnl, unrealizedPnl, 0m, realizedPnl + unrealizedPnl); EquityCurve = Array.AsReadOnly(equityCurve.ToArray()); Journal = Array.AsReadOnly(journal.ToArray()); Statistics = statistics; Timeline = Array.AsReadOnly(timeline.OrderBy(item => item.Sequence).ToArray()); TimelineEntries = Array.AsReadOnly((timelineEntries ?? timeline.Select(item => new PaperTradingTimelineEntry(item.Sequence, item.Type, item.TimestampUtc, item.SourceId, item.Description))).ToArray()); Warnings = Array.AsReadOnly(warnings.ToArray()); Errors = Array.AsReadOnly(errors.ToArray()); Blockers = Array.AsReadOnly((blockers ?? []).ToArray()); Traces = Array.AsReadOnly((traces ?? []).ToArray()); ReplayFingerprint = replayFingerprint.Trim(); Summary = summary.Trim(); CreatedAtUtc = createdAtUtc; CompletedAtUtc = completedAtUtc; SchemaVersion = schemaVersion;
    }

    public PaperTradingSessionId SessionId { get; }
    public PaperTradingSimulationId SimulationId { get; }
    public TradingWorkspaceResult Workspace { get; }
    public TradingPlanResult Plan { get; }
    public WorkspaceId SourceWorkspaceId => Workspace.WorkspaceId;
    public TradingPlanId SourcePlanId => Plan.PlanId;
    public PaperTradingStatus Status { get; }
    public PaperTradingSimulationState SimulationState { get; }
    public IReadOnlyList<PaperOrder> Orders { get; }
    public IReadOnlyList<PaperFill> Fills { get; }
    public IReadOnlyList<PaperExecution> Executions { get; }
    public PaperPositionSnapshot? OpenPosition { get; }
    public PaperPosition? Position { get; }
    public IReadOnlyList<PaperPosition> Positions { get; }
    public IReadOnlyList<PaperPositionSnapshot> PositionSnapshots { get; }
    public decimal RealizedPnl { get; }
    public decimal UnrealizedPnl { get; }
    public decimal RealizedPnL => RealizedPnl;
    public decimal UnrealizedPnL => UnrealizedPnl;
    public decimal InitialBalance { get; }
    public decimal FinalBalance { get; }
    public decimal FinalEquity { get; }
    public decimal Balance => FinalBalance;
    public decimal Equity => FinalEquity;
    public PaperTradingPnL Pnl { get; }
    public IReadOnlyList<PaperEquityPoint> EquityCurve { get; }
    public IReadOnlyList<PaperTradeJournalEntry> Journal { get; }
    public PaperTradingStatistics Statistics { get; }
    public IReadOnlyList<PaperTradingEvent> Timeline { get; }
    public IReadOnlyList<PaperTradingTimelineEntry> TimelineEntries { get; }
    public IReadOnlyList<PaperTradingWarning> Warnings { get; }
    public IReadOnlyList<PaperTradingError> Errors { get; }
    public IReadOnlyList<PaperTradingBlocker> Blockers { get; }
    public IReadOnlyList<PaperTradingTraceReference> Traces { get; }
    public string ReplayFingerprint { get; }
    public string Summary { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public int SchemaVersion { get; }
}
