namespace TradeMind.AI.Agents;

public sealed record AIAgentId
{
    public const int MaximumLength = 64;

    public AIAgentId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaximumLength)
        {
            throw new ArgumentException($"Agent id must be {MaximumLength} characters or fewer.", nameof(value));
        }

        if (!IsLowercaseKebabCase(value))
        {
            throw new ArgumentException(
                "Agent id must use case-sensitive lowercase-kebab-case with ASCII letters and digits.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    private static bool IsLowercaseKebabCase(string value)
    {
        if (!IsLowercaseLetterOrDigit(value[0]) || !IsLowercaseLetterOrDigit(value[^1]))
        {
            return false;
        }

        var previousWasHyphen = false;
        foreach (var character in value)
        {
            if (character == '-')
            {
                if (previousWasHyphen)
                {
                    return false;
                }

                previousWasHyphen = true;
                continue;
            }

            if (!IsLowercaseLetterOrDigit(character))
            {
                return false;
            }

            previousWasHyphen = false;
        }

        return true;
    }

    private static bool IsLowercaseLetterOrDigit(char value) =>
        value is >= 'a' and <= 'z' or >= '0' and <= '9';
}
