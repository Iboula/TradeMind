namespace TradeMind.Identity.Domain.Tenancy;

public readonly record struct TenantId
{
    public TenantId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Tenant identifier is required.", nameof(value));
        Value = value.Trim();
        if (Value.Length > 128) throw new ArgumentOutOfRangeException(nameof(value), "Tenant identifier is too long.");
    }

    public string Value { get; }
    public override string ToString() => Value;
}
