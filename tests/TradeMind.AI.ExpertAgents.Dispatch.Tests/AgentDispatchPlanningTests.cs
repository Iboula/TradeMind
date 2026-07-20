using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.ExpertAgents.Application;
using TradeMind.AI.ExpertAgents.Dispatch.Application;
using TradeMind.AI.ExpertAgents.Dispatch.Domain;
using TradeMind.AI.ExpertAgents.Domain;

namespace TradeMind.AI.ExpertAgents.Dispatch.Tests;

public sealed class AgentDispatchPlanningTests
{
    [Fact]
    public void Request_copies_collections_and_rejects_invalid_invariants()
    {
        var context = DispatcherTestData.Context();
        var included = new List<AgentId> { new("market-agent") };
        var request = DispatcherTestData.Request(context, included: included);

        included.Clear();

        Assert.Single(request.IncludedAgentIds);
        Assert.Throws<ArgumentException>(() => DispatcherTestData.Request(
            context,
            included: [new AgentId("market-agent")],
            excluded: [new AgentId("market-agent")]));
        Assert.Throws<ArgumentException>(() => DispatcherTestData.Request(context, minimumAgents: 2, maximumAgents: 1));
    }

    [Fact]
    public void Classifier_supports_explicit_rules_ambiguity_and_unknown_fallback()
    {
        var context = DispatcherTestData.Context();
        var classifier = new RuleBasedAnalysisIntentClassifier();
        var explicitResult = classifier.Classify(DispatcherTestData.Request(
            context,
            explicitIntent: new AnalysisIntent("custom-request")));
        var ruleResult = classifier.Classify(DispatcherTestData.Request(context, objective: "Explain the market structure and trend."));
        var ambiguousResult = classifier.Classify(DispatcherTestData.Request(context, objective: "Assess the risk and explain it."));
        var unknownResult = classifier.Classify(DispatcherTestData.Request(context, objective: "Tell me something unusual."));
        var fallbackResult = classifier.Classify(DispatcherTestData.Request(
            context,
            objective: "Tell me something unusual.",
            fallback: AgentDispatchFallbackMode.UnknownIntent));

        Assert.Equal("custom-request", explicitResult.Intent.Key);
        Assert.Equal("technical-analysis", ruleResult.Intent.Key);
        Assert.True(ambiguousResult.IsAmbiguous);
        Assert.True(ambiguousResult.NeedsClarification);
        Assert.True(unknownResult.NeedsClarification);
        Assert.True(fallbackResult.UsedFallback);
        Assert.False(fallbackResult.NeedsClarification);
    }

    [Fact]
    public async Task Planner_selects_latest_stable_version_and_deterministic_order()
    {
        var context = DispatcherTestData.Context();
        var agentA1 = new TestExpertAgent(DispatcherTestData.Descriptor("alpha-agent", "1.0.0", AgentSpecialty.MarketStructure));
        var agentA2 = new TestExpertAgent(DispatcherTestData.Descriptor("alpha-agent", "2.0.0", AgentSpecialty.MarketStructure));
        var agentAPreview = new TestExpertAgent(DispatcherTestData.Descriptor("alpha-agent", "3.0.0-beta.1", AgentSpecialty.MarketStructure, maturity: AgentMaturity.Preview));
        var agentB = new TestExpertAgent(DispatcherTestData.Descriptor("beta-agent", "1.0.0", AgentSpecialty.Custom));
        var planner = DispatcherTestData.Planner([agentB, agentAPreview, agentA1, agentA2]);

        var plan = await planner.PlanAsync(context, DispatcherTestData.Request(context, maximumAgents: 4), CancellationToken.None);

        Assert.True(plan.IsExecutable);
        Assert.Equal(["alpha-agent", "beta-agent"], plan.Candidates.Select(candidate => candidate.Descriptor.Id.Value));
        Assert.Equal("2.0.0", plan.Candidates[0].Descriptor.Version.ToString());
    }

    [Fact]
    public async Task Planner_applies_explicit_include_exclude_authorization_and_compatibility()
    {
        var context = DispatcherTestData.Context(quality: 40, freshness: ContextFreshness.Stale);
        var includedId = new AgentId("included-agent");
        var excludedId = new AgentId("excluded-agent");
        var deniedId = new AgentId("denied-agent");
        var lowQualityId = new AgentId("low-quality-agent");
        var staleId = new AgentId("stale-agent");
        var agents = new IExpertAgent[]
        {
            new TestExpertAgent(DispatcherTestData.Descriptor(includedId.Value)),
            new TestExpertAgent(DispatcherTestData.Descriptor(excludedId.Value)),
            new TestExpertAgent(DispatcherTestData.Descriptor(deniedId.Value, requiredPermissions: ["admin"])),
            new TestExpertAgent(DispatcherTestData.Descriptor(lowQualityId.Value, minimumContextQuality: 80)),
            new TestExpertAgent(DispatcherTestData.Descriptor(staleId.Value, requiredContextCategories: [ContextProviderCategory.MarketSnapshot]))
        };
        var planner = DispatcherTestData.Planner(agents);
        var request = DispatcherTestData.Request(
            context,
            included: [includedId],
            excluded: [excludedId],
            minimumAgents: 1,
            objective: "Analyze current market overview.");

        var plan = await planner.PlanAsync(context, request, CancellationToken.None);

        Assert.Contains(plan.Candidates, candidate => candidate.Descriptor.Id == includedId);
        Assert.Contains(plan.Rejections, rejection => rejection.Code == DispatchRejectionCode.ExplicitlyExcluded);
        Assert.Contains(plan.Rejections, rejection => rejection.AgentId == deniedId && rejection.Code == DispatchRejectionCode.Unauthorized);
        Assert.Contains(plan.Rejections, rejection => rejection.AgentId == lowQualityId && rejection.Code == DispatchRejectionCode.ContextQualityInsufficient);
        Assert.Contains(plan.Rejections, rejection => rejection.AgentId == staleId && rejection.Code == DispatchRejectionCode.ContextStale);
    }

    [Fact]
    public async Task Planner_excludes_experimental_versions_and_honors_exact_version()
    {
        var context = DispatcherTestData.Context();
        var stable = new TestExpertAgent(DispatcherTestData.Descriptor("versioned-agent", "1.0.0", AgentSpecialty.Custom));
        var experimental = new TestExpertAgent(DispatcherTestData.Descriptor("experimental-agent", "2.0.0-alpha.1", AgentSpecialty.Custom, maturity: AgentMaturity.Experimental));
        var planner = DispatcherTestData.Planner(
            [experimental, stable],
            authorization: new DefaultExpertAgentAuthorizationPolicy(
                Options.Create(new ExpertAgentOptions { AllowExperimentalAgents = true })));

        var defaultPlan = await planner.PlanAsync(context, DispatcherTestData.Request(context), CancellationToken.None);
        var exactRequest = DispatcherTestData.Request(
            context,
            included: [experimental.Descriptor.Id],
            exactVersions: new Dictionary<AgentId, AgentVersion> { [experimental.Descriptor.Id] = experimental.Descriptor.Version },
            selection: AgentVersionSelection.LatestStable,
            allowExperimental: true);
        var exactPlan = await planner.PlanAsync(context, exactRequest, CancellationToken.None);

        Assert.DoesNotContain(defaultPlan.Candidates, candidate => candidate.Descriptor.Id == experimental.Descriptor.Id);
        Assert.Contains(exactPlan.Candidates, candidate => candidate.Descriptor.Version == experimental.Descriptor.Version);
    }

    [Fact]
    public async Task Planner_scores_relevance_and_applies_budget_to_optional_agents()
    {
        var context = DispatcherTestData.Context();
        var technical = new TestExpertAgent(DispatcherTestData.Descriptor("technical-agent", "1.0.0", AgentSpecialty.MarketStructure));
        var optional = new TestExpertAgent(DispatcherTestData.Descriptor("optional-agent", "1.0.0", AgentSpecialty.Custom));
        var planner = DispatcherTestData.Planner([optional, technical]);
        var request = DispatcherTestData.Request(
            context,
            budget: new AgentDispatchBudget(1, 2),
            minimumAgents: 1,
            maximumAgents: 2);

        var plan = await planner.PlanAsync(context, request, CancellationToken.None);

        Assert.Single(plan.Candidates);
        Assert.Equal("technical-agent", plan.Candidates[0].Descriptor.Id.Value);
        Assert.True(plan.Candidates[0].Relevance.Factors.Count > 0);
        Assert.Contains(plan.Rejections, rejection => rejection.Code == DispatchRejectionCode.BudgetExceeded);
    }

    [Fact]
    public async Task Planner_retains_required_agents_but_marks_required_budget_overrun_non_executable()
    {
        var context = DispatcherTestData.Context();
        var requiredId = new AgentId("required-agent");
        var planner = DispatcherTestData.Planner([new TestExpertAgent(DispatcherTestData.Descriptor(requiredId.Value))]);
        var request = DispatcherTestData.Request(
            context,
            included: [requiredId],
            budget: new AgentDispatchBudget(0, 1));

        var plan = await planner.PlanAsync(context, request, CancellationToken.None);

        Assert.Single(plan.Candidates);
        Assert.False(plan.IsExecutable);
        Assert.Contains(plan.Rejections, rejection => rejection.Code == DispatchRejectionCode.RequiredBudgetExceeded);
    }

    [Fact]
    public async Task Planner_returns_rejection_for_unknown_intent_without_fallback()
    {
        var context = DispatcherTestData.Context();
        var planner = DispatcherTestData.Planner([new TestExpertAgent(DispatcherTestData.Descriptor())]);

        var plan = await planner.PlanAsync(context, DispatcherTestData.Request(context, objective: "No known intent phrase."), CancellationToken.None);

        Assert.False(plan.IsExecutable);
        Assert.Empty(plan.Candidates);
        Assert.Contains(plan.Rejections, rejection => rejection.Code == DispatchRejectionCode.UnknownIntent);
    }

    [Fact]
    public async Task Planner_rejects_stale_context_and_quality_without_bypassing_policies()
    {
        var context = DispatcherTestData.Context(quality: 20, builtAtUtc: DispatcherTestData.Now.AddHours(-3), freshness: ContextFreshness.Stale);
        var planner = DispatcherTestData.Planner([new TestExpertAgent(DispatcherTestData.Descriptor(
            minimumContextQuality: 80,
            requiredContextCategories: [ContextProviderCategory.MarketSnapshot]))]);

        var plan = await planner.PlanAsync(context, DispatcherTestData.Request(context), CancellationToken.None);

        Assert.Empty(plan.Candidates);
        Assert.Contains(plan.Rejections, rejection => rejection.Code == DispatchRejectionCode.ContextQualityInsufficient);
        Assert.Contains(plan.Rejections, rejection => rejection.Code == DispatchRejectionCode.ContextStale);
    }

    [Fact]
    public async Task Planner_rejects_disabled_agents_and_applies_maximum_and_minimum_limits()
    {
        var context = DispatcherTestData.Context();
        var disabledId = new AgentId("disabled-agent");
        var first = new TestExpertAgent(DispatcherTestData.Descriptor("first-agent"));
        var second = new TestExpertAgent(DispatcherTestData.Descriptor("second-agent"));
        var disabled = new TestExpertAgent(DispatcherTestData.Descriptor(
            disabledId.Value,
            activationStatus: AgentActivationStatus.Disabled));
        var planner = DispatcherTestData.Planner([second, disabled, first]);

        var limited = await planner.PlanAsync(
            context,
            DispatcherTestData.Request(context, maximumAgents: 1),
            CancellationToken.None);
        var disabledPlan = await planner.PlanAsync(
            context,
            DispatcherTestData.Request(context, included: [disabledId]),
            CancellationToken.None);
        var lowQualityContext = DispatcherTestData.Context(quality: 10);
        var qualityPlanner = DispatcherTestData.Planner([new TestExpertAgent(DispatcherTestData.Descriptor(minimumContextQuality: 80))]);
        var noEligible = await qualityPlanner.PlanAsync(
            lowQualityContext,
            DispatcherTestData.Request(
                lowQualityContext,
                minimumAgents: 2,
                maximumAgents: 2,
                objective: "Analyze current market structure."),
            CancellationToken.None);

        Assert.Single(limited.Candidates);
        Assert.Contains(limited.Rejections, rejection => rejection.Code == DispatchRejectionCode.MaximumAgentsExceeded);
        Assert.Contains(disabledPlan.Rejections, rejection => rejection.Code == DispatchRejectionCode.Disabled);
        Assert.Contains(noEligible.Rejections, rejection => rejection.Code == DispatchRejectionCode.MinimumAgentsNotReached);
    }

    [Fact]
    public async Task Planner_uses_agent_id_as_deterministic_tie_breaker()
    {
        var context = DispatcherTestData.Context();
        var zebra = new TestExpertAgent(DispatcherTestData.Descriptor("zebra-agent"));
        var alpha = new TestExpertAgent(DispatcherTestData.Descriptor("alpha-agent"));
        var planner = DispatcherTestData.Planner([zebra, alpha]);

        var plan = await planner.PlanAsync(
            context,
            DispatcherTestData.Request(context, maximumAgents: 2),
            CancellationToken.None);

        Assert.Equal(["alpha-agent", "zebra-agent"], plan.Candidates.Select(candidate => candidate.Descriptor.Id.Value));
    }

    [Fact]
    public async Task Dispatch_plan_copies_candidates_and_rejects_duplicate_versions()
    {
        var context = DispatcherTestData.Context();
        var planner = DispatcherTestData.Planner([new TestExpertAgent(DispatcherTestData.Descriptor())]);
        var original = await planner.PlanAsync(context, DispatcherTestData.Request(context), CancellationToken.None);
        var candidates = original.Candidates.ToList();
        var copy = new AgentDispatchPlan(
            original.DispatchId,
            original.MarketContextId,
            original.Classification,
            candidates,
            original.Rejections,
            original.ExecutionGroups,
            original.TotalCost,
            original.IsExecutable,
            original.CreatedAtUtc,
            original.Warnings);
        candidates.Clear();

        Assert.Single(copy.Candidates);
        Assert.Throws<ArgumentException>(() => new AgentDispatchPlan(
            original.DispatchId,
            original.MarketContextId,
            original.Classification,
            [original.Candidates[0], original.Candidates[0]],
            original.Rejections,
            original.ExecutionGroups,
            original.TotalCost,
            original.IsExecutable,
            original.CreatedAtUtc));
    }

    [Fact]
    public void Dispatcher_di_registers_valid_lifetimes_and_rejects_invalid_options()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTradeMindExpertAgentDispatcher(options => options.MaximumParallelism = 2);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        using (var scope = provider.CreateScope())
        {
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IExpertDispatcher>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAgentDispatchPlanner>());
        }

        Assert.Throws<OptionsValidationException>(() =>
        {
            var invalidServices = new ServiceCollection();
            invalidServices.AddLogging();
            invalidServices.AddTradeMindExpertAgentDispatcher(options => options.MaximumParallelism = 0);
            using var invalidProvider = invalidServices.BuildServiceProvider();
            _ = invalidProvider.GetRequiredService<IOptions<AgentDispatchOptions>>().Value;
        });
    }
}
