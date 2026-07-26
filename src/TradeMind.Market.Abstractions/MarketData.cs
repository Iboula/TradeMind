namespace TradeMind.Market.Abstractions;

public sealed record MarketCandle
{
    public MarketCandle(
        DateTimeOffset openTime,
        Price open,
        Price high,
        Price low,
        Price close,
        long tickVolume,
        long? realVolume,
        bool isClosed)
    {
        if (high.Value < open.Value || high.Value < close.Value || high.Value < low.Value)
        {
            throw new ArgumentException("High must be greater than or equal to open, close, and low.", nameof(high));
        }

        if (low.Value > open.Value || low.Value > close.Value || low.Value > high.Value)
        {
            throw new ArgumentException("Low must be less than or equal to open, close, and high.", nameof(low));
        }

        if (tickVolume < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickVolume), "Tick volume cannot be negative.");
        }

        if (realVolume < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(realVolume), "Real volume cannot be negative.");
        }

        OpenTime = openTime;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        TickVolume = tickVolume;
        RealVolume = realVolume;
        IsClosed = isClosed;
    }

    public DateTimeOffset OpenTime { get; }
    public Price Open { get; }
    public Price High { get; }
    public Price Low { get; }
    public Price Close { get; }
    public long TickVolume { get; }
    public long? RealVolume { get; }
    public bool IsClosed { get; }
}

public sealed record MarketQuote
{
    public MarketQuote(Price bid, Price ask, Price? last, DateTimeOffset timestamp)
    {
        if (ask.Value < bid.Value)
        {
            throw new ArgumentException("Ask must be greater than or equal to bid.", nameof(ask));
        }

        Bid = bid;
        Ask = ask;
        Last = last;
        Timestamp = timestamp;
    }

    public Price Bid { get; }
    public Price Ask { get; }
    public Price? Last { get; }
    public DateTimeOffset Timestamp { get; }
    public Price Spread => new(Ask.Value - Bid.Value);
}
