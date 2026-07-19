namespace TradeMind.AI.Application;

public sealed record AIOrchestrationRequest
{
    public AIOrchestrationRequest(
        string? systemInstruction,
        string userMessage,
        string scenario,
        string? model = null,
        float? temperature = null,
        int? maxOutputTokens = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? correlationId = null)
    {
        SystemInstruction = systemInstruction;
        UserMessage = userMessage;
        Scenario = scenario;
        Model = model;
        Temperature = temperature;
        MaxOutputTokens = maxOutputTokens;
        Metadata = CopyMetadata(metadata);
        PromptVariables = new Dictionary<string, string>();
        CorrelationId = correlationId;
        Identity = AIIdentityContext.Empty;
    }

    public string? SystemInstruction { get; init; }

    public string UserMessage { get; init; }

    public string Scenario { get; init; }

    public string? Model { get; init; }

    public float? Temperature { get; init; }

    public int? MaxOutputTokens { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; }

    public PromptTemplateId? PromptTemplateId { get; init; }

    public PromptTemplateVersion? PromptTemplateVersion { get; init; }

    public IReadOnlyDictionary<string, string> PromptVariables { get; init; }

    public bool UseMemory { get; init; }

    public string? CorrelationId { get; init; }

    public string? SessionId { get; init; }

    public string? ConversationId { get; init; }

    public AIIdentityContext Identity { get; init; }

    public string? TenantId => Identity.TenantId;

    public string? UserId => Identity.UserId;

    public string? AgentId => Identity.AgentId;

    private static IReadOnlyDictionary<string, string> CopyMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        return metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
    }
}
