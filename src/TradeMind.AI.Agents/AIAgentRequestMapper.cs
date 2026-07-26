using Microsoft.Extensions.Options;
using TradeMind.AI.Application;
using TradeMind.AI.Memory;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Agents;

public sealed class AIAgentRequestMapper : IAIAgentRequestMapper
{
    private readonly AIAgentFrameworkOptions _options;

    public AIAgentRequestMapper(IOptions<AIAgentFrameworkOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public AIOrchestrationRequest Map(
        AIAgentDefinition definition,
        AIAgentExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);
        AIAgentPolicyGuard.Validate(definition, request, _options);

        var promptVariables = new Dictionary<string, string>(
            definition.PromptPolicy.DefaultVariables,
            StringComparer.Ordinal);
        foreach (var variable in request.PromptVariables)
        {
            promptVariables[variable.Key] = variable.Value;
        }

        var templateId = request.PromptTemplateId ?? definition.PromptPolicy.TemplateId;
        var templateVersion = request.PromptTemplateId is null
            ? definition.PromptPolicy.TemplateVersion
            : request.PromptTemplateVersion;

        var memory = MapMemory(definition.MemoryPolicy, request.MemoryOptions);
        var knowledge = MapKnowledge(definition.KnowledgePolicy, request.KnowledgeOptions);
        var tool = MapTool(definition.ToolPolicy, request.ToolInvocation, request.Permissions);

        return new AIOrchestrationRequest(
            definition.PromptPolicy.StaticSystemInstruction,
            request.UserMessage,
            request.Scenario,
            metadata: request.Metadata,
            correlationId: request.CorrelationId)
        {
            PromptTemplateId = templateId,
            PromptTemplateVersion = templateVersion,
            PromptVariables = promptVariables,
            UseMemory = memory.Enabled,
            Memory = memory,
            Knowledge = knowledge,
            Tool = tool,
            SessionId = request.SessionId,
            ConversationId = request.ConversationId,
            Identity = new AIIdentityContext(
                request.TenantId,
                request.UserId,
                definition.Id.Value,
                request.Identity.Metadata)
        };
    }

    private static AIMemoryOptions MapMemory(
        AIAgentMemoryPolicy policy,
        AIAgentMemoryRequestOptions? requested)
    {
        var enabled = policy.Required || requested?.Enabled == true;
        if (!enabled)
        {
            return AIMemoryOptions.Disabled;
        }

        var window = requested?.Window ?? policy.DefaultWindowOptions;
        return new AIMemoryOptions(
            enabled: true,
            new AIMemoryWindowOptions(
                window.MaxEntries,
                window.MaxCharacters,
                window.MaxEstimatedTokens,
                window.IncludeSystemMessages,
                window.IncludeSummary,
                window.RecentUserMessagesMinimum,
                window.RecentAssistantMessagesMinimum),
            policy.SaveUserMessage,
            policy.SaveAssistantResponse,
            policy.CompactionEnabled,
            policy.FailureMode == MemoryFailureMode.ContinueWithoutMemory
                ? AIMemoryFailureMode.ContinueWithoutMemory
                : AIMemoryFailureMode.FailClosed);
    }

    private static AIKnowledgeOptions MapKnowledge(
        AIAgentKnowledgePolicy policy,
        AIKnowledgeOptions? requested)
    {
        var enabled = policy.Required || requested?.Enabled == true;
        if (!enabled)
        {
            return AIKnowledgeOptions.Disabled;
        }

        return new AIKnowledgeOptions(
            enabled: true,
            requested?.Query,
            requested?.MaxResults ?? policy.DefaultMaxResults,
            requested?.MinimumScore ?? policy.MinimumScore,
            requested?.MaxCharacters ?? policy.MaxCharacters,
            requested?.MaxEstimatedTokens ?? policy.MaxEstimatedTokens,
            requested?.IncludeSourceMetadata ?? false,
            requested?.IncludeCitations ?? policy.IncludeCitations,
            requested?.FailureMode ?? policy.FailureMode,
            requested?.OrderingStrategy ?? KnowledgeOrderingStrategy.SourceThenSequence,
            requested?.Filters,
            requested?.UseCurrentUserMessageAsQuery ?? policy.UseCurrentUserMessageAsQuery,
            requested?.ContextLabel);
    }

    private static AIToolInvocationOptions MapTool(
        AIAgentToolPolicy policy,
        AIToolInvocationOptions? requested,
        IReadOnlyCollection<string> callerPermissions)
    {
        if (requested is null || !requested.Enabled)
        {
            return AIToolInvocationOptions.Disabled;
        }

        return new AIToolInvocationOptions(
            enabled: true,
            requested.ToolId,
            requested.Arguments,
            callerPermissions,
            requested.MaximumAllowedSideEffectLevel,
            requested.TimeoutOverride,
            requested.IdempotencyKey,
            requested.FailureMode == AIToolFailureMode.FailClosed
                ? AIToolFailureMode.FailClosed
                : policy.DefaultFailureMode);
    }
}
