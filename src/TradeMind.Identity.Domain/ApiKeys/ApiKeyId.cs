namespace TradeMind.Identity.Domain.ApiKeys;

public readonly record struct ApiKeyId
{
    public ApiKeyId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("API key identifier is required.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public override string ToString() => Value.ToString("D");
}
