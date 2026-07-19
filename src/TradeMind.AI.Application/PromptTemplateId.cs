namespace TradeMind.AI.Application;

public sealed record PromptTemplateId
{
    public PromptTemplateId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
