using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Application;
using TradeMind.AI.ExpertAgents.Dispatch.Domain;
using TradeMind.AI.ExpertAgents.Domain;

namespace TradeMind.AI.ExpertAgents.Dispatch.Application;

public interface IAgentDispatchPlanner
{
    Task<AgentDispatchPlan> PlanAsync(
        MarketContext context,
        AgentDispatchRequest request,
        CancellationToken cancellationToken);
}

public sealed class AgentDispatchPlanner(
    IExpertAgentRegistry registry,
    IExpertAgentAuthorizationPolicy authorizationPolicy,
    IAgentCompatibilityPolicy compatibilityPolicy,
    IAnalysisIntentClassifier intentClassifier,
    IAgentRelevancePolicy relevancePolicy,
    IAgentCostPolicy costPolicy,
    IAgentDispatchBudgetPolicy budgetPolicy,
    IAgentDispatchVersionPolicy versionPolicy,
    IAgentDispatchFallbackPolicy fallbackPolicy,
    IOptions<AgentDispatchOptions> options,
    TimeProvider timeProvider,
    ILogger<AgentDispatchPlanner> logger) : IAgentDispatchPlanner
{
    private readonly AgentDispatchOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public Task<AgentDispatchPlan> PlanAsync(
        MarketContext context,
        AgentDispatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequest(context, request);

        var classification = intentClassifier.Classify(request, cancellationToken);
        var fallback = fallbackPolicy.Evaluate(request, classification);
        var rejections = new List<AgentDispatchRejection>();
        var warnings = new List<string>();
        if (fallback.Message is not null)
        {
            warnings.Add(fallback.Message);
        }

        if (!fallback.CanProceed)
        {
            rejections.Add(new AgentDispatchRejection(
                classification.IsAmbiguous ? DispatchRejectionCode.AmbiguousIntent : DispatchRejectionCode.UnknownIntent,
                fallback.Message ?? "The dispatch intent cannot be resolved."));
            return Task.FromResult(CreatePlan(
                request,
                context,
                fallback.Classification,
                [],
                rejections,
                warnings,
                isExecutable: false));
        }

        var explicitIds = request.IncludedAgentIds
            .Concat(request.Requirements.Keys)
            .Distinct()
            .ToHashSet();
        var candidateIds = registry.GetAvailable()
            .Where(descriptor => request.Specialty is null || descriptor.Specialty == request.Specialty || explicitIds.Contains(descriptor.Id))
            .Select(descriptor => descriptor.Id)
            .Concat(explicitIds)
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
        var candidates = new List<AgentDispatchCandidate>();

        foreach (var agentId in candidateIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var explicitRequest = explicitIds.Contains(agentId);
            var requirement = request.RequirementFor(agentId);
            if (request.ExcludedAgentIds.Contains(agentId))
            {
                rejections.Add(new AgentDispatchRejection(
                    DispatchRejectionCode.ExplicitlyExcluded,
                    "The agent was explicitly excluded from this dispatch.",
                    agentId,
                    requirement: requirement,
                    explicitRequest: explicitRequest));
                continue;
            }

            var selection = request.ExactVersions.TryGetValue(agentId, out var exactVersion)
                ? AgentVersionSelection.Exact
                : request.VersionSelection;
            var resolution = versionPolicy.Resolve(
                registry,
                agentId,
                selection,
                exactVersion,
                request.AllowExperimentalAgents || _options.AllowExperimentalAgents);
            if (!resolution.IsResolved || resolution.Agent is null)
            {
                rejections.Add(new AgentDispatchRejection(
                    resolution.RejectionCode ?? DispatchRejectionCode.AgentNotFound,
                    resolution.Message ?? "The agent could not be resolved.",
                    agentId,
                    exactVersion,
                    requirement,
                    explicitRequest));
                continue;
            }

            var descriptor = resolution.Agent.Descriptor;
            AgentExecutionRequest executionRequest;
            try
            {
                executionRequest = CreateExecutionRequest(context, request, descriptor);
            }
            catch (ArgumentException exception)
            {
                rejections.Add(new AgentDispatchRejection(
                    DispatchRejectionCode.InvalidRequest,
                    exception.Message,
                    descriptor.Id,
                    descriptor.Version,
                    requirement,
                    explicitRequest));
                continue;
            }

            var authorization = authorizationPolicy.Evaluate(descriptor, executionRequest);
            if (!authorization.Allowed)
            {
                rejections.Add(new AgentDispatchRejection(
                    authorization.ReasonCode switch
                    {
                        AgentErrorCode.AgentDisabled => DispatchRejectionCode.Disabled,
                        AgentErrorCode.UnauthorizedAgent => DispatchRejectionCode.Unauthorized,
                        _ => DispatchRejectionCode.Unauthorized
                    },
                    authorization.Message,
                    descriptor.Id,
                    descriptor.Version,
                    requirement,
                    explicitRequest));
                continue;
            }

            var compatibility = compatibilityPolicy.Evaluate(descriptor, context, executionRequest);
            if (!compatibility.IsCompatible)
            {
                var blockingIssues = compatibility.Issues
                    .Where(issue => issue.Severity == AgentCompatibilityIssueSeverity.Blocking)
                    .ToArray();
                foreach (var issue in blockingIssues.Length == 0
                    ? [new AgentCompatibilityIssue(AgentErrorCode.IncompatibleContext, "The market context is incompatible with the agent.", AgentCompatibilityIssueSeverity.Blocking)]
                    : blockingIssues)
                {
                    var rejectionCode = issue.Code switch
                    {
                        AgentErrorCode.StaleContext => DispatchRejectionCode.ContextStale,
                        AgentErrorCode.InsufficientContextQuality => DispatchRejectionCode.ContextQualityInsufficient,
                        _ => DispatchRejectionCode.Incompatible
                    };
                    rejections.Add(new AgentDispatchRejection(
                        rejectionCode,
                        issue.Message,
                        descriptor.Id,
                        descriptor.Version,
                        requirement,
                        explicitRequest));
                }
                continue;
            }

            var relevance = relevancePolicy.Evaluate(descriptor, request, fallback.Classification, context);
            var cost = costPolicy.Estimate(descriptor, request);
            candidates.Add(new AgentDispatchCandidate(
                descriptor,
                requirement,
                relevance,
                cost,
                executionRequest,
                explicitRequest));
        }

        var budget = budgetPolicy.Select(candidates, request);
        rejections.AddRange(budget.Rejections);
        var accepted = budget.Accepted
            .OrderBy(candidate => RequirementOrder(candidate.Requirement))
            .ThenByDescending(candidate => candidate.Relevance.Score)
            .ThenBy(candidate => candidate.Descriptor.Id.Value, StringComparer.Ordinal)
            .ThenByDescending(candidate => candidate.Descriptor.Version)
            .ToArray();
        if (accepted.Length == 0 && !rejections.Any(rejection => rejection.Code == DispatchRejectionCode.NoEligibleAgent))
        {
            rejections.Add(new AgentDispatchRejection(
                DispatchRejectionCode.NoEligibleAgent,
                "No eligible agent remains after all dispatch policies."));
        }

        var executable = fallback.CanProceed
            && budget.MinimumAgentsReached
            && !budget.RequiredBudgetExceeded
            && accepted.Length <= request.MaximumAgents
            && accepted.Length <= request.Budget.MaximumAgents;
        var plan = CreatePlan(request, context, fallback.Classification, accepted, rejections, warnings, executable, budget.TotalCost);
        logger.LogInformation(
            "Agent dispatch plan created. DispatchId={DispatchId}, MarketContextId={MarketContextId}, Intent={Intent}, CandidateCount={CandidateCount}, RejectionCount={RejectionCount}, IsExecutable={IsExecutable}, TotalCost={TotalCost}",
            request.DispatchId,
            context.Id,
            fallback.Classification.Intent.Key,
            plan.Candidates.Count,
            plan.Rejections.Count,
            plan.IsExecutable,
            plan.TotalCost.Units);
        return Task.FromResult(plan);
    }

    private void ValidateRequest(MarketContext context, AgentDispatchRequest request)
    {
        if (request.Version != AgentDispatchRequest.CurrentVersion)
        {
            throw new ArgumentException("The dispatch request version is not supported.", nameof(request));
        }

        if (request.MarketContextId != context.Id)
        {
            throw new ArgumentException("The dispatch request does not target the supplied market context.", nameof(request));
        }

        if (request.MaximumAgents > _options.MaximumAgents)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The requested maximum agent count exceeds the dispatcher limit.");
        }

        if (request.GlobalTimeout > _options.MaximumGlobalTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The requested global timeout exceeds the dispatcher limit.");
        }

        if (request.PerAgentTimeout > _options.MaximumPerAgentTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The requested per-agent timeout exceeds the dispatcher limit.");
        }
    }

    private AgentDispatchPlan CreatePlan(
        AgentDispatchRequest request,
        MarketContext context,
        AnalysisIntentClassificationResult classification,
        IReadOnlyCollection<AgentDispatchCandidate> candidates,
        IReadOnlyCollection<AgentDispatchRejection> rejections,
        IReadOnlyCollection<string> warnings,
        bool isExecutable,
        AgentCost? totalCost = null)
    {
        var groups = candidates
            .GroupBy(candidate => candidate.Requirement)
            .OrderBy(group => RequirementOrder(group.Key))
            .Select((group, index) => new AgentDispatchGroup(index, group.Key, group
                .OrderByDescending(candidate => candidate.Relevance.Score)
                .ThenBy(candidate => candidate.Descriptor.Id.Value, StringComparer.Ordinal)
                .ThenByDescending(candidate => candidate.Descriptor.Version)
                .ToArray()))
            .ToArray();
        return new AgentDispatchPlan(
            request.DispatchId,
            context.Id,
            classification,
            candidates,
            rejections,
            groups,
            totalCost ?? new AgentCost(0, TimeSpan.FromTicks(1)),
            isExecutable,
            timeProvider.GetUtcNow(),
            warnings);
    }

    private static AgentExecutionRequest CreateExecutionRequest(
        MarketContext context,
        AgentDispatchRequest request,
        AgentDescriptor descriptor) =>
        new(
            new AgentRunId($"dispatch-{request.DispatchId.Value:N}-{descriptor.Id.Value}"),
            descriptor.Id,
            context.Id,
            request.UserId,
            request.SessionId,
            request.Objective,
            request.AnalysisMode,
            request.AnalysisDepth,
            request.Language,
            request.Question,
            request.PerAgentTimeout,
            request.OutputOptions,
            request.DispatchId.ToString(),
            request.Metadata,
            tenantId: request.TenantId,
            permissions: request.Permissions,
            versionSelection: AgentVersionSelection.Exact,
            exactVersion: descriptor.Version);

    private static int RequirementOrder(AgentDispatchRequirement requirement) => requirement switch
    {
        AgentDispatchRequirement.Required => 0,
        AgentDispatchRequirement.Preferred => 1,
        AgentDispatchRequirement.Optional => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(requirement))
    };
}
