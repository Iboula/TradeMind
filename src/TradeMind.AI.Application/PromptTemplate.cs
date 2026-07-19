namespace TradeMind.AI.Application;

public sealed record PromptTemplate
{
    public PromptTemplate(
        PromptTemplateId id,
        string name,
        PromptTemplateVersion version,
        string? description,
        string scenario,
        IReadOnlyList<PromptMessageTemplate> messages,
        IReadOnlyList<PromptVariableDefinition> variables,
        DateTimeOffset createdAtUtc,
        IReadOnlyDictionary<string, string>? metadata = null,
        bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Template creation date must be UTC.", nameof(createdAtUtc));
        }

        var orderedMessages = messages
            .OrderBy(message => message.Order)
            .ToArray();
        if (orderedMessages.Length == 0)
        {
            throw new ArgumentException("A prompt template must contain at least one message.", nameof(messages));
        }

        if (orderedMessages.All(message => message.Role != PromptMessageRole.User))
        {
            throw new ArgumentException("A prompt template must contain at least one user message.", nameof(messages));
        }

        var duplicateVariable = variables
            .GroupBy(variable => variable.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateVariable is not null)
        {
            throw new ArgumentException($"Prompt variable '{duplicateVariable.Key}' is defined more than once.", nameof(variables));
        }

        Id = id;
        Name = name.Trim();
        Version = version;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Scenario = scenario.Trim();
        Messages = orderedMessages;
        Variables = variables.ToArray();
        CreatedAtUtc = createdAtUtc;
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        IsActive = isActive;
    }

    public PromptTemplateId Id { get; }

    public string Name { get; }

    public PromptTemplateVersion Version { get; }

    public string? Description { get; }

    public string Scenario { get; }

    public IReadOnlyList<PromptMessageTemplate> Messages { get; }

    public IReadOnlyList<PromptVariableDefinition> Variables { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public bool IsActive { get; }
}
