using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.ExpertAgents.Consensus.Tests;

public sealed class ConsensusEngineTests
{
    [Fact]
    public async Task Empty_request_returns_structured_no_eligible_result()
    {
        var context = ConsensusTestData.Context();
        var result = await ConsensusTestData.Engine().BuildAsync(ConsensusTestData.Request(context), CancellationToken.None);

        Assert.Equal(ConsensusStatus.NoEligibleAnalyses, result.Status);
        Assert.Equal(AgentDirectionalBias.InsufficientData, result.ConsolidatedBias);
        Assert.Equal(ConsensusLevel.None, result.Level);
        Assert.Contains("No analyses", result.Warnings.Single());
    }

    [Fact]
    public async Task Context_mismatch_and_duplicate_run_are_rejected_without_affecting_valid_result()
    {
        var context = ConsensusTestData.Context();
        var otherContext = ConsensusTestData.Context();
        var valid = ConsensusTestData.Result(context, "run-a", "agent-a", AgentDirectionalBias.Bullish);
        var duplicate = ConsensusTestData.Result(context, "run-a", "agent-b", AgentDirectionalBias.Bearish);
        var mismatch = ConsensusTestData.Result(otherContext, "run-c", "agent-c", AgentDirectionalBias.Bearish);

        var result = await ConsensusTestData.Engine().BuildAsync(
            ConsensusTestData.Request(context, [mismatch, duplicate, valid]), CancellationToken.None);

        Assert.Equal(ConsensusStatus.PartiallySucceeded, result.Status);
        Assert.Single(result.EligibleAnalyses);
        Assert.Contains(result.RejectedAnalyses, item => item.Code == ConsensusRejectionCode.DuplicateRun);
        Assert.Contains(result.RejectedAnalyses, item => item.Code == ConsensusRejectionCode.ContextMismatch);
    }

    [Fact]
    public async Task Failed_timed_out_and_unavailable_analyses_do_not_vote()
    {
        var context = ConsensusTestData.Context();
        var analyses = new[]
        {
            ConsensusTestData.Result(context, "run-failed", "agent-failed", AgentDirectionalBias.Bullish, AgentAnalysisStatus.Failed),
            ConsensusTestData.Result(context, "run-timeout", "agent-timeout", AgentDirectionalBias.Bullish, AgentAnalysisStatus.TimedOut),
            ConsensusTestData.Result(context, "run-unavailable", "agent-unavailable", AgentDirectionalBias.Bullish, AgentAnalysisStatus.Unavailable)
        };

        var result = await ConsensusTestData.Engine().BuildAsync(ConsensusTestData.Request(context, analyses), CancellationToken.None);

        Assert.Equal(ConsensusStatus.NoEligibleAnalyses, result.Status);
        Assert.Equal(3, result.RejectedAnalyses.Count);
        Assert.All(result.RejectedAnalyses, item => Assert.Equal(ConsensusRejectionCode.InvalidResultStatus, item.Code));
    }

    [Fact]
    public async Task Strong_bullish_majority_is_not_probability_and_preserves_bearish_minority()
    {
        var context = ConsensusTestData.Context();
        var analyses = Enumerable.Range(1, 4)
            .Select(index => ConsensusTestData.Result(context, $"run-b{index}", $"agent-b{index}", AgentDirectionalBias.Bullish))
            .Append(ConsensusTestData.Result(context, "run-bear", "agent-bear", AgentDirectionalBias.Bearish))
            .ToArray();

        var result = await ConsensusTestData.Engine().BuildAsync(ConsensusTestData.Request(context, analyses), CancellationToken.None);

        Assert.Equal(AgentDirectionalBias.Bullish, result.ConsolidatedBias);
        Assert.Equal(ConsensusLevel.Strong, result.Level);
        Assert.NotEmpty(result.MinorityOpinions);
        Assert.Contains(result.MinorityOpinions, item => item.Bias == AgentDirectionalBias.Bearish);
        Assert.False(result.Confidence.IsProbabilityOfProfit);
        Assert.Contains(result.Confidence.Limitations, item => item.Contains("probability of profit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Strong_bullish_and_bearish_shares_create_visible_critical_conflict()
    {
        var context = ConsensusTestData.Context();
        var analyses = new[]
        {
            ConsensusTestData.Result(context, "run-bull", "agent-bull", AgentDirectionalBias.Bullish),
            ConsensusTestData.Result(context, "run-bear", "agent-bear", AgentDirectionalBias.Bearish),
            ConsensusTestData.Result(context, "run-neutral", "agent-neutral", AgentDirectionalBias.Neutral)
        };

        var result = await ConsensusTestData.Engine().BuildAsync(ConsensusTestData.Request(context, analyses), CancellationToken.None);

        Assert.Equal(ConsensusLevel.Conflicted, result.Level);
        Assert.Contains(result.Conflicts, item => item.Kind == ConsensusConflictKind.Directional && item.Severity == ConsensusConflictSeverity.Critical);
        Assert.Equal(AgentDirectionalBias.Neutral, result.ConsolidatedBias);
        Assert.Contains(result.Confidence.Limitations, item => item.Contains("critical conflict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Neutral_is_not_opposite_direction_and_mixed_increases_uncertainty()
    {
        var context = ConsensusTestData.Context();
        var neutral = ConsensusTestData.Result(context, "run-neutral", "agent-neutral", AgentDirectionalBias.Neutral);
        var mixed = ConsensusTestData.Result(context, "run-mixed", "agent-mixed", AgentDirectionalBias.Mixed);

        var result = await ConsensusTestData.Engine().BuildAsync(ConsensusTestData.Request(context, [neutral, mixed]), CancellationToken.None);

        Assert.Equal(AgentDirectionalBias.Mixed, result.ConsolidatedBias);
        Assert.Equal(ConsensusLevel.Conflicted, result.Level);
        Assert.True(result.Confidence.Uncertainty > 0);
        Assert.Equal(0, result.Disagreement.Score);
    }

    [Fact]
    public async Task Insufficient_data_and_not_applicable_are_traceable_without_directional_vote()
    {
        var context = ConsensusTestData.Context();
        var analyses = new[]
        {
            ConsensusTestData.Result(context, "run-insufficient", "agent-insufficient", AgentDirectionalBias.InsufficientData),
            ConsensusTestData.Result(context, "run-na", "agent-na", AgentDirectionalBias.NotApplicable)
        };

        var result = await ConsensusTestData.Engine().BuildAsync(ConsensusTestData.Request(context, analyses), CancellationToken.None);

        Assert.Equal(ConsensusStatus.Succeeded, result.Status);
        Assert.Equal(AgentDirectionalBias.InsufficientData, result.ConsolidatedBias);
        Assert.Equal(2, result.EligibleAnalyses.Count);
        Assert.All(result.EligibleAnalyses, item => Assert.False(item.ContributesOpinion));
    }

    [Fact]
    public async Task Partially_succeeded_is_included_with_penalty_while_low_quality_is_rejected()
    {
        var context = ConsensusTestData.Context();
        var partial = ConsensusTestData.Result(context, "run-partial", "agent-partial", AgentDirectionalBias.Bullish, AgentAnalysisStatus.PartiallySucceeded);
        var lowConfidence = ConsensusTestData.Result(context, "run-low-confidence", "agent-low", AgentDirectionalBias.Bullish, confidence: 10);
        var stale = ConsensusTestData.Result(context, "run-stale", "agent-stale", AgentDirectionalBias.Bearish, freshness: 10);

        var result = await ConsensusTestData.Engine().BuildAsync(ConsensusTestData.Request(context, [partial, lowConfidence, stale]), CancellationToken.None);

        Assert.Equal(ConsensusStatus.PartiallySucceeded, result.Status);
        Assert.Single(result.EligibleAnalyses);
        Assert.Equal(AgentAnalysisStatus.PartiallySucceeded, result.EligibleAnalyses[0].Result.Status);
        Assert.Contains(result.EligibleAnalyses[0].Reasons, item => item.Contains("penalty", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.RejectedAnalyses, item => item.Code == ConsensusRejectionCode.InsufficientConfidence);
        Assert.Contains(result.RejectedAnalyses, item => item.Code == ConsensusRejectionCode.InsufficientFreshness);
    }

    [Fact]
    public async Task Contribution_cap_and_tie_break_are_explainable_and_deterministic()
    {
        var context = ConsensusTestData.Context();
        var options = new ConsensusOptions { MaximumAgentContributionShare = 0.4 };
        var analyses = new[]
        {
            ConsensusTestData.Result(context, "run-a", "same-agent", AgentDirectionalBias.Bullish),
            ConsensusTestData.Result(context, "run-b", "same-agent", AgentDirectionalBias.Bullish),
            ConsensusTestData.Result(context, "run-c", "other-agent", AgentDirectionalBias.Bearish),
            ConsensusTestData.Result(context, "run-d", "other-agent-2", AgentDirectionalBias.Bearish)
        };

        var first = await ConsensusTestData.Engine(options).BuildAsync(ConsensusTestData.Request(context, analyses), CancellationToken.None);
        var second = await ConsensusTestData.Engine(options).BuildAsync(ConsensusTestData.Request(context, analyses.Reverse().ToArray()), CancellationToken.None);

        Assert.Equal(first.ConsolidatedBias, second.ConsolidatedBias);
        Assert.Equal(first.Conclusion.Summary, second.Conclusion.Summary);
        Assert.Contains(first.Weights, item => item.Capped && item.Factors.Any(factor => factor.Contains("cap", StringComparison.OrdinalIgnoreCase)));
        Assert.All(first.Weights.GroupBy(item => item.AgentId), group => Assert.True(group.Sum(item => item.EffectiveShare) <= options.MaximumAgentContributionShare + 0.0000001));
        Assert.Equal(first.MinorityOpinions.Select(item => item.Bias), second.MinorityOpinions.Select(item => item.Bias));
    }

    [Fact]
    public async Task Compatible_levels_merge_incompatible_levels_remain_separate_and_sources_are_preserved()
    {
        var context = ConsensusTestData.Context();
        var firstLevel = ConsensusTestData.Level("support-a", AgentMarketLevelType.Support, 1.1000m);
        var secondLevel = ConsensusTestData.Level("support-b", AgentMarketLevelType.Support, 1.1003m);
        var incompatible = ConsensusTestData.Level("support-c", AgentMarketLevelType.Support, 1.1200m);
        var resistance = ConsensusTestData.Level("resistance", AgentMarketLevelType.Resistance, 1.2000m);
        var analyses = new[]
        {
            ConsensusTestData.Result(context, "run-a", "agent-a", AgentDirectionalBias.Bullish, levels: [firstLevel, incompatible]),
            ConsensusTestData.Result(context, "run-b", "agent-b", AgentDirectionalBias.Bullish, levels: [secondLevel, resistance])
        };

        var result = await ConsensusTestData.Engine(new ConsensusOptions { LevelMergeTolerance = 0.0005m })
            .BuildAsync(ConsensusTestData.Request(context, analyses), CancellationToken.None);

        Assert.Equal(3, result.MarketLevels.Count);
        Assert.Contains(result.MarketLevels, level => level.SourceRuns.Count == 2);
        Assert.Contains(result.MarketLevels, level => level.Type == AgentMarketLevelType.Resistance);
        Assert.NotEmpty(result.Sources);
        Assert.All(result.Sources, source => Assert.NotEmpty(source.References));
    }

    [Fact]
    public async Task Scenarios_are_grouped_only_when_equivalent_and_critical_risks_are_never_dropped()
    {
        var context = ConsensusTestData.Context();
        var scenarioA = ConsensusTestData.Scenario("bull-a", AgentDirectionalBias.Bullish, "above resistance", "below support");
        var scenarioB = ConsensusTestData.Scenario("bull-b", AgentDirectionalBias.Bullish, "above resistance", "below support");
        var scenarioC = ConsensusTestData.Scenario("bear-c", AgentDirectionalBias.Bearish, "below support", "above resistance");
        var analyses = new[]
        {
            ConsensusTestData.Result(context, "run-a", "agent-a", AgentDirectionalBias.Bullish, scenarios: [scenarioA], risks: ["critical: liquidity gap"]),
            ConsensusTestData.Result(context, "run-b", "agent-b", AgentDirectionalBias.Bullish, scenarios: [scenarioB], risks: ["critical: liquidity gap", "high: spread"]),
            ConsensusTestData.Result(context, "run-c", "agent-c", AgentDirectionalBias.Bearish, scenarios: [scenarioC])
        };

        var result = await ConsensusTestData.Engine().BuildAsync(ConsensusTestData.Request(context, analyses), CancellationToken.None);

        Assert.Equal(2, result.Scenarios.Count);
        Assert.Single(result.Risks, risk => risk.Description == "critical: liquidity gap");
        Assert.Equal(ConsensusRiskSeverity.Critical, result.Risks[0].Severity);
        Assert.Contains(result.Conflicts, item => item.Kind == ConsensusConflictKind.Scenario);
    }

    [Fact]
    public async Task Limits_produce_deterministic_warnings_and_keep_critical_risks()
    {
        var context = ConsensusTestData.Context();
        var levels = Enumerable.Range(1, 5).Select(index => ConsensusTestData.Level($"level-{index}", AgentMarketLevelType.Custom, index)).ToArray();
        var risks = Enumerable.Range(1, 5).Select(index => $"low: risk-{index}").Append("critical: never-drop").ToArray();
        var analysis = ConsensusTestData.Result(context, "run-a", "agent-a", AgentDirectionalBias.Bullish, levels: levels, risks: risks);

        var result = await ConsensusTestData.Engine(new ConsensusOptions
        {
            MaximumMarketLevels = 2,
            MaximumRisks = 2,
            MaximumConclusionCharacters = 20
        }).BuildAsync(ConsensusTestData.Request(context, [analysis]), CancellationToken.None);

        Assert.Equal(2, result.MarketLevels.Count);
        Assert.Contains(result.Risks, risk => risk.Description == "critical: never-drop");
        Assert.Contains(result.Warnings, warning => warning.Contains("truncated", StringComparison.OrdinalIgnoreCase));
        Assert.All(result.Warnings, warning => Assert.False(string.IsNullOrWhiteSpace(warning)));
    }

    [Fact]
    public async Task Permutations_produce_the_same_structured_output()
    {
        var context = ConsensusTestData.Context();
        var analyses = new[]
        {
            ConsensusTestData.Result(context, "run-1", "agent-1", AgentDirectionalBias.Bullish, risks: ["high: spread"]),
            ConsensusTestData.Result(context, "run-2", "agent-2", AgentDirectionalBias.Bearish, risks: ["critical: liquidity"]),
            ConsensusTestData.Result(context, "run-3", "agent-3", AgentDirectionalBias.Neutral)
        };

        var first = await ConsensusTestData.Engine().BuildAsync(ConsensusTestData.Request(context, analyses), CancellationToken.None);
        var second = await ConsensusTestData.Engine().BuildAsync(ConsensusTestData.Request(context, analyses.Reverse().ToArray()), CancellationToken.None);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.Conclusion.Summary, second.Conclusion.Summary);
        Assert.Equal(first.Confidence.Score, second.Confidence.Score, 8);
        Assert.Equal(first.Conflicts.Select(item => item.Summary), second.Conflicts.Select(item => item.Summary));
        Assert.Equal(first.Risks.Select(item => item.Description), second.Risks.Select(item => item.Description));
    }

    [Fact]
    public async Task Request_and_result_collections_are_defensive_copies()
    {
        var context = ConsensusTestData.Context();
        var analyses = new List<AgentAnalysisResult>
        {
            ConsensusTestData.Result(context, "run-a", "agent-a", AgentDirectionalBias.Bullish)
        };
        var request = ConsensusTestData.Request(context, analyses);
        analyses.Clear();
        var result = await ConsensusTestData.Engine().BuildAsync(request, CancellationToken.None);

        Assert.Single(request.Analyses);
        Assert.Single(result.EligibleAnalyses);
        Assert.Throws<NotSupportedException>(() => ((IList<ConsensusEligibleAnalysis>)result.EligibleAnalyses).Add(result.EligibleAnalyses[0]));
    }

    [Fact]
    public async Task Timeout_returns_structured_result_and_propagates_token_to_provider()
    {
        var context = ConsensusTestData.Context();
        var analysis = ConsensusTestData.Result(context, "run-a", "agent-a", AgentDirectionalBias.Bullish);
        var engine = ConsensusTestData.Engine(
            eligibility: new DelayedEligibilityPolicy(TimeSpan.FromSeconds(1)));

        var result = await engine.BuildAsync(ConsensusTestData.Request(context, [analysis], TimeSpan.FromMilliseconds(20)), CancellationToken.None);

        Assert.Equal(ConsensusStatus.TimedOut, result.Status);
        Assert.Contains(result.Errors, error => error.Code == "CONSENSUS_TIMEOUT");
    }

    [Fact]
    public async Task External_cancellation_is_distinguished_from_timeout()
    {
        var context = ConsensusTestData.Context();
        var analysis = ConsensusTestData.Result(context, "run-a", "agent-a", AgentDirectionalBias.Bullish);
        var engine = ConsensusTestData.Engine(eligibility: new DelayedEligibilityPolicy(TimeSpan.FromSeconds(1)));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.BuildAsync(ConsensusTestData.Request(context, [analysis], TimeSpan.FromSeconds(1)), cancellation.Token));
    }

    [Fact]
    public void Options_and_di_registration_are_validated_without_capturing_a_scope()
    {
        var services = new ServiceCollection();
        services.AddTradeMindConsensus();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        Assert.IsType<ConsensusEngine>(scope.ServiceProvider.GetRequiredService<IConsensusEngine>());
        Assert.IsType<DefaultConsensusEligibilityPolicy>(provider.GetRequiredService<IConsensusEligibilityPolicy>());

        var invalid = new ConsensusOptions { MaximumRisks = 0 };
        var validation = new ConsensusOptionsValidator().Validate(null, invalid);
        Assert.False(validation.Succeeded);
    }

    [Fact]
    public async Task Unsupported_request_version_is_a_structured_failure()
    {
        var context = ConsensusTestData.Context();
        var request = new ConsensusRequest(ConsensusId.New(), context.Id, [], version: 2);

        var result = await ConsensusTestData.Engine().BuildAsync(request, CancellationToken.None);

        Assert.Equal(ConsensusStatus.Failed, result.Status);
        Assert.Contains(result.Errors, error => error.Code == "UNSUPPORTED_REQUEST_VERSION");
    }
}
