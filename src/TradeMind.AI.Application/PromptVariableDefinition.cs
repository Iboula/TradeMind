namespace TradeMind.AI.Application;

public sealed record PromptVariableDefinition
{
    public PromptVariableDefinition(
        string name,
        PromptVariableType type,
        bool required,
        string? description = null,
        string? defaultValue = null,
        bool isSensitive = false,
        int? maxLength = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (maxLength is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "MaxLength must be greater than 0 when provided.");
        }

        Name = name.Trim();
        Type = type;
        Required = required;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DefaultValue = defaultValue;
        IsSensitive = isSensitive;
        MaxLength = maxLength;
    }

    public string Name { get; }

    public PromptVariableType Type { get; }

    public bool Required { get; }

    public string? Description { get; }

    public string? DefaultValue { get; }

    public bool IsSensitive { get; }

    public int? MaxLength { get; }
}
