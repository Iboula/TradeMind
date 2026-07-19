using TradeMind.AI.Application;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Agents;

public sealed record AIAgentDefinition
{
    public AIAgentDefinition(
        AIAgentId id,
        string name,
        string description,
        AIAgentVersion version,
        AIAgentAvailability availability,
        AIAgentCapabilities capabilities,
        AIAgentPolicy policy,
        IReadOnlyCollection<string>? requiredPermissions = null,
        IReadOnlyCollection<string>? supportedScenarios = null,
        IReadOnlyCollection<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        IReadOnlyCollection<string>? allowedTenantIds = null,
        IReadOnlyCollection<string>? allowedUserIds = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Id = id;
        Name = name.Trim();
        Description = description.Trim();
        Version = version;
        Availability = availability;
        Capabilities = capabilities;
        Policy = policy;
        RequiredPermissions = AIAgentCollections.CopyStrings(requiredPermissions, StringComparer.OrdinalIgnoreCase);
        SupportedScenarios = AIAgentCollections.CopyStrings(supportedScenarios, StringComparer.OrdinalIgnoreCase);
        Tags = AIAgentCollections.CopyStrings(tags, StringComparer.OrdinalIgnoreCase);
        Metadata = AIAgentCollections.CopyDictionary(metadata, StringComparer.OrdinalIgnoreCase, filterSensitiveKeys: true);
        AllowedTenantIds = AIAgentCollections.CopyStrings(allowedTenantIds, StringComparer.Ordinal);
        AllowedUserIds = AIAgentCollections.CopyStrings(allowedUserIds, StringComparer.Ordinal);

        ValidateCapabilities();
    }

    public AIAgentId Id { get; }

    public string Name { get; }

    public string Description { get; }

    public AIAgentVersion Version { get; }

    public AIAgentAvailability Availability { get; }

    public AIAgentCapabilities Capabilities { get; }

    public IReadOnlyCollection<string> RequiredPermissions { get; }

    public IReadOnlyCollection<string> SupportedScenarios { get; }

    public AIAgentPolicy Policy { get; }

    public AIAgentPromptPolicy PromptPolicy => Policy.Prompt;

    public AIAgentMemoryPolicy MemoryPolicy => Policy.Memory;

    public AIAgentKnowledgePolicy KnowledgePolicy => Policy.Knowledge;

    public AIAgentToolPolicy ToolPolicy => Policy.Tool;

    public AIAgentFailureMode FailureMode => Policy.FailureMode;

    public TimeSpan? MaximumExecutionDuration => Policy.MaximumExecutionDuration;

    public IReadOnlyCollection<string> Tags { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public IReadOnlyCollection<string> AllowedTenantIds { get; }

    public IReadOnlyCollection<string> AllowedUserIds { get; }

    public AIToolSideEffectLevel MaximumAllowedSideEffectLevel => Policy.MaximumAllowedSideEffectLevel;

    private void ValidateCapabilities()
    {
        if (MemoryPolicy.Enabled && !Capabilities.SupportsMemory)
        {
            throw new ArgumentException("Memory policy requires the memory capability.", nameof(Capabilities));
        }

        if (KnowledgePolicy.Enabled && !Capabilities.SupportsKnowledge)
        {
            throw new ArgumentException("Knowledge policy requires the knowledge capability.", nameof(Capabilities));
        }

        if (ToolPolicy.Enabled && !Capabilities.SupportsTools)
        {
            throw new ArgumentException("Tool policy requires the tools capability.", nameof(Capabilities));
        }

        if (PromptPolicy.TemplateId is not null && !Capabilities.SupportsPromptTemplates)
        {
            throw new ArgumentException("Prompt template policy requires the prompt template capability.", nameof(Capabilities));
        }
    }
}
