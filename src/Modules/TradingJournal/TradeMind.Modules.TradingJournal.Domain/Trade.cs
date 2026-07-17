using TradeMind.BuildingBlocks.Domain;

namespace TradeMind.Modules.TradingJournal.Domain;

public sealed class Trade : AggregateRoot<Guid>
{
    private Trade(Guid id, string symbol, TradeDirection direction, decimal entryPrice, decimal quantity, DateTimeOffset openedAtUtc)
        : base(id)
    {
        Symbol = symbol;
        Direction = direction;
        EntryPrice = entryPrice;
        Quantity = quantity;
        OpenedAtUtc = openedAtUtc;
    }

    public string Symbol { get; private set; }
    public TradeDirection Direction { get; private set; }
    public decimal EntryPrice { get; private set; }
    public decimal Quantity { get; private set; }
    public DateTimeOffset OpenedAtUtc { get; private set; }
    public decimal? ExitPrice { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public string? Notes { get; private set; }

    public static Trade Open(string symbol, TradeDirection direction, decimal entryPrice, decimal quantity, DateTimeOffset openedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        return new Trade(Guid.NewGuid(), symbol.Trim().ToUpperInvariant(), direction, entryPrice, quantity, openedAtUtc);
    }

    public void Close(decimal exitPrice, DateTimeOffset closedAtUtc, string? notes = null)
    {
        if (ExitPrice is not null) throw new InvalidOperationException("Trade is already closed.");
        if (closedAtUtc < OpenedAtUtc) throw new ArgumentException("Close time cannot precede open time.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exitPrice);

        ExitPrice = exitPrice;
        ClosedAtUtc = closedAtUtc;
        Notes = notes?.Trim();
    }
}

public enum TradeDirection
{
    Long = 1,
    Short = 2
}
