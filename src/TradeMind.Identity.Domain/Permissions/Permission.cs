namespace TradeMind.Identity.Domain.Permissions;

public readonly record struct Permission
{
    public Permission(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Permission is required.", nameof(value));
        Value = value.Trim();
        if (Value.Length > 256 || Value.Contains('*', StringComparison.Ordinal)) throw new ArgumentException("Permission is invalid.", nameof(value));
    }

    public string Value { get; }
    public override string ToString() => Value;
}
