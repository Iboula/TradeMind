using System.Globalization;

namespace TradeMind.Market.Abstractions;

public sealed record SnapshotId
{
    public SnapshotId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Snapshot id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static SnapshotId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public sealed record ConnectorId
{
    public ConnectorId(string value)
    {
        Value = MarketValueObject.Normalize(value, nameof(value)).ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record ExternalAccountReference
{
    public ExternalAccountReference(string value)
    {
        Value = MarketValueObject.Normalize(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record ExternalPositionId
{
    public ExternalPositionId(string value)
    {
        Value = MarketValueObject.Normalize(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record ExternalOrderId
{
    public ExternalOrderId(string value)
    {
        Value = MarketValueObject.Normalize(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record Instrument
{
    public Instrument(string value)
    {
        var normalized = MarketValueObject.Normalize(value, nameof(value));
        if (normalized.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Instrument cannot contain whitespace.", nameof(value));
        }

        Symbol = normalized.ToUpperInvariant();
    }

    public string Symbol { get; }

    public override string ToString() => Symbol;
}

public readonly record struct Price
{
    public Price(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Price cannot be negative.");
        }

        Value = value;
    }

    public decimal Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed record DrawingId
{
    public DrawingId(string value)
    {
        Value = MarketValueObject.Normalize(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record IndicatorName
{
    public IndicatorName(string value)
    {
        Value = MarketValueObject.Normalize(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record IndicatorInstanceId
{
    public IndicatorInstanceId(string value)
    {
        Value = MarketValueObject.Normalize(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

internal static class MarketValueObject
{
    public static string Normalize(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}
