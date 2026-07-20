using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.TradingPlans.Tests;

public sealed class TradingPlanGeneratorTests
{
    [Fact]
    public async Task Long_approved_decision_produces_candidate_without_changing_levels_or_quantity()
    {
        var request = TradingPlanTestData.Request();

        var result = await TradingPlanTestData.Generator().GenerateAsync(request, CancellationToken.None);

        Assert.Equal(TradingPlanType.ExecutableCandidate, result.Type);
        Assert.Equal(TradingPlanStatus.Succeeded, result.Status);
        Assert.Equal(TradingPlanDirection.Long, result.Direction);
        Assert.Equal(1.1000m, result.Entry!.Price.Value);
        Assert.Equal(1.0900m, result.Stop!.Price.Value);
        Assert.Equal(1.1200m, result.Targets.Single().Price.Value);
        Assert.Equal(0.10m, result.Quantity!.FinalQuantity);
        Assert.Equal(RiskVerdict.Approved, result.RiskVerdict);
        Assert.Equal(TradingPlanRiskOutcome.Approved, result.RiskOutcome);
    }

    [Fact]
    public async Task Short_approved_decision_preserves_short_direction()
    {
        var result = await TradingPlanTestData.Generator().GenerateAsync(
            TradingPlanTestData.Request(TradingPlanTestData.Decision(TradingDecisionType.ShortSetup)), CancellationToken.None);

        Assert.Equal(TradingPlanType.ExecutableCandidate, result.Type);
        Assert.Equal(TradingPlanDirection.Short, result.Direction);
        Assert.Equal(1.1100m, result.Stop!.Price.Value);
        Assert.Equal(1.0800m, result.Targets.Single().Price.Value);
    }

    [Fact]
    public async Task Approved_with_reduction_preserves_the_exact_reduced_quantity()
    {
        var decision = TradingPlanTestData.Decision();
        var risk = TradingPlanTestData.Risk(decision, RiskVerdict.Reduced, quantity: 0.05m);
        var result = await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(decision, risk), CancellationToken.None);

        Assert.Equal(TradingPlanType.ExecutableCandidate, result.Type);
        Assert.Equal(TradingPlanRiskOutcome.ApprovedWithReduction, result.RiskOutcome);
        Assert.Equal(0.05m, result.Quantity!.FinalQuantity);
        Assert.Contains(result.Warnings, warning => warning.Code == "RISK_QUANTITY_REDUCED");
        Assert.Contains(result.ManagementRules, rule => rule.Type == TradingPlanRuleType.DoNotExceedApprovedQuantity);
    }

    [Fact]
    public async Task Rejected_risk_produces_no_trade_plan()
    {
        var decision = TradingPlanTestData.Decision();
        var risk = TradingPlanTestData.Risk(decision, RiskVerdict.Rejected, RiskAssessmentStatus.Rejected);

        var result = await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(decision, risk), CancellationToken.None);

        Assert.Equal(TradingPlanType.NoTradePlan, result.Type);
        Assert.Equal(TradingPlanStatus.Rejected, result.Status);
        Assert.Null(result.Quantity);
        Assert.Contains(result.Errors, error => error.Code == TradingPlanEligibilityCode.MissingQuantity.ToString());
    }

    [Theory]
    [InlineData(TradingDecisionType.Wait, TradingPlanType.WaitPlan)]
    [InlineData(TradingDecisionType.Monitor, TradingPlanType.MonitorPlan)]
    [InlineData(TradingDecisionType.Conflicted, TradingPlanType.ConflictedPlan)]
    [InlineData(TradingDecisionType.InsufficientData, TradingPlanType.InsufficientDataPlan)]
    public async Task Non_directional_decisions_map_to_their_declarative_plan_type(TradingDecisionType decisionType, TradingPlanType expectedType)
    {
        var status = decisionType == TradingDecisionType.InsufficientData ? TradingDecisionStatus.InsufficientData : TradingDecisionStatus.Succeeded;
        var decision = TradingPlanTestData.Decision(decisionType, status: status);

        var result = await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(decision), CancellationToken.None);

        Assert.Equal(expectedType, result.Type);
        Assert.Null(result.Entry);
        Assert.Null(result.Stop);
        Assert.Null(result.Quantity);
    }

    [Fact]
    public async Task Request_and_result_are_versioned_and_collections_are_immutable()
    {
        Assert.Throws<ArgumentException>(() => new TradingPlanId(Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => TradingPlanTestData.Request(timeout: TimeSpan.Zero));
        var result = await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(), CancellationToken.None);
        var source = new List<TradingPlanTarget>(result.Targets);
        source.Clear();

        Assert.Single(result.Targets);
        Assert.Equal(TradingPlanResult.CurrentSchemaVersion, result.SchemaVersion);
    }

    [Fact]
    public async Task Context_and_identifier_mismatches_are_structured_rejections()
    {
        var decision = TradingPlanTestData.Decision();
        var risk = TradingPlanTestData.Risk(decision, contextId: MarketContextId.New());
        var contextMismatch = await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(decision, risk), CancellationToken.None);
        var idMismatch = await TradingPlanTestData.Generator().GenerateAsync(
            TradingPlanTestData.Request(decision, TradingPlanTestData.Risk(decision), decisionId: TradingDecisionId.New()), CancellationToken.None);

        Assert.Equal(TradingPlanType.NoTradePlan, contextMismatch.Type);
        Assert.Contains(contextMismatch.Errors, error => error.Code == TradingPlanEligibilityCode.ContextMismatch.ToString());
        Assert.Contains(idMismatch.Errors, error => error.Code == TradingPlanEligibilityCode.DecisionIdMismatch.ToString());
    }

    [Fact]
    public async Task Instrument_timeframe_and_direction_divergences_are_never_hidden()
    {
        var decision = TradingPlanTestData.Decision();
        var instrumentResult = await TradingPlanTestData.Generator().GenerateAsync(
            TradingPlanTestData.Request(decision, TradingPlanTestData.Risk(decision, instrument: new Instrument("GBPUSD"))), CancellationToken.None);
        var timeframeResult = await TradingPlanTestData.Generator().GenerateAsync(
            TradingPlanTestData.Request(decision, TradingPlanTestData.Risk(decision, timeframe: Timeframe.H4)), CancellationToken.None);
        var directionResult = await TradingPlanTestData.Generator().GenerateAsync(
            TradingPlanTestData.Request(decision, TradingPlanTestData.Risk(decision, stopDirection: MarketDirection.Short)), CancellationToken.None);

        Assert.Contains(instrumentResult.Errors, error => error.Code == TradingPlanEligibilityCode.InstrumentMismatch.ToString());
        Assert.Contains(timeframeResult.Errors, error => error.Code == TradingPlanEligibilityCode.TimeframeMismatch.ToString());
        Assert.Contains(directionResult.Errors, error => error.Code == TradingPlanEligibilityCode.DivergentDirection.ToString());
    }

    [Fact]
    public async Task Directional_plan_requires_scenario_entry_stop_quantity_and_invalidation()
    {
        var missingScenario = TradingPlanTestData.Decision(primaryScenario: false);
        var missingEntry = TradingPlanTestData.Decision(entry: null);
        var missingStop = TradingPlanTestData.Decision(includeStop: false);
        var missingQuantityDecision = TradingPlanTestData.Decision();
        var missingQuantityRisk = TradingPlanTestData.Risk(missingQuantityDecision, RiskVerdict.Rejected, RiskAssessmentStatus.Rejected);
        var missingInvalidation = TradingPlanTestData.Decision(invalidations: []);

        var results = new[]
        {
            await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(missingScenario), CancellationToken.None),
            await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(missingEntry), CancellationToken.None),
            await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(missingStop), CancellationToken.None),
            await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(missingQuantityDecision, missingQuantityRisk), CancellationToken.None),
            await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(missingInvalidation), CancellationToken.None)
        };

        Assert.All(results, result => Assert.Equal(TradingPlanType.NoTradePlan, result.Type));
        Assert.Contains(results, result => result.Errors.Any(error => error.Code == TradingPlanEligibilityCode.MissingScenario.ToString()));
        Assert.Contains(results, result => result.Errors.Any(error => error.Code == TradingPlanEligibilityCode.MissingEntry.ToString()));
        Assert.Contains(results, result => result.Errors.Any(error => error.Code == TradingPlanEligibilityCode.MissingStop.ToString()));
        Assert.Contains(results, result => result.Errors.Any(error => error.Code == TradingPlanEligibilityCode.MissingQuantity.ToString()));
        Assert.Contains(results, result => result.Errors.Any(error => error.Code == TradingPlanEligibilityCode.MissingInvalidation.ToString()));
    }

    [Fact]
    public async Task No_level_or_sizing_is_invented()
    {
        var decision = TradingPlanTestData.Decision(targets: []);
        var risk = TradingPlanTestData.Risk(decision, RiskVerdict.Approved, quantity: 0.07m);
        var result = await TradingPlanTestData.Generator(new TradingPlanOptions { RequireTargets = false }).GenerateAsync(
            TradingPlanTestData.Request(decision, risk), CancellationToken.None);

        Assert.Empty(result.Targets);
        Assert.Equal(risk.PositionSize!.FinalQuantity, result.Quantity!.FinalQuantity);
        Assert.Equal(risk.PositionSize.ActualRisk, result.Quantity.ActualRisk);
        Assert.DoesNotContain(result.ManagementRules, rule => rule.Type == TradingPlanRuleType.ReevaluateRiskOnEntryChange && rule.Tolerance is null);
    }

    [Fact]
    public async Task Quantity_and_traces_are_copied_from_the_risk_and_decision_sources()
    {
        var decision = TradingPlanTestData.Decision();
        var risk = TradingPlanTestData.Risk(decision, RiskVerdict.Reduced, quantity: 0.03m);
        var result = await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(decision, risk), CancellationToken.None);

        Assert.Equal(0.03m, result.Quantity!.FinalQuantity);
        Assert.Equal(risk.PositionSize!.ActualRisk, result.Quantity.ActualRisk);
        Assert.Contains(result.Traces, trace => trace.Origin == "decision");
        Assert.Contains(result.Traces, trace => trace.Origin == "risk-engine");
    }

    [Fact]
    public async Task Checklist_is_blocking_and_requires_future_user_confirmation()
    {
        var result = await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(), CancellationToken.None);

        Assert.True(result.Checklist.IsBlocking);
        var confirmation = Assert.Single(result.Checklist.Items, item => item.Type == PreTradeChecklistItemType.UserConfirmationRequired);
        Assert.False(confirmation.IsSatisfied);
        Assert.True(confirmation.IsBlocking);
    }

    [Fact]
    public async Task Critical_risks_are_preserved_and_block_acknowledgement()
    {
        var risks = Enumerable.Range(0, 4)
            .Select(index => new DecisionRisk(
                new ConsensusRisk(index == 3 ? "critical-risk" : $"risk-{index}", index == 3 ? ConsensusRiskSeverity.Critical : ConsensusRiskSeverity.Low, [new AgentRunId($"risk-{index}")]),
                "Preserved."))
            .ToArray();
        var result = await TradingPlanTestData.Generator(new TradingPlanOptions { MaximumRisks = 1 }).GenerateAsync(
            TradingPlanTestData.Request(TradingPlanTestData.Decision(risks: risks)), CancellationToken.None);

        Assert.Contains(result.Risks, risk => risk.IsCritical);
        Assert.Contains(result.Checklist.Items, item => item.Type == PreTradeChecklistItemType.CriticalRisksAcknowledged && !item.IsSatisfied);
    }

    [Fact]
    public async Task Conditions_alternatives_invalidations_and_rules_are_preserved()
    {
        var alternativeSource = new ConsensusScenario("scenario-alt", "Alternative", "Alternative scenario", AgentDirectionalBias.Bullish, ["alt-activation"], ["alt-invalidation"], ["alt-level"], [], TimeSpan.FromHours(2), 60, [new AgentRunId("alt-run")]);
        var alternative = new DecisionScenario(alternativeSource, false, 60, "Alternative retained.");
        var result = await TradingPlanTestData.Generator().GenerateAsync(
            TradingPlanTestData.Request(TradingPlanTestData.Decision(alternatives: [alternative])), CancellationToken.None);

        Assert.Contains(result.Conditions, condition => condition.Expression == "activation-confirmed");
        Assert.Single(result.Alternatives);
        Assert.Single(result.Invalidations);
        Assert.Contains(result.AbandonCriteria, criterion => criterion.Criterion == "invalidation-reached");
        Assert.Contains(result.ManagementRules, rule => rule.Type == TradingPlanRuleType.CancelOnInvalidation);
        Assert.Contains(result.ManagementRules, rule => rule.Type == TradingPlanRuleType.DoNotWidenStop);
        Assert.Contains(result.ManagementRules, rule => rule.Type == TradingPlanRuleType.ExpirePlan);
        Assert.Contains(result.ManagementRules, rule => rule.Type == TradingPlanRuleType.RebuildContextWhenStale);
    }

    [Fact]
    public async Task Expired_plan_is_never_an_executable_candidate()
    {
        var result = await TradingPlanTestData.Generator().GenerateAsync(
            TradingPlanTestData.Request(expiresAtUtc: TradingPlanTestData.Now.AddSeconds(-1)), CancellationToken.None);

        Assert.Equal(TradingPlanStatus.Expired, result.Status);
        Assert.Equal(TradingPlanType.ExpiredPlan, result.Type);
        Assert.NotEqual(TradingPlanType.ExecutableCandidate, result.Type);
        Assert.Contains(result.Errors, error => error.Code == "PLAN_EXPIRED");
    }

    [Fact]
    public async Task Stale_decision_or_risk_source_produces_insufficient_data_plan()
    {
        var staleDecision = TradingPlanTestData.Decision(completedAtUtc: TradingPlanTestData.Now.AddMinutes(-30));
        var staleRisk = TradingPlanTestData.Risk(staleDecision, completedAtUtc: TradingPlanTestData.Now.AddMinutes(-30));

        var result = await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(staleDecision, staleRisk), CancellationToken.None);

        Assert.Equal(TradingPlanStatus.InsufficientData, result.Status);
        Assert.Equal(TradingPlanType.InsufficientDataPlan, result.Type);
        Assert.Contains(result.Errors, error => error.Code == "CONTEXT_STALE");
    }

    [Fact]
    public async Task Target_truncation_is_stable_and_source_ordinal_is_respected()
    {
        var decision = TradingPlanTestData.Decision(targets: [1.1300m, 1.1200m, 1.1400m]);
        var result = await TradingPlanTestData.Generator(new TradingPlanOptions { MaximumTargets = 2 }).GenerateAsync(
            TradingPlanTestData.Request(decision), CancellationToken.None);

        Assert.Equal([1.1300m, 1.1200m], result.Targets.Select(target => target.Price.Value));
        Assert.Contains(result.Warnings, warning => warning.Code == "TARGETS_TRUNCATED");
    }

    [Fact]
    public async Task Permuting_input_target_collections_does_not_change_the_deterministic_source_order()
    {
        var firstDecision = TradingPlanTestData.Decision(targets: [1.1300m, 1.1200m, 1.1400m]);
        var secondDecision = new TradingDecisionResult(
            firstDecision.DecisionId,
            firstDecision.ConsensusId,
            firstDecision.MarketContextId,
            firstDecision.Instrument,
            firstDecision.Timeframe,
            firstDecision.Strategy,
            firstDecision.Status,
            firstDecision.Type,
            firstDecision.Confidence,
            firstDecision.PrimaryScenario,
            firstDecision.AlternativeScenarios,
            firstDecision.Entry,
            firstDecision.Stop,
            firstDecision.Targets.Reverse().ToArray(),
            firstDecision.Risks,
            firstDecision.Invalidations,
            firstDecision.Conflicts,
            firstDecision.Traces,
            firstDecision.Warnings,
            firstDecision.Errors,
            firstDecision.CreatedAtUtc,
            firstDecision.CompletedAtUtc);

        var first = await TradingPlanTestData.Generator().GenerateAsync(
            TradingPlanTestData.Request(firstDecision, TradingPlanTestData.Risk(firstDecision)), CancellationToken.None);
        var second = await TradingPlanTestData.Generator().GenerateAsync(
            TradingPlanTestData.Request(secondDecision, TradingPlanTestData.Risk(secondDecision)), CancellationToken.None);

        Assert.Equal(first.Targets.Select(target => target.Price.Value), second.Targets.Select(target => target.Price.Value));
        Assert.Equal(first.Traces.Select(trace => trace.Source.Reference.Value), second.Traces.Select(trace => trace.Source.Reference.Value));
    }

    [Fact]
    public async Task Summary_and_output_are_repeatable_and_culture_independent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var first = await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(), CancellationToken.None);
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var second = await TradingPlanTestData.Generator().GenerateAsync(TradingPlanTestData.Request(), CancellationToken.None);

            Assert.Equal(first.Summary, second.Summary);
            Assert.Equal(first.Targets.Select(item => item.Price.Value), second.Targets.Select(item => item.Price.Value));
            Assert.Equal(first.Quantity!.FinalQuantity, second.Quantity!.FinalQuantity);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Timeout_returns_structured_result_and_external_cancellation_is_propagated()
    {
        var timeout = await TradingPlanTestData.Generator(
                options: new TradingPlanOptions { Timeout = TimeSpan.FromMilliseconds(20) },
                eligibility: new DelayedPlanEligibilityPolicy(TimeSpan.FromSeconds(1)))
            .GenerateAsync(TradingPlanTestData.Request(timeout: TimeSpan.FromMilliseconds(20)), CancellationToken.None);

        using var cancellation = new CancellationTokenSource();
        var task = TradingPlanTestData.Generator(eligibility: new DelayedPlanEligibilityPolicy(TimeSpan.FromSeconds(1)))
            .GenerateAsync(TradingPlanTestData.Request(timeout: TimeSpan.FromSeconds(5)), cancellation.Token);
        cancellation.Cancel();

        Assert.Equal(TradingPlanStatus.TimedOut, timeout.Status);
        Assert.Contains(timeout.Errors, error => error.Code == "PLAN_TIMEOUT");
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public void DI_configuration_is_validated_and_scopes_are_not_captured()
    {
        using var invalid = new ServiceCollection()
            .AddTradeMindTradingPlans(options => options.MaximumTargets = 0)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var invalidScope = invalid.CreateScope();
        Assert.Throws<OptionsValidationException>(() => invalidScope.ServiceProvider.GetRequiredService<ITradingPlanGenerator>());

        using var valid = new ServiceCollection()
            .AddTradeMindTradingPlans()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var validScope = valid.CreateScope();
        Assert.NotNull(validScope.ServiceProvider.GetRequiredService<ITradingPlanGenerator>());
    }

    [Fact]
    public void Public_module_has_no_order_execution_or_forbidden_dependency()
    {
        Assert.DoesNotContain(typeof(ITradingPlanGenerator).GetMethods(), method => method.Name.Contains("Execute", StringComparison.OrdinalIgnoreCase));
        var references = typeof(ITradingPlanGenerator).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.DoesNotContain(references, name => name is "Npgsql" or "Microsoft.EntityFrameworkCore" or "TradeMind.Api" or "MetaTrader5");
    }
}
