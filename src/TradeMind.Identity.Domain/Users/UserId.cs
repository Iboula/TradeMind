namespace TradeMind.Identity.Domain.Users;

public readonly record struct UserId
{
    public UserId(string value)
    {
        Value = Normalize(value, nameof(value), 128);
        if (Value.Contains('@', StringComparison.Ordinal)) throw new ArgumentException("UserId must not be an email address.", nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;

    private static string Normalize(string value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("The identifier is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName, "The identifier is too long.");
        return normalized;
    }
}
