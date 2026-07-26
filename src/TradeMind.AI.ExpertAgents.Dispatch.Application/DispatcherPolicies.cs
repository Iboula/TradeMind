using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Application;
using TradeMind.AI.ExpertAgents.Dispatch.Domain;
using TradeMind.AI.ExpertAgents.Domain;

namespace TradeMind.AI.ExpertAgents.Dispatch.Application;

public interface IAgentRelevancePolicy
{
    AgentRelevanceScore Evaluate(
        AgentDescriptor descriptor,
        AgentDispatchRequest request,
        AnalysisIntentClassificationResult classification,
        MarketContext context);
}

public sealed class DefaultAgentRelevancePolicy : IAgentRelevancePolicy
{
    public AgentRelevanceScore Evaluate(
        AgentDescriptor descriptor,
        AgentDispatchRequest request,
        AnalysisIntentClassificationResult classification,
        MarketContext context)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentNullException.ThrowIfNull(context);

        var factors = new List<AgentRelevanceFactor>();
        var score = 0;
        if (MatchesSpecialty(descriptor.Specialty, classification.Intent))
        {
            score += 60;
            factors.Add(new AgentRelevanceFactor("specialty", 60, $"Specialty '{descriptor.Specialty}' matches intent '{classification.Intent.Key}'."));
        }

        var tagMatches = descriptor.Tags
            .Where(tag => tag.Contains(classification.Intent.Key, StringComparison.OrdinalIgnoreCase)
                || classification.Intent.Keywords.Any(keyword => tag.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (tagMatches.Length > 0)
        {
            var points = Math.Min(20, tagMatches.Length * 5);
            score += points;
            factors.Add(new AgentRelevanceFactor("tags", points, $"Tags matched: {string.Join(", ", tagMatches)}."));
        }

        if (descriptor.Capabilities.Supports(request.AnalysisMode))
        {
            score += 10;
            factors.Add(new AgentRelevanceFactor("analysis-mode", 10, "The agent supports the requested analysis mode."));
        }

        if (descriptor.Capabilities.RequiredContextCategories.All(category => HasContext(context, category)))
        {
            score += 10;
            factors.Add(new AgentRelevanceFactor("context", 10, "All required context categories are present."));
        }

        if (request.IncludedAgentIds.Contains(descriptor.Id))
        {
            score += 10;
            factors.Add(new AgentRelevanceFactor("explicit-inclusion", 10, "The caller explicitly included this agent."));
        }

        return new AgentRelevanceScore(Math.Min(100, score), factors);
    }

    private static bool MatchesSpecialty(AgentSpecialty specialty, AnalysisIntent intent) =>
        intent.Kind switch
        {
            AnalysisIntentKind.TechnicalAnalysis => specialty.Value is "ict" or "smc" or "wyckoff" or "market-structure" or "volume-profile" or "order-flow",
            AnalysisIntentKind.RiskAssessment => specialty == AgentSpecialty.Risk,
            AnalysisIntentKind.MacroAnalysis => specialty == AgentSpecialty.Macro,
            AnalysisIntentKind.EducationalExplanation => specialty is { Value: "psychology" or "custom" },
            _ => specialty == AgentSpecialty.Custom
        };

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
}

public interface IAgentCostPolicy
{
    AgentCost Estimate(AgentDescriptor descriptor, AgentDispatchRequest request);
}

public sealed class DefaultAgentCostPolicy(IOptions<AgentDispatchOptions> options) : IAgentCostPolicy
{
    private readonly AgentDispatchOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public AgentCost Estimate(AgentDescriptor descriptor, AgentDispatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(request);
        var duration = descriptor.Capabilities.RecommendedTimeout
            ?? request.PerAgentTimeout
            ?? _options.DefaultEstimatedAgentDuration;
        return new AgentCost(_options.DefaultAgentCostUnits, duration);
    }
}

public sealed record AgentBudgetEvaluation(
    IReadOnlyList<AgentDispatchCandidate> Accepted,
    IReadOnlyList<AgentDispatchRejection> Rejections,
    AgentCost TotalCost,
    bool RequiredBudgetExceeded,
    bool MaximumAgentsExceeded,
    bool MinimumAgentsReached);

public interface IAgentDispatchBudgetPolicy
{
    AgentBudgetEvaluation Select(
        IReadOnlyCollection<AgentDispatchCandidate> candidates,
        AgentDispatchRequest request);
}

public sealed class DefaultAgentDispatchBudgetPolicy : IAgentDispatchBudgetPolicy
{
    public AgentBudgetEvaluation Select(
        IReadOnlyCollection<AgentDispatchCandidate> candidates,
        AgentDispatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(request);

        var ordered = candidates
            .OrderBy(candidate => RequirementOrder(candidate.Requirement))
            .ThenByDescending(candidate => candidate.Relevance.Score)
            .ThenBy(candidate => candidate.Descriptor.Id.Value, StringComparer.Ordinal)
            .ThenByDescending(candidate => candidate.Descriptor.Version)
            .ToArray();
        var accepted = new List<AgentDispatchCandidate>();
        var rejections = new List<AgentDispatchRejection>();
        var total = 0m;
        var requiredOverBudget = false;
        var maximumExceeded = false;

        foreach (var candidate in ordered)
        {
            var isRequired = candidate.Requirement == AgentDispatchRequirement.Required;
            if (isRequired)
            {
                accepted.Add(candidate);
                total += candidate.Cost.Units;
                if (total > request.Budget.MaximumUnits)
                {
                    requiredOverBudget = true;
                    rejections.Add(new AgentDispatchRejection(
                        DispatchRejectionCode.RequiredBudgetExceeded,
                        "Required agents exceed the dispatch budget; the plan is not executable.",
                        candidate.Descriptor.Id,
                        candidate.Descriptor.Version,
                        candidate.Requirement,
                        candidate.ExplicitlyIncluded));
                }

                continue;
            }

            if (accepted.Count >= Math.Min(request.MaximumAgents, request.Budget.MaximumAgents))
            {
                maximumExceeded = true;
                rejections.Add(new AgentDispatchRejection(
                    DispatchRejectionCode.MaximumAgentsExceeded,
                    "The candidate was removed because the maximum agent count was reached.",
                    candidate.Descriptor.Id,
                    candidate.Descriptor.Version,
                    candidate.Requirement,
                    candidate.ExplicitlyIncluded));
                continue;
            }

            if (requiredOverBudget || total + candidate.Cost.Units > request.Budget.MaximumUnits)
            {
                rejections.Add(new AgentDispatchRejection(
                    DispatchRejectionCode.BudgetExceeded,
                    "The candidate was removed because adding it would exceed the dispatch budget.",
                    candidate.Descriptor.Id,
                    candidate.Descriptor.Version,
                    candidate.Requirement,
                    candidate.ExplicitlyIncluded));
                continue;
            }

            accepted.Add(candidate);
            total += candidate.Cost.Units;
        }

        var minimumReached = accepted.Count >= request.MinimumAgents;
        if (!minimumReached)
        {
            rejections.Add(new AgentDispatchRejection(
                DispatchRejectionCode.MinimumAgentsNotReached,
                "The selected agents do not satisfy the configured minimum.",
                requirement: AgentDispatchRequirement.Required));
        }

        if (accepted.Count == 0)
        {
            rejections.Add(new AgentDispatchRejection(
                DispatchRejectionCode.NoEligibleAgent,
                "No eligible agent remains after authorization, compatibility and budget policies."));
        }

        return new AgentBudgetEvaluation(
            Array.AsReadOnly(accepted.ToArray()),
            Array.AsReadOnly(rejections.ToArray()),
            new AgentCost(total, accepted.Count == 0 ? TimeSpan.FromTicks(1) : accepted.Max(candidate => candidate.Cost.EstimatedDuration)),
            requiredOverBudget,
            maximumExceeded,
            minimumReached);
    }

    private static int RequirementOrder(AgentDispatchRequirement requirement) => requirement switch
    {
        AgentDispatchRequirement.Required => 0,
        AgentDispatchRequirement.Preferred => 1,
        AgentDispatchRequirement.Optional => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(requirement))
    };
}

public sealed record AgentDispatchVersionResolution(
    bool IsResolved,
    IExpertAgent? Agent,
    DispatchRejectionCode? RejectionCode,
    string? Message);

public interface IAgentDispatchVersionPolicy
{
    AgentDispatchVersionResolution Resolve(
        IExpertAgentRegistry registry,
        AgentId agentId,
        AgentVersionSelection selection,
        AgentVersion? exactVersion,
        bool allowExperimental);
}

public sealed class DefaultAgentDispatchVersionPolicy : IAgentDispatchVersionPolicy
{
    public AgentDispatchVersionResolution Resolve(
        IExpertAgentRegistry registry,
        AgentId agentId,
        AgentVersionSelection selection,
        AgentVersion? exactVersion,
        bool allowExperimental)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(agentId);
        if (!registry.TryResolve(agentId, selection, exactVersion, out var agent) || agent is null)
        {
            return new(false, null, selection == AgentVersionSelection.Exact ? DispatchRejectionCode.VersionNotFound : DispatchRejectionCode.AgentNotFound, "The requested agent version could not be resolved.");
        }

        if (agent.Descriptor.Maturity == AgentMaturity.Experimental && !allowExperimental)
        {
            return new(false, null, DispatchRejectionCode.ExperimentalExcluded, "Experimental agent versions are excluded by default.");
        }

        return new(true, agent, null, null);
    }
}

public sealed record AgentDispatchFallbackDecision(
    bool CanProceed,
    AnalysisIntentClassificationResult Classification,
    string? Message);

public interface IAgentDispatchFallbackPolicy
{
    AgentDispatchFallbackDecision Evaluate(
        AgentDispatchRequest request,
        AnalysisIntentClassificationResult classification);
}

public sealed class DefaultAgentDispatchFallbackPolicy : IAgentDispatchFallbackPolicy
{
    public AgentDispatchFallbackDecision Evaluate(
        AgentDispatchRequest request,
        AnalysisIntentClassificationResult classification)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(classification);
        if (!classification.NeedsClarification)
        {
            return new(true, classification, null);
        }

        return request.FallbackMode == AgentDispatchFallbackMode.None
            ? new(false, classification, "The dispatch requires clarification before selecting agents.")
            : new(true, classification, "The configured fallback allows dispatch with a low-confidence intent.");
    }
}
