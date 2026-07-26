using TradeMind.AI.Application;
using TradeMind.AI.Memory;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Agents;

public sealed record AIAgentMemoryRequestOptions
{
    public AIAgentMemoryRequestOptions(bool enabled, MemoryWindowOptions? window = null)
    {
        Enabled = enabled;
        Window = window;
    }

    public bool Enabled { get; }

    public MemoryWindowOptions? Window { get; }
}

public sealed record AIAgentIdentityContext
{
    public AIAgentIdentityContext(
        string? tenantId = null,
        string? userId = null,
        IReadOnlyCollection<string>? permissions = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        TenantId = Normalize(tenantId);
        UserId = Normalize(userId);
        Permissions = AIAgentCollections.CopyStrings(permissions, StringComparer.OrdinalIgnoreCase);
        Metadata = AIAgentCollections.CopyDictionary(metadata, StringComparer.OrdinalIgnoreCase, filterSensitiveKeys: true);
    }

    public string? TenantId { get; }

    public string? UserId { get; }

    public IReadOnlyCollection<string> Permissions { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record AIAgentExecutionRequest
{
    public AIAgentExecutionRequest(
        AIAgentId agentId,
        string userMessage,
        string scenario,
        DateTimeOffset requestedAtUtc,
        AIAgentVersionSelection versionSelection = AIAgentVersionSelection.LatestStable,
        AIAgentVersion? exactVersion = null,
        string? sessionId = null,
        string? conversationId = null,
        string? tenantId = null,
        string? userId = null,
        string? correlationId = null,
        IReadOnlyCollection<string>? permissions = null,
        IReadOnlyDictionary<string, string>? promptVariables = null,
        AIAgentMemoryRequestOptions? memoryOptions = null,
        AIKnowledgeOptions? knowledgeOptions = null,
        AIToolInvocationOptions? toolInvocation = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        TimeSpan? timeoutOverride = null,
        AIAgentCapabilities? requestedCapabilities = null,
        PromptTemplateId? promptTemplateId = null,
        PromptTemplateVersion? promptTemplateVersion = null)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

        if (requestedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Agent request date must be UTC.", nameof(requestedAtUtc));
        }

        if (versionSelection == AIAgentVersionSelection.Exact && exactVersion is null)
        {
            throw new ArgumentException("ExactVersion is required for exact version selection.", nameof(exactVersion));
        }

        if (versionSelection != AIAgentVersionSelection.Exact && exactVersion is not null)
        {
            throw new ArgumentException("ExactVersion can only be used with exact version selection.", nameof(exactVersion));
        }

        if (timeoutOverride is not null && timeoutOverride <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutOverride));
        }

        AgentId = agentId;
        VersionSelection = versionSelection;
        ExactVersion = exactVersion;
        UserMessage = userMessage;
        SessionId = Normalize(sessionId);
        ConversationId = Normalize(conversationId);
        TenantId = Normalize(tenantId);
        UserId = Normalize(userId);
        CorrelationId = Normalize(correlationId);
        Scenario = scenario.Trim();
        Permissions = AIAgentCollections.CopyStrings(permissions, StringComparer.OrdinalIgnoreCase);
        PromptVariables = AIAgentCollections.CopyDictionary(promptVariables, StringComparer.Ordinal);
        MemoryOptions = memoryOptions;
        KnowledgeOptions = knowledgeOptions;
        ToolInvocation = toolInvocation;
        Metadata = AIAgentCollections.CopyDictionary(metadata, StringComparer.OrdinalIgnoreCase, filterSensitiveKeys: true);
        RequestedAtUtc = requestedAtUtc;
        TimeoutOverride = timeoutOverride;
        RequestedCapabilities = requestedCapabilities ?? AIAgentCapabilities.None;
        PromptTemplateId = promptTemplateId;
        PromptTemplateVersion = promptTemplateVersion;
        Identity = new AIAgentIdentityContext(TenantId, UserId, Permissions, Metadata);
    }

    public AIAgentId AgentId { get; }

    public AIAgentVersionSelection VersionSelection { get; }

    public AIAgentVersion? ExactVersion { get; }

    public string UserMessage { get; }

    public string? SessionId { get; }

    public string? ConversationId { get; }

    public string? TenantId { get; }

    public string? UserId { get; }

    public string? CorrelationId { get; }

    public string Scenario { get; }

    public IReadOnlyCollection<string> Permissions { get; }

    public IReadOnlyDictionary<string, string> PromptVariables { get; }

    public AIAgentMemoryRequestOptions? MemoryOptions { get; }

    public AIKnowledgeOptions? KnowledgeOptions { get; }

    public AIToolInvocationOptions? ToolInvocation { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public DateTimeOffset RequestedAtUtc { get; }

    public TimeSpan? TimeoutOverride { get; }

    public AIAgentCapabilities RequestedCapabilities { get; }

    public PromptTemplateId? PromptTemplateId { get; }

    public PromptTemplateVersion? PromptTemplateVersion { get; }

    public AIAgentIdentityContext Identity { get; }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record AIAgentAuthorizationContext
{
    public AIAgentAuthorizationContext(
        string? tenantId,
        string? userId,
        IReadOnlyCollection<string>? permissions,
        string scenario,
        AIToolSideEffectLevel maximumAllowedSideEffectLevel,
        AIAgentCapabilities? requestedCapabilities = null,
        bool developmentAgentsEnabled = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
        TenantId = Normalize(tenantId);
        UserId = Normalize(userId);
        Permissions = AIAgentCollections.CopyStrings(permissions, StringComparer.OrdinalIgnoreCase);
        Scenario = scenario.Trim();
        MaximumAllowedSideEffectLevel = maximumAllowedSideEffectLevel;
        RequestedCapabilities = requestedCapabilities ?? AIAgentCapabilities.None;
        DevelopmentAgentsEnabled = developmentAgentsEnabled;
    }

    public string? TenantId { get; }

    public string? UserId { get; }

    public IReadOnlyCollection<string> Permissions { get; }

    public string Scenario { get; }

    public AIToolSideEffectLevel MaximumAllowedSideEffectLevel { get; }

    public AIAgentCapabilities RequestedCapabilities { get; }

    public bool DevelopmentAgentsEnabled { get; }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record AIAgentDiscoveryContext
{
    public AIAgentDiscoveryContext(
        IReadOnlyCollection<string>? permissions = null,
        string? tenantId = null,
        string? userId = null,
        string? scenario = null,
        AIAgentCapabilities? requiredCapabilities = null,
        IReadOnlyCollection<string>? tags = null,
        IReadOnlyCollection<AIAgentAvailability>? availabilities = null,
        AIToolSideEffectLevel maximumAllowedSideEffectLevel = AIToolSideEffectLevel.ReadOnly,
        bool includeDevelopmentAgents = false)
    {
        Permissions = AIAgentCollections.CopyStrings(permissions, StringComparer.OrdinalIgnoreCase);
        TenantId = Normalize(tenantId);
        UserId = Normalize(userId);
        Scenario = Normalize(scenario);
        RequiredCapabilities = requiredCapabilities ?? AIAgentCapabilities.None;
        Tags = AIAgentCollections.CopyStrings(tags, StringComparer.OrdinalIgnoreCase);
        Availabilities = Array.AsReadOnly(availabilities?.Distinct().ToArray() ?? [AIAgentAvailability.Enabled]);
        MaximumAllowedSideEffectLevel = maximumAllowedSideEffectLevel;
        IncludeDevelopmentAgents = includeDevelopmentAgents;
    }

    public IReadOnlyCollection<string> Permissions { get; }

    public string? TenantId { get; }

    public string? UserId { get; }

    public string? Scenario { get; }

    public AIAgentCapabilities RequiredCapabilities { get; }

    public IReadOnlyCollection<string> Tags { get; }

    public IReadOnlyCollection<AIAgentAvailability> Availabilities { get; }

    public AIToolSideEffectLevel MaximumAllowedSideEffectLevel { get; }

    public bool IncludeDevelopmentAgents { get; }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record AIAgentExecutionMetrics
{
    public AIAgentExecutionMetrics(
        TimeSpan resolutionDuration,
        TimeSpan authorizationDuration,
        TimeSpan mappingDuration,
        TimeSpan orchestrationDuration,
        TimeSpan totalDuration,
        bool promptUsed,
        bool memoryUsed,
        bool knowledgeUsed,
        bool toolUsed,
        string? provider,
        int? estimatedInputTokens = null,
        int? estimatedOutputTokens = null)
    {
        if (resolutionDuration < TimeSpan.Zero
            || authorizationDuration < TimeSpan.Zero
            || mappingDuration < TimeSpan.Zero
            || orchestrationDuration < TimeSpan.Zero
            || totalDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalDuration), "Agent durations cannot be negative.");
        }

        ResolutionDuration = resolutionDuration;
        AuthorizationDuration = authorizationDuration;
        MappingDuration = mappingDuration;
        OrchestrationDuration = orchestrationDuration;
        TotalDuration = totalDuration;
        PromptUsed = promptUsed;
        MemoryUsed = memoryUsed;
        KnowledgeUsed = knowledgeUsed;
        ToolUsed = toolUsed;
        Provider = Normalize(provider);
        EstimatedInputTokens = estimatedInputTokens;
        EstimatedOutputTokens = estimatedOutputTokens;
    }

    public static AIAgentExecutionMetrics Empty { get; } = new(
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        false,
        false,
        false,
        false,
        null);

    public TimeSpan ResolutionDuration { get; }

    public TimeSpan AuthorizationDuration { get; }

    public TimeSpan MappingDuration { get; }

    public TimeSpan OrchestrationDuration { get; }

    public TimeSpan TotalDuration { get; }

    public bool PromptUsed { get; }

    public bool MemoryUsed { get; }

    public bool KnowledgeUsed { get; }

    public bool ToolUsed { get; }

    public string? Provider { get; }

    public int? EstimatedInputTokens { get; }

    public int? EstimatedOutputTokens { get; }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record AIAgentError(string Code, string Message, string? CorrelationId = null);

public sealed record AIAgentExecutionResponse
{
    public AIAgentExecutionResponse(
        AIAgentId agentId,
        AIAgentVersion agentVersion,
        string sessionId,
        string? conversationId,
        string correlationId,
        string scenario,
        bool success,
        string content,
        AIAgentExecutionState state,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        string provider,
        bool memoryUsed,
        bool knowledgeUsed,
        bool toolUsed,
        IReadOnlyList<string>? citations,
        AIAgentExecutionMetrics metrics,
        string? toolId = null,
        AIAgentError? error = null)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(agentVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentNullException.ThrowIfNull(metrics);

        if (startedAtUtc.Offset != TimeSpan.Zero || completedAtUtc.Offset != TimeSpan.Zero || completedAtUtc < startedAtUtc)
        {
            throw new ArgumentException("Agent response dates must be ordered UTC values.");
        }

        AgentId = agentId;
        AgentVersion = agentVersion;
        SessionId = sessionId;
        ConversationId = Normalize(conversationId);
        CorrelationId = correlationId;
        Scenario = scenario;
        Success = success;
        Content = content;
        State = state;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Provider = provider;
        MemoryUsed = memoryUsed;
        KnowledgeUsed = knowledgeUsed;
        ToolUsed = toolUsed;
        Citations = Array.AsReadOnly(citations?.ToArray() ?? []);
        ToolId = Normalize(toolId);
        Error = error;
        Metrics = metrics;
    }

    public AIAgentId AgentId { get; init; }

    public AIAgentVersion AgentVersion { get; init; }

    public string SessionId { get; init; }

    public string? ConversationId { get; init; }

    public string CorrelationId { get; init; }

    public string Scenario { get; init; }

    public bool Success { get; init; }

    public string Content { get; init; }

    public AIAgentExecutionState State { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;

    public string Provider { get; init; }

    public bool MemoryUsed { get; init; }

    public bool KnowledgeUsed { get; init; }

    public bool ToolUsed { get; init; }

    public IReadOnlyList<string> Citations { get; init; }

    public string? ToolId { get; init; }

    public AIAgentError? Error { get; init; }

    public AIAgentExecutionMetrics Metrics { get; init; }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AIAgentExecutionContext
{
    public AIAgentExecutionContext(
        AIAgentDefinition definition,
        AIAgentExecutionRequest request,
        DateTimeOffset startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);
        if (startedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Agent execution start date must be UTC.", nameof(startedAtUtc));
        }

        Definition = definition;
        Request = request;
        Identity = request.Identity;
        SessionId = request.SessionId;
        CorrelationId = request.CorrelationId;
        EffectivePolicy = definition.Policy;
        StartedAtUtc = startedAtUtc;
    }

    public AIAgentDefinition Definition { get; }

    public AIAgentExecutionRequest Request { get; }

    public AIAgentIdentityContext Identity { get; }

    public string? SessionId { get; }

    public string? CorrelationId { get; }

    public AIAgentPolicy EffectivePolicy { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public AIAgentExecutionState State { get; internal set; } = AIAgentExecutionState.Created;

    public AIAgentExecutionMetrics Metrics { get; internal set; } = AIAgentExecutionMetrics.Empty;

    public AIAgentError? Error { get; internal set; }
}
