namespace TradeMind.AI.Application;

public sealed record PromptRenderRequest
{
    public PromptRenderRequest(
        PromptTemplateId templateId,
        string scenario,
        IReadOnlyDictionary<string, string>? variables = null,
        PromptTemplateVersion? version = null,
        string? culture = null,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(templateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

        TemplateId = templateId;
        Version = version;
        Scenario = scenario.Trim();
        Variables = variables is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(variables, StringComparer.Ordinal);
        Culture = string.IsNullOrWhiteSpace(culture) ? null : culture.Trim();
        CorrelationId = correlationId;
    }

    public PromptTemplateId TemplateId { get; }

    public PromptTemplateVersion? Version { get; }

    public string Scenario { get; }

    public IReadOnlyDictionary<string, string> Variables { get; }

    public string? Culture { get; }

    public string? CorrelationId { get; }
}
