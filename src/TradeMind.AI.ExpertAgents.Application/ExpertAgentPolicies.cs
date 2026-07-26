using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Domain;

namespace TradeMind.AI.ExpertAgents.Application;

public enum AgentCompatibilityIssueSeverity
{
    Warning,
    Blocking
}

public sealed record AgentCompatibilityIssue(
    AgentErrorCode Code,
    string Message,
    AgentCompatibilityIssueSeverity Severity);

public sealed record AgentCompatibilityResult
{
    private AgentCompatibilityResult(
        bool isCompatible,
        IReadOnlyCollection<AgentCompatibilityIssue> issues)
    {
        IsCompatible = isCompatible;
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public bool IsCompatible { get; }
    public bool HasWarnings => Issues.Any(issue => issue.Severity == AgentCompatibilityIssueSeverity.Warning);
    public IReadOnlyList<AgentCompatibilityIssue> Issues { get; }

    public static AgentCompatibilityResult Compatible() => new(true, []);

    public static AgentCompatibilityResult WithWarnings(IEnumerable<AgentCompatibilityIssue> issues) =>
        new(true, issues.ToArray());

    public static AgentCompatibilityResult Incompatible(IEnumerable<AgentCompatibilityIssue> issues) =>
        new(false, issues.ToArray());
}

public interface IAgentCompatibilityPolicy
{
    AgentCompatibilityResult Evaluate(
        AgentDescriptor descriptor,
        MarketContext context,
        AgentExecutionRequest request);
}

public sealed class DefaultAgentCompatibilityPolicy(TimeProvider timeProvider) : IAgentCompatibilityPolicy
{
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public AgentCompatibilityResult Evaluate(
        AgentDescriptor descriptor,
        MarketContext context,
        AgentExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<AgentCompatibilityIssue>();
        if (!descriptor.SupportedContextVersions.Contains(context.Version))
        {
            issues.Add(new(AgentErrorCode.UnsupportedContextVersion, "The market context version is not supported by the agent.", AgentCompatibilityIssueSeverity.Blocking));
        }

        if (context.Status == MarketContextBuildStatus.Failed)
        {
            issues.Add(new(AgentErrorCode.IncompatibleContext, "A failed market context cannot be analyzed.", AgentCompatibilityIssueSeverity.Blocking));
        }

        if (!descriptor.Capabilities.Supports(context.Instrument))
        {
            issues.Add(new(AgentErrorCode.UnsupportedInstrument, "The agent does not support the requested instrument.", AgentCompatibilityIssueSeverity.Blocking));
        }

        if (!descriptor.Capabilities.Supports(context.Timeframe))
        {
            issues.Add(new(AgentErrorCode.UnsupportedTimeframe, "The agent does not support the requested timeframe.", AgentCompatibilityIssueSeverity.Blocking));
        }

        if (!descriptor.Capabilities.Supports(request.AnalysisMode))
        {
            issues.Add(new(AgentErrorCode.UnsupportedAnalysisMode, "The requested analysis mode is not supported by the agent.", AgentCompatibilityIssueSeverity.Blocking));
        }

        if (request.Question is not null && !descriptor.Capabilities.SupportsUserQuestion)
        {
            issues.Add(new(AgentErrorCode.IncompatibleContext, "The agent does not accept a user question.", AgentCompatibilityIssueSeverity.Blocking));
        }

        foreach (var category in descriptor.Capabilities.RequiredContextCategories)
        {
            if (!HasContext(context, category))
            {
                issues.Add(new(AgentErrorCode.MissingRequiredContext, $"Required context category '{category}' is unavailable.", AgentCompatibilityIssueSeverity.Blocking));
            }
        }

        if (context.Quality.Score < descriptor.MinimumContextQuality)
        {
            issues.Add(new(AgentErrorCode.InsufficientContextQuality, "The context quality is below the agent minimum.", AgentCompatibilityIssueSeverity.Blocking));
        }

        if (!descriptor.AllowStaleContext)
        {
            var staleRequiredSources = context.Traces
                .Where(trace => descriptor.Capabilities.RequiredContextCategories.Contains(trace.Category))
                .Any(trace => trace.Freshness is ContextFreshness.Stale or ContextFreshness.Unknown);
            if (staleRequiredSources)
            {
                issues.Add(new(AgentErrorCode.StaleContext, "A required context source is stale or has unknown freshness.", AgentCompatibilityIssueSeverity.Blocking));
            }
        }

        if (descriptor.Capabilities.MaximumContextAge is { } maximumAge
            && _timeProvider.GetUtcNow() - context.BuiltAtUtc > maximumAge)
        {
            issues.Add(new(AgentErrorCode.StaleContext, "The market context is older than the agent allows.", AgentCompatibilityIssueSeverity.Blocking));
        }

        if (context.Status == MarketContextBuildStatus.PartiallySucceeded && issues.Count == 0)
        {
            issues.Add(new(AgentErrorCode.IncompatibleContext, "The context is usable but was built with reduced capabilities.", AgentCompatibilityIssueSeverity.Warning));
        }

        var estimatedCharacters = EstimateContextCharacters(context);
        if (descriptor.Capabilities.MaximumContextCharacters is { } maximumCharacters
            && estimatedCharacters > maximumCharacters)
        {
            issues.Add(new(AgentErrorCode.IncompatibleContext, "The market context exceeds the agent context size limit.", AgentCompatibilityIssueSeverity.Blocking));
        }

        return issues.Any(issue => issue.Severity == AgentCompatibilityIssueSeverity.Blocking)
            ? AgentCompatibilityResult.Incompatible(issues)
            : issues.Count == 0
                ? AgentCompatibilityResult.Compatible()
                : AgentCompatibilityResult.WithWarnings(issues);
    }

    private static bool HasContext(MarketContext context, ContextProviderCategory category) => category switch
    {
        ContextProviderCategory.MarketSnapshot => context.MarketSnapshot is not null,
        ContextProviderCategory.Knowledge => context.Knowledge is not null,
        ContextProviderCategory.Memory => context.Memory is not null,
        ContextProviderCategory.TraderProfile => context.TraderProfile is not null,
        ContextProviderCategory.Workspace => context.Workspace is not null,
        ContextProviderCategory.News => context.News is not null,
        ContextProviderCategory.EconomicCalendar => context.EconomicCalendar is not null,
        _ => false
    };

    private static int EstimateContextCharacters(MarketContext context) =>
        (context.Knowledge?.Chunks.Sum(chunk => chunk.Content.Length) ?? 0)
        + (context.Memory?.Summary?.Length ?? 0)
        + (context.Memory?.Items.Sum(item => item.Content.Length) ?? 0)
        + (context.News?.Items.Sum(item => item.Headline.Length) ?? 0)
        + (context.EconomicCalendar?.Events.Sum(item => item.Title.Length) ?? 0);
}

public sealed record AgentAuthorizationResult
{
    private AgentAuthorizationResult(bool allowed, AgentErrorCode? reasonCode, string message)
    {
        Allowed = allowed;
        ReasonCode = reasonCode;
        Message = message;
    }

    public bool Allowed { get; }
    public AgentErrorCode? ReasonCode { get; }
    public string Message { get; }

    public static AgentAuthorizationResult Allow() => new(true, null, "Agent execution authorized.");

    public static AgentAuthorizationResult Deny(AgentErrorCode reasonCode, string message) =>
        new(false, reasonCode, message);
}

public interface IExpertAgentAuthorizationPolicy
{
    AgentAuthorizationResult Evaluate(
        AgentDescriptor descriptor,
        AgentExecutionRequest request);
}

public sealed class DefaultExpertAgentAuthorizationPolicy(
    Microsoft.Extensions.Options.IOptions<ExpertAgentOptions> options) : IExpertAgentAuthorizationPolicy
{
    private readonly ExpertAgentOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public AgentAuthorizationResult Evaluate(AgentDescriptor descriptor, AgentExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(request);
        if (descriptor.ActivationStatus == AgentActivationStatus.Disabled)
        {
            return AgentAuthorizationResult.Deny(AgentErrorCode.AgentDisabled, "The requested agent is disabled.");
        }

        if (descriptor.Maturity == AgentMaturity.Experimental && !_options.AllowExperimentalAgents)
        {
            return AgentAuthorizationResult.Deny(AgentErrorCode.UnauthorizedAgent, "Experimental agents are not enabled for this environment.");
        }

        if (descriptor.Maturity == AgentMaturity.Preview && !_options.AllowPreviewAgents)
        {
            return AgentAuthorizationResult.Deny(AgentErrorCode.UnauthorizedAgent, "Preview agents are not enabled for this environment.");
        }

        if (descriptor.RequiredPermissions.Any(permission => !request.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)))
        {
            return AgentAuthorizationResult.Deny(AgentErrorCode.UnauthorizedAgent, "The caller lacks a required permission for this agent.");
        }

        if (descriptor.AllowedTenantIds.Count > 0
            && (request.TenantId is null || !descriptor.AllowedTenantIds.Contains(request.TenantId, StringComparer.Ordinal)))
        {
            return AgentAuthorizationResult.Deny(AgentErrorCode.UnauthorizedAgent, "The caller tenant is not authorized for this agent.");
        }

        if (descriptor.AllowedUserIds.Count > 0
            && !descriptor.AllowedUserIds.Contains(request.UserId, StringComparer.Ordinal))
        {
            return AgentAuthorizationResult.Deny(AgentErrorCode.UnauthorizedAgent, "The caller is not authorized for this agent.");
        }

        return AgentAuthorizationResult.Allow();
    }
}
