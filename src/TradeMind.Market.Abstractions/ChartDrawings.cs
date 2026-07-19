namespace TradeMind.Market.Abstractions;

public enum ChartDrawingType
{
    HorizontalLine,
    VerticalLine,
    TrendLine,
    Rectangle,
    Text,
    RiskRewardBox
}

public abstract record ChartDrawing
{
    protected ChartDrawing(DrawingId id, ChartDrawingType type, string? label)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
        Type = type;
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
    }

    public DrawingId Id { get; }
    public ChartDrawingType Type { get; }
    public string? Label { get; }
}

public sealed record HorizontalLineDrawing : ChartDrawing
{
    public HorizontalLineDrawing(DrawingId id, Price price, string? label = null)
        : base(id, ChartDrawingType.HorizontalLine, label)
    {
        Price = price;
    }

    public Price Price { get; }
}

public sealed record VerticalLineDrawing : ChartDrawing
{
    public VerticalLineDrawing(DrawingId id, DateTimeOffset time, string? label = null)
        : base(id, ChartDrawingType.VerticalLine, label)
    {
        Time = time;
    }

    public DateTimeOffset Time { get; }
}

public sealed record TrendLineDrawing : ChartDrawing
{
    public TrendLineDrawing(
        DrawingId id,
        DateTimeOffset startTime,
        Price startPrice,
        DateTimeOffset endTime,
        Price endPrice,
        string? label = null)
        : base(id, ChartDrawingType.TrendLine, label)
    {
        MarketGeometry.ValidatePeriod(startTime, endTime);
        StartTime = startTime;
        StartPrice = startPrice;
        EndTime = endTime;
        EndPrice = endPrice;
    }

    public DateTimeOffset StartTime { get; }
    public Price StartPrice { get; }
    public DateTimeOffset EndTime { get; }
    public Price EndPrice { get; }
}

public sealed record RectangleDrawing : ChartDrawing
{
    public RectangleDrawing(
        DrawingId id,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        Price lowerPrice,
        Price upperPrice,
        string? label = null)
        : base(id, ChartDrawingType.Rectangle, label)
    {
        MarketGeometry.ValidatePeriod(startTime, endTime);
        if (upperPrice.Value < lowerPrice.Value)
        {
            throw new ArgumentException("Rectangle upper price cannot be below its lower price.", nameof(upperPrice));
        }

        StartTime = startTime;
        EndTime = endTime;
        LowerPrice = lowerPrice;
        UpperPrice = upperPrice;
    }

    public DateTimeOffset StartTime { get; }
    public DateTimeOffset EndTime { get; }
    public Price LowerPrice { get; }
    public Price UpperPrice { get; }
}

public sealed record TextDrawing : ChartDrawing
{
    public TextDrawing(DrawingId id, DateTimeOffset time, Price? price, string text, string? label = null)
        : base(id, ChartDrawingType.Text, label)
    {
        Text = MarketValueObject.Normalize(text, nameof(text));
        Time = time;
        Price = price;
    }

    public DateTimeOffset Time { get; }
    public Price? Price { get; }
    public string Text { get; }
}

public sealed record RiskRewardBoxDrawing : ChartDrawing
{
    public RiskRewardBoxDrawing(
        DrawingId id,
        MarketDirection direction,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        Price entryPrice,
        Price stopPrice,
        Price targetPrice,
        string? label = null)
        : base(id, ChartDrawingType.RiskRewardBox, label)
    {
        MarketGeometry.ValidatePeriod(startTime, endTime);
        ValidatePrices(direction, entryPrice, stopPrice, targetPrice);
        Direction = direction;
        StartTime = startTime;
        EndTime = endTime;
        EntryPrice = entryPrice;
        StopPrice = stopPrice;
        TargetPrice = targetPrice;
    }

    public MarketDirection Direction { get; }
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset EndTime { get; }
    public Price EntryPrice { get; }
    public Price StopPrice { get; }
    public Price TargetPrice { get; }

    private static void ValidatePrices(
        MarketDirection direction,
        Price entryPrice,
        Price stopPrice,
        Price targetPrice)
    {
        var isValid = direction switch
        {
            MarketDirection.Long => stopPrice.Value < entryPrice.Value && entryPrice.Value < targetPrice.Value,
            MarketDirection.Short => targetPrice.Value < entryPrice.Value && entryPrice.Value < stopPrice.Value,
            _ => false
        };

        if (!isValid)
        {
            throw new ArgumentException("Risk/reward prices are inconsistent with the selected direction.", nameof(direction));
        }
    }
}

internal static class MarketGeometry
{
    public static void ValidatePeriod(DateTimeOffset startTime, DateTimeOffset endTime)
    {
        if (endTime < startTime)
        {
            throw new ArgumentException("Drawing end time cannot precede start time.", nameof(endTime));
        }
    }
}
