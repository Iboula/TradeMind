using Microsoft.Extensions.Options;
using TradeMind.AI.Application;
using TradeMind.AI.Memory;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Agents;

public sealed class PolicyBasedAIAgentAuthorizer : IAIAgentAuthorizer
{
    private readonly AIAgentFrameworkOptions _options;

    public PolicyBasedAIAgentAuthorizer(IOptions<AIAgentFrameworkOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public void Authorize(
        AIAgentDefinition definition,
        AIAgentExecutionRequest request,
        AIAgentAuthorizationContext context)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (definition.Availability == AIAgentAvailability.Disabled
            || definition.Availability == AIAgentAvailability.DevelopmentOnly
                && (!_options.EnableDevelopmentAgents || !context.DevelopmentAgentsEnabled))
        {
            throw new AIAgentUnavailableException(definition.Id, definition.Version, request.CorrelationId);
        }

        RequireAllPermissions(definition, context.Permissions, definition.RequiredPermissions);
        RequireAllPermissions(definition, context.Permissions, definition.ToolPolicy.RequiredPermissions);

        if (!IsAllowed(context.TenantId, definition.AllowedTenantIds))
        {
            throw AuthorizationDenied(definition, request, "The tenant is not authorized to execute this AI agent.");
        }

        if (!IsAllowed(context.UserId, definition.AllowedUserIds))
        {
            throw AuthorizationDenied(definition, request, "The user is not authorized to execute this AI agent.");
        }

        if (definition.SupportedScenarios.Count > 0
            && !definition.SupportedScenarios.Contains(context.Scenario, StringComparer.OrdinalIgnoreCase))
        {
            throw AuthorizationDenied(definition, request, "The requested scenario is not supported by this AI agent.");
        }

        if (!definition.Capabilities.Includes(context.RequestedCapabilities))
        {
            throw AuthorizationDenied(definition, request, "A requested capability is not supported by this AI agent.");
        }

        if (definition.MaximumAllowedSideEffectLevel > context.MaximumAllowedSideEffectLevel)
        {
            throw AuthorizationDenied(definition, request, "The AI agent side effect policy exceeds the caller authorization boundary.");
        }

        AIAgentPolicyGuard.Validate(definition, request, _options);
    }

    private static void RequireAllPermissions(
        AIAgentDefinition definition,
        IReadOnlyCollection<string> available,
        IReadOnlyCollection<string> required)
    {
        if (required.Any(permission => !available.Contains(permission, StringComparer.OrdinalIgnoreCase)))
        {
            throw new AIAgentAuthorizationException(
                definition.Id,
                definition.Version,
                "A required permission is missing.");
        }
    }

    private static bool IsAllowed(string? value, IReadOnlyCollection<string> allowedValues) =>
        allowedValues.Count == 0
        || value is not null && allowedValues.Contains(value, StringComparer.Ordinal);

    private static AIAgentAuthorizationException AuthorizationDenied(
        AIAgentDefinition definition,
        AIAgentExecutionRequest request,
        string message) =>
        new(definition.Id, definition.Version, message, request.CorrelationId);
}

internal static class AIAgentPolicyGuard
{
    public static void Validate(
        AIAgentDefinition definition,
        AIAgentExecutionRequest request,
        AIAgentFrameworkOptions options)
    {
        if (request.AgentId != definition.Id)
        {
            throw Violation(definition, request, "The resolved agent does not match the requested agent id.");
        }

        ValidatePrompt(definition, request);
        ValidateMemory(definition, request);
        ValidateKnowledge(definition, request);
        ValidateTool(definition, request, options);
        ValidateTimeout(definition, request, options);
        ValidateContextSize(definition, request);
    }

    private static void ValidatePrompt(AIAgentDefinition definition, AIAgentExecutionRequest request)
    {
        var policy = definition.PromptPolicy;
        var requestsDifferentTemplate = request.PromptTemplateId is not null
            && request.PromptTemplateId != policy.TemplateId;
        if (requestsDifferentTemplate && !policy.AllowRequestTemplateOverride)
        {
            throw Violation(definition, request, "Prompt template override is not allowed.");
        }

        if (request.PromptTemplateVersion is not null && request.PromptTemplateId is null)
        {
            throw Violation(definition, request, "Prompt template version override requires a template id.");
        }

        if (request.PromptVariables.ContainsKey("userMessage"))
        {
            throw Violation(definition, request, "The reserved userMessage prompt variable cannot be supplied explicitly.");
        }

        if (!policy.AllowRequestVariableOverride
            && request.PromptVariables.Any(variable =>
                policy.DefaultVariables.TryGetValue(variable.Key, out var defaultValue)
                && !string.Equals(defaultValue, variable.Value, StringComparison.Ordinal)))
        {
            throw Violation(definition, request, "Prompt variable override is not allowed.");
        }

        var effective = new Dictionary<string, string>(policy.DefaultVariables, StringComparer.Ordinal);
        foreach (var variable in request.PromptVariables)
        {
            effective[variable.Key] = variable.Value;
        }

        if (policy.RequiredVariables.Any(variable =>
                !string.Equals(variable, "userMessage", StringComparison.Ordinal)
                && (!effective.TryGetValue(variable, out var value) || string.IsNullOrWhiteSpace(value))))
        {
            throw Violation(definition, request, "A required prompt variable is missing.");
        }

        if (policy.MaximumRenderedCharacters is { } maximum
            && CalculatePromptCharacterCount(policy, request, effective) > maximum)
        {
            throw Violation(definition, request, "The prompt character budget was exceeded.");
        }
    }

    private static void ValidateMemory(AIAgentDefinition definition, AIAgentExecutionRequest request)
    {
        var policy = definition.MemoryPolicy;
        var requested = request.MemoryOptions;
        if (requested is null)
        {
            if (policy.Required && string.IsNullOrWhiteSpace(request.ConversationId))
            {
                throw Violation(definition, request, "ConversationId is required when memory is enabled.");
            }

            return;
        }

        if (requested.Enabled && !policy.Enabled)
        {
            throw Violation(definition, request, "Memory is not allowed for this AI agent.");
        }

        if (!requested.Enabled && policy.Required)
        {
            throw Violation(definition, request, "Required memory cannot be disabled.");
        }

        if (requested.Enabled && string.IsNullOrWhiteSpace(request.ConversationId))
        {
            throw Violation(definition, request, "ConversationId is required when memory is enabled.");
        }

        if (requested.Window is null)
        {
            return;
        }

        if (!policy.AllowRequestOverride)
        {
            throw Violation(definition, request, "Memory window override is not allowed.");
        }

        var maximum = policy.DefaultWindowOptions;
        var window = requested.Window;
        if (window.MaxEntries > maximum.MaxEntries
            || Exceeds(window.MaxCharacters, maximum.MaxCharacters)
            || Exceeds(window.MaxEstimatedTokens, maximum.MaxEstimatedTokens)
            || window.IncludeSystemMessages && !maximum.IncludeSystemMessages
            || window.IncludeSummary && !maximum.IncludeSummary
            || window.RecentUserMessagesMinimum > maximum.RecentUserMessagesMinimum
            || window.RecentAssistantMessagesMinimum > maximum.RecentAssistantMessagesMinimum)
        {
            throw Violation(definition, request, "The requested memory window exceeds the agent policy.");
        }
    }

    private static void ValidateKnowledge(AIAgentDefinition definition, AIAgentExecutionRequest request)
    {
        var policy = definition.KnowledgePolicy;
        var requested = request.KnowledgeOptions;
        if (requested is null)
        {
            return;
        }

        if (requested.Enabled && !policy.Enabled)
        {
            throw Violation(definition, request, "Knowledge retrieval is not allowed for this AI agent.");
        }

        if (!requested.Enabled && policy.Required)
        {
            throw Violation(definition, request, "Required knowledge retrieval cannot be disabled.");
        }

        if (!requested.Enabled)
        {
            return;
        }

        if (requested.MaxResults > policy.DefaultMaxResults
            || Exceeds(requested.MaxCharacters, policy.MaxCharacters)
            || Exceeds(requested.MaxEstimatedTokens, policy.MaxEstimatedTokens)
            || policy.MinimumScore is { } minimum
                && (requested.MinimumScore is null || requested.MinimumScore < minimum))
        {
            throw Violation(definition, request, "The requested knowledge budget exceeds the agent policy.");
        }

        if (requested.Filters.Keys.Any(filter =>
                !policy.AllowedFilters.Contains(filter, StringComparer.OrdinalIgnoreCase)))
        {
            throw Violation(definition, request, "A requested knowledge filter is not allowed.");
        }

        if (requested.Query is not null && !policy.AllowExplicitQuery)
        {
            throw Violation(definition, request, "Explicit knowledge queries are not allowed.");
        }

        if (requested.IncludeCitations && !policy.IncludeCitations)
        {
            throw Violation(definition, request, "Knowledge citations are not allowed.");
        }

        if (policy.FailureMode == KnowledgeFailureMode.FailClosed
            && requested.FailureMode == KnowledgeFailureMode.ContinueWithoutKnowledge)
        {
            throw Violation(definition, request, "Knowledge failure mode cannot be weakened.");
        }
    }

    private static void ValidateTool(
        AIAgentDefinition definition,
        AIAgentExecutionRequest request,
        AIAgentFrameworkOptions options)
    {
        var policy = definition.ToolPolicy;
        var requested = request.ToolInvocation;
        if (requested is null || !requested.Enabled)
        {
            return;
        }

        if (!policy.Enabled || !policy.AllowExplicitInvocation)
        {
            throw Violation(definition, request, "Explicit tool invocation is not allowed.");
        }

        if (requested.ToolId is null
            || !policy.AllowedToolIds.Contains(requested.ToolId))
        {
            throw Violation(definition, request, "The requested tool is not allowed.");
        }

        if (policy.AllowedScenarios.Count > 0
            && !policy.AllowedScenarios.Contains(request.Scenario, StringComparer.OrdinalIgnoreCase))
        {
            throw Violation(definition, request, "Tool invocation is not allowed for the requested scenario.");
        }

        if (requested.MaximumAllowedSideEffectLevel > policy.MaximumAllowedSideEffectLevel
            || requested.MaximumAllowedSideEffectLevel > definition.MaximumAllowedSideEffectLevel
            || requested.MaximumAllowedSideEffectLevel > options.MaximumAllowedSideEffectLevel)
        {
            throw Violation(definition, request, "The requested tool side effect level exceeds the authorization boundary.");
        }

        if (policy.MaximumTimeout is { } maximumTimeout
            && requested.TimeoutOverride > maximumTimeout)
        {
            throw Violation(definition, request, "The requested tool timeout exceeds the agent policy.");
        }

        if (requested.Permissions.Any(permission =>
                !request.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)))
        {
            throw Violation(definition, request, "Tool permissions cannot be added by invocation options.");
        }

        if (policy.DefaultFailureMode == AIToolFailureMode.FailClosed
            && requested.FailureMode == AIToolFailureMode.ContinueWithoutTool)
        {
            throw Violation(definition, request, "Tool failure mode cannot be weakened.");
        }

        if (policy.RequireIdempotencyKeyForWrites
            && requested.MaximumAllowedSideEffectLevel > AIToolSideEffectLevel.ReadOnly
            && string.IsNullOrWhiteSpace(requested.IdempotencyKey))
        {
            throw Violation(definition, request, "An idempotency key is required for write-capable tool invocation.");
        }
    }

    private static void ValidateTimeout(
        AIAgentDefinition definition,
        AIAgentExecutionRequest request,
        AIAgentFrameworkOptions options)
    {
        if (request.TimeoutOverride > options.MaximumExecutionTimeout
            || definition.MaximumExecutionDuration is { } agentMaximum
                && request.TimeoutOverride > agentMaximum)
        {
            throw Violation(definition, request, "The requested execution timeout exceeds the agent policy.");
        }
    }

    private static void ValidateContextSize(AIAgentDefinition definition, AIAgentExecutionRequest request)
    {
        if (definition.Policy.MaximumContextCharacters is { } maximum
            && request.UserMessage.Length + request.PromptVariables.Sum(variable => variable.Value.Length) > maximum)
        {
            throw Violation(definition, request, "The requested context exceeds the agent policy.");
        }
    }

    private static int CalculatePromptCharacterCount(
        AIAgentPromptPolicy policy,
        AIAgentExecutionRequest request,
        IReadOnlyDictionary<string, string> variables) =>
        request.UserMessage.Length
        + (policy.StaticSystemInstruction?.Length ?? 0)
        + variables.Sum(variable => variable.Value.Length);

    private static bool Exceeds(int? requested, int? maximum) =>
        maximum is not null && (requested is null || requested > maximum);

    private static AIAgentPolicyViolationException Violation(
        AIAgentDefinition definition,
        AIAgentExecutionRequest request,
        string message) =>
        new(definition.Id, definition.Version, message, request.CorrelationId);
}
