namespace TradeMind.ExecutionSessions.Domain;

public sealed record ExecutionSessionId
{
    public ExecutionSessionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Execution session id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ExecutionSessionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record ExecutionCorrelationId
{
    public ExecutionCorrelationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > 128)
        {
            throw new ArgumentException("Correlation id cannot exceed 128 characters.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
