namespace TradeMind.Market.Abstractions;

public enum MarketDirection
{
    Long,
    Short
}

public enum PendingOrderType
{
    Limit,
    Stop,
    StopLimit
}

public sealed record TradingPosition
{
    public TradingPosition(
        ExternalPositionId id,
        Instrument instrument,
        MarketDirection direction,
        decimal volume,
        Price entryPrice,
        Price? stopLoss,
        Price? takeProfit,
        DateTimeOffset openedAt,
        decimal? unrealizedProfitLoss)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(instrument);
        if (volume <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), "Position volume must be greater than zero.");
        }

        Id = id;
        Instrument = instrument;
        Direction = direction;
        Volume = volume;
        EntryPrice = entryPrice;
        StopLoss = stopLoss;
        TakeProfit = takeProfit;
        OpenedAt = openedAt;
        UnrealizedProfitLoss = unrealizedProfitLoss;
    }

    public ExternalPositionId Id { get; }
    public Instrument Instrument { get; }
    public MarketDirection Direction { get; }
    public decimal Volume { get; }
    public Price EntryPrice { get; }
    public Price? StopLoss { get; }
    public Price? TakeProfit { get; }
    public DateTimeOffset OpenedAt { get; }
    public decimal? UnrealizedProfitLoss { get; }
}

public sealed record PendingOrder
{
    public PendingOrder(
        ExternalOrderId id,
        Instrument instrument,
        PendingOrderType type,
        MarketDirection direction,
        decimal volume,
        Price requestedPrice,
        Price? stopLoss,
        Price? takeProfit,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(instrument);
        if (volume <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), "Pending order volume must be greater than zero.");
        }

        if (expiresAt < createdAt)
        {
            throw new ArgumentException("Expiration cannot precede creation.", nameof(expiresAt));
        }

        Id = id;
        Instrument = instrument;
        Type = type;
        Direction = direction;
        Volume = volume;
        RequestedPrice = requestedPrice;
        StopLoss = stopLoss;
        TakeProfit = takeProfit;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public ExternalOrderId Id { get; }
    public Instrument Instrument { get; }
    public PendingOrderType Type { get; }
    public MarketDirection Direction { get; }
    public decimal Volume { get; }
    public Price RequestedPrice { get; }
    public Price? StopLoss { get; }
    public Price? TakeProfit { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ExpiresAt { get; }
}
