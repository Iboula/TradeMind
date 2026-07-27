namespace TradeMind.Identity.Domain.Organizations;

public readonly record struct OrganizationId
{
    public OrganizationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Organization identifier is required.", nameof(value));
        Value = value.Trim();
        if (Value.Length > 128) throw new ArgumentOutOfRangeException(nameof(value), "Organization identifier is too long.");
    }

    public string Value { get; }
    public override string ToString() => Value;
}
