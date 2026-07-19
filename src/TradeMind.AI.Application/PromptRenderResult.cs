namespace TradeMind.AI.Application;

public sealed record PromptRenderResult
{
    public PromptRenderResult(
        PromptTemplateId templateId,
        PromptTemplateVersion version,
        string scenario,
        IReadOnlyList<PromptRenderedMessage> messages,
        IReadOnlyDictionary<string, string> variablesUsed,
        IReadOnlyList<string> sensitiveVariableNames,
        DateTimeOffset renderedAtUtc,
        string? correlationId,
        string? fingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(templateId);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

        if (renderedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Rendered date must be UTC.", nameof(renderedAtUtc));
        }

        if (messages.Count == 0 || messages.All(message => message.Role != PromptMessageRole.User))
        {
            throw new PromptRenderingException(templateId, version, "A rendered prompt must contain at least one user message.", correlationId);
        }

        TemplateId = templateId;
        Version = version;
        Scenario = scenario.Trim();
        Messages = messages.ToArray();
        VariablesUsed = new Dictionary<string, string>(variablesUsed, StringComparer.Ordinal);
        SensitiveVariableNames = sensitiveVariableNames.ToArray();
        RenderedAtUtc = renderedAtUtc;
        CorrelationId = correlationId;
        Fingerprint = fingerprint;
    }

    public PromptTemplateId TemplateId { get; }

    public PromptTemplateVersion Version { get; }

    public string Scenario { get; }

    public IReadOnlyList<PromptRenderedMessage> Messages { get; }

    public IReadOnlyDictionary<string, string> VariablesUsed { get; }

    public IReadOnlyList<string> SensitiveVariableNames { get; }

    public DateTimeOffset RenderedAtUtc { get; }

    public string? CorrelationId { get; }

    public string? Fingerprint { get; }
}
