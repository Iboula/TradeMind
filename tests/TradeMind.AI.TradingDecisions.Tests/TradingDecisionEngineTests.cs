using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.TradingDecisions.Tests;

public sealed class TradingDecisionEngineTests
{
    [Fact]
    public async Task Strong_bullish_consensus_produces_long_setup_from_explicit_levels()
    {
        var consensus = TradingDecisionTestData.Consensus(
            scenarios: [TradingDecisionTestData.Scenario()],
            levels:
            [
                TradingDecisionTestData.Level(AgentMarketLevelType.Entry, 1.1000m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Stop, 1.0900m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Target, 1.1200m)
            ],
            invalidations: [TradingDecisionTestData.Invalidation()]);

        var result = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(consensus), CancellationToken.None);

        Assert.Equal(TradingDecisionType.LongSetup, result.Type);
        Assert.Equal(TradingDecisionStatus.Succeeded, result.Status);
        Assert.Equal(1.1000m, result.Entry!.Price.Value);
        Assert.Equal(1.0900m, result.Stop!.Price.Value);
        Assert.Single(result.Targets);
        Assert.False(result.Confidence.IsProbabilityOfProfit);
    }

    [Fact]
    public async Task Strong_bearish_consensus_produces_short_setup_with_downside_targets()
    {
        var consensus = TradingDecisionTestData.Consensus(
            bias: AgentDirectionalBias.Bearish,
            scenarios: [TradingDecisionTestData.Scenario(AgentDirectionalBias.Bearish, invalidations: ["above invalidation"])],
            levels:
            [
                TradingDecisionTestData.Level(AgentMarketLevelType.Entry, 1.1000m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Stop, 1.1100m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Target, 1.0800m)
            ],
            invalidations: [TradingDecisionTestData.Invalidation("above invalidation")]);

        var result = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(consensus), CancellationToken.None);

        Assert.Equal(TradingDecisionType.ShortSetup, result.Type);
        Assert.Equal(1.1000m, result.Entry!.Price.Value);
        Assert.Equal(1.1100m, result.Stop!.Price.Value);
        Assert.Equal(1.0800m, result.Targets.Single().Price.Value);
    }

    [Fact]
    public async Task Weak_consensus_prefers_wait_without_creating_a_setup()
    {
        var consensus = TradingDecisionTestData.Consensus(
            level: ConsensusLevel.Weak,
            confidence: 70,
            scenarios: [TradingDecisionTestData.Scenario(confidence: 90)],
            levels:
            [
                TradingDecisionTestData.Level(AgentMarketLevelType.Entry, 1.1000m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Stop, 1.0900m)
            ],
            invalidations: [TradingDecisionTestData.Invalidation()]);

        var result = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(consensus), CancellationToken.None);

        Assert.Equal(TradingDecisionType.Wait, result.Type);
        Assert.Null(result.Entry);
        Assert.Null(result.Stop);
    }

    [Fact]
    public async Task Critical_conflict_produces_conflicted_result_and_preserves_risk_and_conflict()
    {
        var consensus = TradingDecisionTestData.Consensus(
            level: ConsensusLevel.Conflicted,
            conflicts: [TradingDecisionTestData.CriticalConflict()],
            risks: [TradingDecisionTestData.Risk("critical: liquidity event", ConsensusRiskSeverity.Critical)],
            scenarios: [TradingDecisionTestData.Scenario()]);

        var result = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(consensus), CancellationToken.None);

        Assert.Equal(TradingDecisionType.Conflicted, result.Type);
        Assert.Contains(result.Conflicts, conflict => conflict.Severity == ConsensusConflictSeverity.Critical);
        Assert.Contains(result.Risks, risk => risk.IsCritical);
    }

    [Fact]
    public async Task Insufficient_coverage_and_confidence_produce_insufficient_data()
    {
        var lowCoverage = TradingDecisionTestData.Consensus(confidence: 90, coverage: 20);
        var lowConfidence = TradingDecisionTestData.Consensus(confidence: 20, coverage: 90);

        var first = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(lowCoverage), CancellationToken.None);
        var second = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(lowConfidence), CancellationToken.None);

        Assert.Equal(TradingDecisionType.InsufficientData, first.Type);
        Assert.Equal(TradingDecisionType.InsufficientData, second.Type);
        Assert.Contains(first.Errors, error => error.Code.Contains("Coverage", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(second.Errors, error => error.Code.Contains("Confidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Missing_invalidation_blocks_directional_decision_without_inventing_levels()
    {
        var consensus = TradingDecisionTestData.Consensus(
            scenarios: [TradingDecisionTestData.Scenario(invalidations: [])],
            levels:
            [
                TradingDecisionTestData.Level(AgentMarketLevelType.Entry, 1.1000m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Stop, 1.0900m)
            ],
            invalidations: []);

        var result = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(consensus), CancellationToken.None);

        Assert.Equal(TradingDecisionType.NoTrade, result.Type);
        Assert.Null(result.Entry);
        Assert.Null(result.Stop);
        Assert.Contains(result.Errors, error => error.Code == "MISSING_INVALIDATION");
    }

    [Fact]
    public async Task Missing_entry_or_stop_is_not_replaced_by_a_synthetic_value()
    {
        var missingEntry = TradingDecisionTestData.Consensus(
            scenarios: [TradingDecisionTestData.Scenario()],
            levels: [TradingDecisionTestData.Level(AgentMarketLevelType.Stop, 1.0900m)],
            invalidations: [TradingDecisionTestData.Invalidation()]);
        var missingStop = TradingDecisionTestData.Consensus(
            scenarios: [TradingDecisionTestData.Scenario()],
            levels: [TradingDecisionTestData.Level(AgentMarketLevelType.Entry, 1.1000m)],
            invalidations: [TradingDecisionTestData.Invalidation()]);

        var first = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(missingEntry), CancellationToken.None);
        var second = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(missingStop), CancellationToken.None);

        Assert.Equal(TradingDecisionType.Wait, first.Type);
        Assert.Equal(TradingDecisionType.NoTrade, second.Type);
        Assert.Null(first.Entry);
        Assert.Null(second.Stop);
    }

    [Fact]
    public async Task Long_and_short_level_coherence_is_validated()
    {
        var badLong = TradingDecisionTestData.Consensus(
            scenarios: [TradingDecisionTestData.Scenario()],
            levels:
            [
                TradingDecisionTestData.Level(AgentMarketLevelType.Entry, 1.1000m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Stop, 1.1100m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Target, 1.1200m)
            ],
            invalidations: [TradingDecisionTestData.Invalidation()]);
        var badShort = TradingDecisionTestData.Consensus(
            bias: AgentDirectionalBias.Bearish,
            scenarios: [TradingDecisionTestData.Scenario(AgentDirectionalBias.Bearish, invalidations: ["above invalidation"])],
            levels:
            [
                TradingDecisionTestData.Level(AgentMarketLevelType.Entry, 1.1000m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Stop, 1.0900m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Target, 1.0800m)
            ],
            invalidations: [TradingDecisionTestData.Invalidation("above invalidation")]);

        var first = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(badLong), CancellationToken.None);
        var second = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(badShort), CancellationToken.None);

        Assert.Equal(TradingDecisionType.NoTrade, first.Type);
        Assert.Equal(TradingDecisionType.NoTrade, second.Type);
        Assert.Contains(first.Errors, error => error.Code == "INCOHERENT_STOP");
        Assert.Contains(second.Errors, error => error.Code == "INCOHERENT_STOP");
    }

    [Fact]
    public async Task Targets_must_be_coherent_and_are_sorted_by_direction()
    {
        var badTargets = TradingDecisionTestData.Consensus(
            scenarios: [TradingDecisionTestData.Scenario()],
            levels:
            [
                TradingDecisionTestData.Level(AgentMarketLevelType.Entry, 1.1000m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Stop, 1.0900m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Target, 1.1200m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Target, 1.0800m)
            ],
            invalidations: [TradingDecisionTestData.Invalidation()]);
        var validTargets = TradingDecisionTestData.Consensus(
            scenarios: [TradingDecisionTestData.Scenario()],
            levels:
            [
                TradingDecisionTestData.Level(AgentMarketLevelType.Entry, 1.1000m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Stop, 1.0900m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Target, 1.1300m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Target, 1.1200m)
            ],
            invalidations: [TradingDecisionTestData.Invalidation()]);

        var first = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(badTargets), CancellationToken.None);
        var second = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(validTargets), CancellationToken.None);

        Assert.Equal(TradingDecisionType.NoTrade, first.Type);
        Assert.Contains(first.Errors, error => error.Code == "INCOHERENT_TARGETS");
        Assert.Equal(TradingDecisionType.LongSetup, second.Type);
        Assert.Equal(new[] { 1.1200m, 1.1300m }, second.Targets.Select(target => target.Price.Value));
    }

    [Fact]
    public async Task Primary_scenario_selection_is_deterministic_and_alternatives_are_preserved()
    {
        var scenarios = new[]
        {
            TradingDecisionTestData.Scenario(id: "scenario-z", confidence: 75),
            TradingDecisionTestData.Scenario(id: "scenario-a", confidence: 90),
            TradingDecisionTestData.Scenario(AgentDirectionalBias.Bearish, "scenario-bear", 95, ["above invalidation"])
        };
        var consensus = TradingDecisionTestData.Consensus(
            scenarios: scenarios,
            levels:
            [
                TradingDecisionTestData.Level(AgentMarketLevelType.Entry, 1.1000m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Stop, 1.0900m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Target, 1.1200m)
            ],
            invalidations: [TradingDecisionTestData.Invalidation()]);

        var first = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(consensus), CancellationToken.None);
        var second = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(consensus), CancellationToken.None);

        Assert.Equal("scenario-a", first.PrimaryScenario!.Source.Id);
        Assert.Equal(first.PrimaryScenario.Source.Id, second.PrimaryScenario!.Source.Id);
        Assert.Equal(2, first.AlternativeScenarios.Count);
        Assert.Contains(first.AlternativeScenarios, item => item.Source.Id == "scenario-bear");
        Assert.Equal(first.AlternativeScenarios.Select(item => item.Source.Id), second.AlternativeScenarios.Select(item => item.Source.Id));
    }

    [Fact]
    public async Task Critical_risks_are_preserved_when_non_critical_risks_are_truncated()
    {
        var risks = Enumerable.Range(1, 5)
            .Select(index => TradingDecisionTestData.Risk($"low: risk-{index}", ConsensusRiskSeverity.Low))
            .Append(TradingDecisionTestData.Risk("critical: event", ConsensusRiskSeverity.Critical))
            .ToArray();
        var consensus = TradingDecisionTestData.Consensus(risks: risks);
        var options = new TradingDecisionOptions { MaximumRisks = 2 };

        var result = await TradingDecisionTestData.Engine(options).BuildAsync(TradingDecisionTestData.Request(consensus), CancellationToken.None);

        Assert.Contains(result.Risks, risk => risk.IsCritical);
        Assert.Contains(result.Warnings, warning => warning.Code == "RISKS_TRUNCATED");
    }

    [Fact]
    public async Task Traceability_contains_consensus_and_proposal_source_references()
    {
        var consensus = TradingDecisionTestData.Consensus(
            sources: [TradingDecisionTestData.Source()],
            scenarios: [TradingDecisionTestData.Scenario()],
            levels:
            [
                TradingDecisionTestData.Level(AgentMarketLevelType.Entry, 1.1000m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Stop, 1.0900m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Target, 1.1200m)
            ],
            risks: [TradingDecisionTestData.Risk("high: spread", ConsensusRiskSeverity.High)],
            invalidations: [TradingDecisionTestData.Invalidation()]);

        var result = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(consensus), CancellationToken.None);

        Assert.NotEmpty(result.Traces);
        Assert.Contains(result.Traces, trace => trace.Role == DecisionTraceRole.Consensus);
        Assert.Contains(result.Traces, trace => trace.Role == DecisionTraceRole.Scenario);
        Assert.Contains(result.Traces, trace => trace.Role == DecisionTraceRole.Entry);
        Assert.Contains(result.Traces, trace => trace.Role == DecisionTraceRole.Stop);
        Assert.Contains(result.Traces, trace => trace.Role == DecisionTraceRole.Target);
    }

    [Fact]
    public async Task Permutations_and_repeated_runs_produce_identical_decision_content()
    {
        var consensus = TradingDecisionTestData.Consensus(
            scenarios:
            [
                TradingDecisionTestData.Scenario(id: "z", confidence: 75),
                TradingDecisionTestData.Scenario(id: "a", confidence: 80)
            ],
            levels:
            [
                TradingDecisionTestData.Level(AgentMarketLevelType.Target, 1.1300m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Entry, 1.1000m),
                TradingDecisionTestData.Level(AgentMarketLevelType.Stop, 1.0900m)
            ],
            invalidations: [TradingDecisionTestData.Invalidation()]);

        var first = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(consensus), CancellationToken.None);
        var second = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(consensus), CancellationToken.None);

        Assert.Equal(first.Type, second.Type);
        Assert.Equal(first.PrimaryScenario!.Source.Id, second.PrimaryScenario!.Source.Id);
        Assert.Equal(first.Targets.Select(item => item.Price.Value), second.Targets.Select(item => item.Price.Value));
        Assert.Equal(first.Warnings.Select(item => item.Code), second.Warnings.Select(item => item.Code));
    }

    [Fact]
    public async Task Target_truncation_is_stable_and_warns()
    {
        var levels = new List<ConsensusMarketLevel>
        {
            TradingDecisionTestData.Level(AgentMarketLevelType.Entry, 1.1000m),
            TradingDecisionTestData.Level(AgentMarketLevelType.Stop, 1.0900m)
        };
        levels.AddRange(Enumerable.Range(1, 4).Select(index => TradingDecisionTestData.Level(AgentMarketLevelType.Target, 1.1100m + index / 1000m)));
        var consensus = TradingDecisionTestData.Consensus(
            scenarios: [TradingDecisionTestData.Scenario()],
            levels: levels,
            invalidations: [TradingDecisionTestData.Invalidation()]);

        var result = await TradingDecisionTestData.Engine(new TradingDecisionOptions { MaximumTargets = 2 })
            .BuildAsync(TradingDecisionTestData.Request(consensus), CancellationToken.None);

        Assert.Equal(2, result.Targets.Count);
        Assert.Contains(result.Warnings, warning => warning.Code == "TARGETS_TRUNCATED");
    }

    [Fact]
    public async Task Timeout_returns_structured_failure_and_external_cancellation_is_propagated()
    {
        var consensus = TradingDecisionTestData.Consensus();
        var slowEngine = TradingDecisionTestData.Engine(
            eligibility: new DelayedEligibilityPolicy(TimeSpan.FromSeconds(1)));
        var timeoutResult = await slowEngine.BuildAsync(TradingDecisionTestData.Request(consensus, TimeSpan.FromMilliseconds(20)), CancellationToken.None);

        Assert.Equal(TradingDecisionStatus.TimedOut, timeoutResult.Status);
        Assert.Contains(timeoutResult.Errors, error => error.Code == "DECISION_TIMEOUT");

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAsync<OperationCanceledException>(() => slowEngine.BuildAsync(TradingDecisionTestData.Request(consensus, TimeSpan.FromSeconds(1)), cancellation.Token));
    }

    [Fact]
    public async Task Incoherent_ids_and_request_versions_are_rejected()
    {
        var consensus = TradingDecisionTestData.Consensus();
        var mismatch = TradingDecisionTestData.Request(consensus, requestedConsensusId: ConsensusId.New());
        var unsupported = TradingDecisionTestData.Request(consensus, version: 2);

        var first = await TradingDecisionTestData.Engine().BuildAsync(mismatch, CancellationToken.None);
        var second = await TradingDecisionTestData.Engine().BuildAsync(unsupported, CancellationToken.None);

        Assert.Equal(TradingDecisionType.NoTrade, first.Type);
        Assert.Contains(first.Errors, error => error.Code == nameof(TradingDecisionRejectionCode.ConsensusMismatch));
        Assert.Contains(second.Errors, error => error.Code == nameof(TradingDecisionRejectionCode.UnsupportedRequestVersion));
    }

    [Fact]
    public async Task Result_and_request_collections_are_defensive_copies()
    {
        var scenarios = new List<ConsensusScenario> { TradingDecisionTestData.Scenario() };
        var consensus = TradingDecisionTestData.Consensus(scenarios: scenarios);
        scenarios.Clear();
        var result = await TradingDecisionTestData.Engine().BuildAsync(TradingDecisionTestData.Request(consensus), CancellationToken.None);

        Assert.Single(consensus.Scenarios);
        var allScenarios = result.AlternativeScenarios.ToList();
        if (result.PrimaryScenario is not null)
        {
            allScenarios.Add(result.PrimaryScenario);
        }

        Assert.Single(allScenarios);
        Assert.Throws<NotSupportedException>(() => ((IList<DecisionScenario>)result.AlternativeScenarios).Add(result.AlternativeScenarios.FirstOrDefault()!));
    }

    [Fact]
    public void Di_is_validated_and_application_has_no_forbidden_runtime_dependencies()
    {
        var services = new ServiceCollection();
        services.AddTradeMindTradingDecisions();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        Assert.IsType<TradingDecisionEngine>(scope.ServiceProvider.GetRequiredService<ITradingDecisionEngine>());

        var invalid = new TradingDecisionOptions { MaximumTargets = 0 };
        Assert.False(new TradingDecisionOptionsValidator().Validate(null, invalid).Succeeded);

        var forbidden = typeof(TradingDecisionEngine).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .Where(name => name.Contains("MT5", StringComparison.OrdinalIgnoreCase)
                || name.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
                || name.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase)
                || name.Contains("OpenAI", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Empty(forbidden);
    }
}
