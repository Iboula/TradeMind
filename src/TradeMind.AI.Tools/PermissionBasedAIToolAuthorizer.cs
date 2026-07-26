using Microsoft.Extensions.Options;

namespace TradeMind.AI.Tools;

public sealed class PermissionBasedAIToolAuthorizer : IAIToolAuthorizer
{
    private readonly AIToolEngineOptions _options;

    public PermissionBasedAIToolAuthorizer(IOptions<AIToolEngineOptions> options)
    {
        _options = options.Value;
    }

    public void Authorize(AIToolDefinition definition, AIToolAuthorizationContext context)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        if (definition.Availability == AIToolAvailability.Disabled)
        {
            throw new AIToolUnavailableException(
                definition.Id,
                "Tool is disabled.",
                context.CorrelationId);
        }

        if (definition.Availability == AIToolAvailability.DevelopmentOnly && !_options.EnableDevelopmentTools)
        {
            throw new AIToolUnavailableException(
                definition.Id,
                "Development-only tool is unavailable in this environment.",
                context.CorrelationId);
        }

        var maximumSideEffect = (AIToolSideEffectLevel)Math.Min(
            (int)_options.MaximumAllowedSideEffectLevel,
            (int)context.MaximumAllowedSideEffectLevel);
        if (definition.SideEffectLevel > maximumSideEffect)
        {
            throw new AIToolAuthorizationException(
                definition.Id,
                "Tool side effects exceed the authorized level.",
                context.CorrelationId);
        }

        if (definition.RequiredPermissions.Any(required =>
                !context.Permissions.Contains(required, StringComparer.OrdinalIgnoreCase)))
        {
            throw new AIToolAuthorizationException(
                definition.Id,
                "Required tool permission is missing.",
                context.CorrelationId);
        }

        EnsureAllowed(definition, definition.AllowedTenantIds, context.TenantId, "tenant", StringComparer.Ordinal, context.CorrelationId);
        EnsureAllowed(definition, definition.AllowedUserIds, context.UserId, "user", StringComparer.Ordinal, context.CorrelationId);
        EnsureAllowed(definition, definition.AllowedAgentIds, context.AgentId, "agent", StringComparer.Ordinal, context.CorrelationId);
        EnsureAllowed(definition, definition.AllowedScenarios, context.Scenario, "scenario", StringComparer.OrdinalIgnoreCase, context.CorrelationId);
    }

    private static void EnsureAllowed(
        AIToolDefinition definition,
        IReadOnlyCollection<string> allowedValues,
        string? actualValue,
        string restriction,
        StringComparer comparer,
        string? correlationId)
    {
        if (allowedValues.Count > 0
            && (actualValue is null || !allowedValues.Contains(actualValue, comparer)))
        {
            throw new AIToolAuthorizationException(
                definition.Id,
                $"Tool is not authorized for the current {restriction}.",
                correlationId);
        }
    }
}
