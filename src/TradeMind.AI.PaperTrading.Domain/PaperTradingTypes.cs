using System.Globalization;
using TradeMind.AI.TradingPlans.Domain;
using TradeMind.AI.TradingWorkspace.Domain;

namespace TradeMind.AI.PaperTrading.Domain;

public sealed record PaperTradingSessionId
{
    public PaperTradingSessionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Paper trading session id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static PaperTradingSessionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public sealed record PaperTradeId
{
    public PaperTradeId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Paper trade id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public sealed record PaperOrderId
{
    public PaperOrderId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Paper order id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public sealed record PaperFillId
{
    public PaperFillId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Paper fill id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public enum PaperTradingStatus
{
    Succeeded,
    PartiallySucceeded,
    NoTrade,
    NoFill,
    Expired,
    Invalid,
    TimedOut,
    Cancelled,
    Failed
}

public enum PaperOrderType
{
    Entry,
    StopLoss,
    TakeProfit
}

public enum PaperOrderSide
{
    Buy,
    Sell
}

public enum PaperOrderStatus
{
    Pending,
    Filled,
    Expired,
    Cancelled
}

public enum PaperExitReason
{
    StopLoss,
    TakeProfit,
    EndOfSimulation
}

public enum PaperTradingEventType
{
    SessionStarted,
    EntryOrderCreated,
    EntryFilled,
    PositionOpened,
    StopLossTriggered,
    TakeProfitTriggered,
    ExitFilled,
    PositionClosed,
    UnrealizedPnlUpdated,
    EntryExpired,
    SessionCompleted,
    Warning,
    Error
}

public enum PaperTriggerTieBreakPolicy
{
    StopLossFirst,
    TakeProfitFirst
}

/// <summary>One immutable bid/ask observation used as the paper market path.</summary>
public sealed record PaperPricePoint
{
    public PaperPricePoint(DateTimeOffset timestampUtc, decimal bid, decimal ask)
    {
        if (bid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bid), "Bid must be positive.");
        }

        if (ask <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ask), "Ask must be positive.");
        }

        if (bid > ask)
        {
            throw new ArgumentException("Bid cannot be greater than ask.", nameof(ask));
        }

        TimestampUtc = timestampUtc;
        Bid = bid;
        Ask = ask;
    }

    public DateTimeOffset TimestampUtc { get; }
    public decimal Bid { get; }
    public decimal Ask { get; }
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
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(pricePath);
        if (initialEquity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialEquity));
        }

        if (timeout is { } timeoutValue && timeoutValue <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        SessionId = sessionId;
        Workspace = workspace;
        Plan = plan;
        PricePath = Array.AsReadOnly(pricePath.ToArray());
        InitialEquity = initialEquity;
        Timeout = timeout;
        Version = version;
    }

    public int Version { get; }
    public PaperTradingSessionId SessionId { get; }
    public TradingWorkspaceResult Workspace { get; }
    public TradingPlanResult Plan { get; }
    public IReadOnlyList<PaperPricePoint> PricePath { get; }
    public decimal InitialEquity { get; }
    public TimeSpan? Timeout { get; }
}

public sealed record PaperOrder
{
    public PaperOrder(
        PaperOrderId orderId,
        PaperTradeId tradeId,
        PaperOrderType type,
        PaperOrderSide side,
        decimal quantity,
        decimal requestedPrice,
        DateTimeOffset createdAtUtc,
        PaperOrderStatus status,
        PaperFillId? fillId = null)
    {
        ArgumentNullException.ThrowIfNull(orderId);
        ArgumentNullException.ThrowIfNull(tradeId);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (requestedPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedPrice));
        }

        OrderId = orderId;
        TradeId = tradeId;
        Type = type;
        Side = side;
        Quantity = quantity;
        RequestedPrice = requestedPrice;
        CreatedAtUtc = createdAtUtc;
        Status = status;
        FillId = fillId;
    }

    public PaperOrderId OrderId { get; }
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
    public PaperFill(
        PaperFillId fillId,
        PaperOrderId orderId,
        PaperTradeId tradeId,
        PaperOrderSide side,
        decimal quantity,
        decimal price,
        DateTimeOffset timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(fillId);
        ArgumentNullException.ThrowIfNull(orderId);
        ArgumentNullException.ThrowIfNull(tradeId);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price));
        }

        FillId = fillId;
        OrderId = orderId;
        TradeId = tradeId;
        Side = side;
        Quantity = quantity;
        Price = price;
        TimestampUtc = timestampUtc;
    }

    public PaperFillId FillId { get; }
    public PaperOrderId OrderId { get; }
    public PaperTradeId TradeId { get; }
    public PaperOrderSide Side { get; }
    public decimal Quantity { get; }
    public decimal Price { get; }
    public DateTimeOffset TimestampUtc { get; }
}

public sealed record PaperPositionSnapshot
{
    public PaperPositionSnapshot(
        PaperTradeId tradeId,
        TradingPlanDirection direction,
        decimal quantity,
        decimal entryPrice,
        DateTimeOffset entryTimestampUtc,
        decimal markPrice,
        decimal unrealizedPnl)
    {
        ArgumentNullException.ThrowIfNull(tradeId);
        if (direction is not (TradingPlanDirection.Long or TradingPlanDirection.Short))
        {
            throw new ArgumentException("Position direction must be directional.", nameof(direction));
        }

        if (quantity <= 0 || entryPrice <= 0 || markPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        TradeId = tradeId;
        Direction = direction;
        Quantity = quantity;
        EntryPrice = entryPrice;
        EntryTimestampUtc = entryTimestampUtc;
        MarkPrice = markPrice;
        UnrealizedPnl = unrealizedPnl;
    }

    public PaperTradeId TradeId { get; }
    public TradingPlanDirection Direction { get; }
    public decimal Quantity { get; }
    public decimal EntryPrice { get; }
    public DateTimeOffset EntryTimestampUtc { get; }
    public decimal MarkPrice { get; }
    public decimal UnrealizedPnl { get; }
}

public sealed record PaperEquityPoint
{
    public PaperEquityPoint(DateTimeOffset timestampUtc, decimal equity, decimal realizedPnl, decimal unrealizedPnl)
    {
        TimestampUtc = timestampUtc;
        Equity = equity;
        RealizedPnl = realizedPnl;
        UnrealizedPnl = unrealizedPnl;
    }

    public DateTimeOffset TimestampUtc { get; }
    public decimal Equity { get; }
    public decimal RealizedPnl { get; }
    public decimal UnrealizedPnl { get; }
}

public sealed record PaperTradeJournalEntry
{
    public PaperTradeJournalEntry(
        PaperTradeId tradeId,
        TradingPlanDirection direction,
        decimal quantity,
        decimal entryPrice,
        DateTimeOffset entryTimestampUtc,
        decimal? exitPrice,
        DateTimeOffset? exitTimestampUtc,
        PaperExitReason? exitReason,
        decimal realizedPnl)
    {
        ArgumentNullException.ThrowIfNull(tradeId);
        if (quantity <= 0 || entryPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (direction is not (TradingPlanDirection.Long or TradingPlanDirection.Short))
        {
            throw new ArgumentException("Journal direction must be directional.", nameof(direction));
        }

        TradeId = tradeId;
        Direction = direction;
        Quantity = quantity;
        EntryPrice = entryPrice;
        EntryTimestampUtc = entryTimestampUtc;
        ExitPrice = exitPrice;
        ExitTimestampUtc = exitTimestampUtc;
        ExitReason = exitReason;
        RealizedPnl = realizedPnl;
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
    public bool IsClosed => ExitPrice.HasValue && ExitTimestampUtc.HasValue && ExitReason.HasValue;
}

public sealed record PaperTradingStatistics
{
    public PaperTradingStatistics(
        int totalTrades,
        int closedTrades,
        int winningTrades,
        int losingTrades,
        int breakevenTrades,
        decimal grossProfit,
        decimal grossLoss,
        decimal netRealizedPnl,
        decimal maxDrawdown,
        decimal averageClosedPnl)
    {
        if (totalTrades < 0 || closedTrades < 0 || winningTrades < 0 || losingTrades < 0 || breakevenTrades < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTrades));
        }

        TotalTrades = totalTrades;
        ClosedTrades = closedTrades;
        WinningTrades = winningTrades;
        LosingTrades = losingTrades;
        BreakevenTrades = breakevenTrades;
        GrossProfit = grossProfit;
        GrossLoss = grossLoss;
        NetRealizedPnl = netRealizedPnl;
        MaxDrawdown = maxDrawdown;
        AverageClosedPnl = averageClosedPnl;
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
    public decimal WinRate => ClosedTrades == 0 ? 0 : (decimal)WinningTrades / ClosedTrades;
    public decimal ProfitFactor => GrossLoss == 0 ? (GrossProfit == 0 ? 0 : decimal.MaxValue) : GrossProfit / GrossLoss;
}

public sealed record PaperTradingEvent
{
    public PaperTradingEvent(
        int sequence,
        PaperTradingEventType type,
        DateTimeOffset timestampUtc,
        string sourceId,
        string description,
        PaperOrderId? orderId = null,
        PaperFillId? fillId = null,
        PaperTradeId? tradeId = null)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Sequence = sequence;
        Type = type;
        TimestampUtc = timestampUtc;
        SourceId = sourceId.Trim();
        Description = description.Trim();
        OrderId = orderId;
        FillId = fillId;
        TradeId = tradeId;
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

public sealed record PaperTradingWarning
{
    public PaperTradingWarning(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
    }

    public string Code { get; }
    public string Message { get; }
}

public sealed record PaperTradingError
{
    public PaperTradingError(string code, string message, bool blocking = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
        Blocking = blocking;
    }

    public string Code { get; }
    public string Message { get; }
    public bool Blocking { get; }
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
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(fills);
        ArgumentNullException.ThrowIfNull(equityCurve);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentException.ThrowIfNullOrWhiteSpace(replayFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        if (completedAtUtc < createdAtUtc)
        {
            throw new ArgumentException("Simulation completion cannot precede creation.", nameof(completedAtUtc));
        }

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        SessionId = sessionId;
        Workspace = workspace;
        Plan = plan;
        Status = status;
        Orders = Array.AsReadOnly(orders.ToArray());
        Fills = Array.AsReadOnly(fills.ToArray());
        OpenPosition = openPosition;
        RealizedPnl = realizedPnl;
        UnrealizedPnl = unrealizedPnl;
        EquityCurve = Array.AsReadOnly(equityCurve.ToArray());
        Journal = Array.AsReadOnly(journal.ToArray());
        Statistics = statistics;
        Timeline = Array.AsReadOnly(timeline.OrderBy(item => item.Sequence).ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
        Errors = Array.AsReadOnly(errors.ToArray());
        ReplayFingerprint = replayFingerprint.Trim();
        Summary = summary.Trim();
        CreatedAtUtc = createdAtUtc;
        CompletedAtUtc = completedAtUtc;
        SchemaVersion = schemaVersion;
    }

    public PaperTradingSessionId SessionId { get; }
    public TradingWorkspaceResult Workspace { get; }
    public TradingPlanResult Plan { get; }
    public PaperTradingStatus Status { get; }
    public IReadOnlyList<PaperOrder> Orders { get; }
    public IReadOnlyList<PaperFill> Fills { get; }
    public PaperPositionSnapshot? OpenPosition { get; }
    public decimal RealizedPnl { get; }
    public decimal UnrealizedPnl { get; }
    public IReadOnlyList<PaperEquityPoint> EquityCurve { get; }
    public IReadOnlyList<PaperTradeJournalEntry> Journal { get; }
    public PaperTradingStatistics Statistics { get; }
    public IReadOnlyList<PaperTradingEvent> Timeline { get; }
    public IReadOnlyList<PaperTradingWarning> Warnings { get; }
    public IReadOnlyList<PaperTradingError> Errors { get; }
    public string ReplayFingerprint { get; }
    public string Summary { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public int SchemaVersion { get; }
}
