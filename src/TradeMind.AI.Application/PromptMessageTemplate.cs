namespace TradeMind.AI.Application;

public sealed record PromptMessageTemplate
{
    public PromptMessageTemplate(
        PromptMessageRole role,
        string contentTemplate,
        int order)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentTemplate);

        Role = role;
        ContentTemplate = contentTemplate;
        Order = order;
    }

    public PromptMessageRole Role { get; }

    public string ContentTemplate { get; }

    public int Order { get; }
}
