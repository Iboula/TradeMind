namespace TradeMind.Brokers.Domain;

internal static class BrokerValidation
{
    public static string Required(string value, string parameterName, int maximumLength = 256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    public static string? Optional(string? value, int maximumLength = 256)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length > maximumLength
            ? throw new ArgumentOutOfRangeException(nameof(value), $"Value cannot exceed {maximumLength} characters.")
            : normalized;
    }
}

public sealed record BrokerId
{
    public BrokerId(string value) => Value = BrokerValidation.Required(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record BrokerConnectorId
{
    public BrokerConnectorId(string value) => Value = BrokerValidation.Required(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record BrokerAccountId
{
    public BrokerAccountId(string value) => Value = BrokerValidation.Required(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record BrokerOrderId
{
    public BrokerOrderId(string value) => Value = BrokerValidation.Required(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record BrokerPositionId
{
    public BrokerPositionId(string value) => Value = BrokerValidation.Required(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record BrokerExecutionId
{
    public BrokerExecutionId(string value) => Value = BrokerValidation.Required(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record BrokerFillId
{
    public BrokerFillId(string value) => Value = BrokerValidation.Required(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record BrokerReconciliationId
{
    public BrokerReconciliationId(string value) => Value = BrokerValidation.Required(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}
