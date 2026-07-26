using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.RiskEngine.Tests;

public sealed class RiskEngineTests
{
    [Fact]
    public async Task Long_setup_calculates_conservative_decimal_size_and_r_multiple()
    {
        var result = await RiskTestData.Engine().AssessAsync(RiskTestData.Request(), CancellationToken.None);

        Assert.Equal(RiskAssessmentStatus.Succeeded, result.Status);
        Assert.Equal(RiskVerdict.Approved, result.Verdict);
        Assert.Equal(0.1m, result.PositionSize!.FinalQuantity);
        Assert.Equal(100m, result.PositionSize.ActualRisk.Amount);
        Assert.Equal(2m, result.RMultiples.Single().Value);
        Assert.Equal("(StopDistance / TickSize) * TickValue", result.PositionSize.Formula);
    }

    [Fact]
    public async Task Short_setup_uses_the_same_absolute_distance_formula()
    {
        var result = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(RiskTestData.Decision(TradingDecisionType.ShortSetup)),
            CancellationToken.None);

        Assert.Equal(RiskAssessmentStatus.Succeeded, result.Status);
        Assert.Equal(MarketDirection.Short, result.StopDistance!.Direction);
        Assert.Equal(2m, result.RMultiples.Single().Value);
    }

    [Theory]
    [InlineData(TradingDecisionType.Wait)]
    [InlineData(TradingDecisionType.Monitor)]
    [InlineData(TradingDecisionType.Conflicted)]
    [InlineData(TradingDecisionType.NoTrade)]
    public async Task Non_directional_decisions_never_produce_sizing(TradingDecisionType type)
    {
        var result = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(RiskTestData.Decision(type)),
            CancellationToken.None);

        Assert.Null(result.PositionSize);
        Assert.Equal(RiskVerdict.NoTrade, result.Verdict);
        Assert.Contains(result.Errors, error => error.Code == RiskRejectionCode.DecisionNotDirectional.ToString());
    }

    [Fact]
    public async Task Insufficient_data_is_distinguished_from_a_normal_no_trade()
    {
        var result = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(RiskTestData.Decision(TradingDecisionType.InsufficientData)), CancellationToken.None);

        Assert.Equal(RiskAssessmentStatus.InsufficientData, result.Status);
        Assert.Equal(RiskVerdict.InsufficientData, result.Verdict);
        Assert.Null(result.PositionSize);
    }

    [Fact]
    public async Task Missing_or_incoherent_stop_is_rejected_without_inventing_a_level()
    {
        var missing = RiskTestData.Decision();
        var noStop = new TradingDecisionResult(
            missing.DecisionId, missing.ConsensusId, missing.MarketContextId, missing.Instrument, missing.Timeframe,
            missing.Strategy, missing.Status, missing.Type, missing.Confidence, missing.PrimaryScenario,
            missing.AlternativeScenarios, missing.Entry, null, missing.Targets, missing.Risks, missing.Invalidations,
            missing.Conflicts, missing.Traces, missing.Warnings, missing.Errors, missing.CreatedAtUtc, missing.CompletedAtUtc);
        var incoherent = RiskTestData.Decision(TradingDecisionType.LongSetup, stop: 1.1100m);

        var noStopResult = await RiskTestData.Engine().AssessAsync(RiskTestData.Request(noStop), CancellationToken.None);
        var incoherentResult = await RiskTestData.Engine().AssessAsync(RiskTestData.Request(incoherent), CancellationToken.None);

        Assert.Null(noStopResult.StopDistance);
        Assert.Null(noStopResult.PositionSize);
        Assert.Contains(noStopResult.Errors, error => error.Code == RiskRejectionCode.InvalidStop.ToString());
        Assert.Null(incoherentResult.PositionSize);
        Assert.Contains(incoherentResult.Errors, error => error.Code == RiskRejectionCode.InvalidStop.ToString());
    }

    [Fact]
    public async Task Most_restrictive_percent_amount_and_daily_limit_wins()
    {
        var result = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(profile: RiskTestData.Profile(percent: 2, amount: new Money(180, RiskTestData.Usd), daily: new Money(50, RiskTestData.Usd))),
            CancellationToken.None);

        Assert.Equal(RiskVerdict.Reduced, result.Verdict);
        Assert.Equal(50m, result.EffectiveRiskBudget!.Amount.Amount);
        Assert.Equal(RiskConstraintKind.DailyLoss, result.EffectiveRiskBudget.LimitingConstraint);
        Assert.Contains(result.Constraints, constraint => constraint.Kind == RiskConstraintKind.DailyLoss);
    }

    [Fact]
    public async Task Exhausted_daily_loss_and_drawdown_are_hard_rejections()
    {
        var daily = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(profile: RiskTestData.Profile(daily: new Money(100, RiskTestData.Usd)), account: RiskTestData.Account(daily: 100)), CancellationToken.None);
        var drawdown = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(profile: RiskTestData.Profile(drawdown: new Money(100, RiskTestData.Usd)), account: RiskTestData.Account(drawdown: 100)), CancellationToken.None);

        Assert.Equal(RiskAssessmentStatus.Rejected, daily.Status);
        Assert.Equal(RiskConstraintKind.DailyLoss, daily.EffectiveRiskBudget!.LimitingConstraint);
        Assert.True(drawdown.Drawdown!.Breached);
        Assert.Equal(RiskConstraintKind.Drawdown, drawdown.EffectiveRiskBudget!.LimitingConstraint);
    }

    [Fact]
    public async Task Portfolio_exposure_reduces_quantity_conservatively()
    {
        var request = RiskTestData.Request(
            profile: RiskTestData.Profile(percent: 1, amount: new Money(1000, RiskTestData.Usd)),
            specification: RiskTestData.Specification(exposurePerQuantity: new Money(100, RiskTestData.Usd)),
            portfolio: RiskTestData.Portfolio(currentExposure: 95, maximumExposure: 100));

        var result = await RiskTestData.Engine().AssessAsync(request, CancellationToken.None);

        Assert.Equal(RiskVerdict.Reduced, result.Verdict);
        Assert.Equal(0.05m, result.PositionSize!.FinalQuantity);
        Assert.Equal(5m, result.PositionSize.ActualExposure!.Value.Amount);
        Assert.Contains("portfolio-exposure-limit", result.PositionSize.ReductionReasons);
    }

    [Fact]
    public async Task Portfolio_absence_is_reported_without_failing_a_valid_risk_calculation()
    {
        var result = await RiskTestData.Engine().AssessAsync(RiskTestData.Request(), CancellationToken.None);

        Assert.Null(result.Exposure);
        Assert.Contains(result.Warnings, warning => warning.Code == "PORTFOLIO_CONTEXT_UNAVAILABLE");
    }

    [Fact]
    public async Task Minimum_quantity_exceeding_budget_is_rejected()
    {
        var result = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(specification: RiskTestData.Specification(minimum: 1)), CancellationToken.None);

        Assert.Null(result.PositionSize);
        Assert.Contains(result.Errors, error => error.Code == RiskRejectionCode.QuantityBelowMinimum.ToString());
    }

    [Fact]
    public async Task Maximum_quantity_is_applied_as_a_deterministic_reduction()
    {
        var result = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(profile: RiskTestData.Profile(percent: 10, amount: new Money(10_000, RiskTestData.Usd)), specification: RiskTestData.Specification(maximum: 0.05m)), CancellationToken.None);

        Assert.Equal(0.05m, result.PositionSize!.FinalQuantity);
        Assert.Contains("maximum-quantity-limit", result.PositionSize.ReductionReasons);
    }

    [Fact]
    public async Task Targets_with_wrong_direction_or_below_minimum_are_rejected()
    {
        var wrongDirection = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(RiskTestData.Decision(targets: [1.0800m])), CancellationToken.None);
        var belowMinimum = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(RiskTestData.Decision(targets: [1.1050m])), CancellationToken.None);

        Assert.Null(wrongDirection.PositionSize);
        Assert.Null(belowMinimum.PositionSize);
        Assert.Contains(wrongDirection.Errors, error => error.Code == RiskRejectionCode.InvalidSizing.ToString());
        Assert.Contains(belowMinimum.Errors, error => error.Code == RiskRejectionCode.InvalidSizing.ToString());
    }

    [Fact]
    public async Task No_target_is_normal_missing_data_not_an_exception()
    {
        var decision = RiskTestData.Decision(targets: []);
        var result = await RiskTestData.Engine().AssessAsync(RiskTestData.Request(decision), CancellationToken.None);

        Assert.Equal(RiskAssessmentStatus.Succeeded, result.Status);
        Assert.Empty(result.RMultiples);
        Assert.Contains(result.Warnings, warning => warning.Code == "R_MULTIPLE_UNAVAILABLE");
    }

    [Fact]
    public async Task Risks_and_source_traces_are_preserved_and_critical_risks_survive_limits()
    {
        var risks = Enumerable.Range(0, 5)
            .Select(index => new DecisionRisk(
                new ConsensusRisk($"risk-{index}", index == 4 ? ConsensusRiskSeverity.Critical : ConsensusRiskSeverity.Low, [new AgentRunId($"run-{index}")]),
                "Preserved."))
            .ToArray();
        var result = await RiskTestData.Engine(new RiskEngineOptions { MaximumRisks = 2, MaximumTraces = 1 })
            .AssessAsync(RiskTestData.Request(RiskTestData.Decision(risks: risks)), CancellationToken.None);

        Assert.Contains(result.Risks, risk => risk.IsCritical);
        Assert.Single(result.Traces);
    }

    [Fact]
    public async Task Request_decision_id_currency_and_specification_invariants_are_enforced()
    {
        var decision = RiskTestData.Decision();
        var mismatch = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(decision, requestedDecisionId: TradingDecisionId.New()), CancellationToken.None);

        Assert.Contains(mismatch.Errors, error => error.Code == RiskRejectionCode.DecisionMismatch.ToString());
        Assert.Throws<ArgumentException>(() => new RiskProfile("bad", new CurrencyCode("EUR"), maximumRiskAmountPerTrade: new Money(10, RiskTestData.Usd)));
        Assert.Throws<ArgumentOutOfRangeException>(() => RiskTestData.Specification(tickSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RiskTestData.Specification(tickValue: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RiskTestData.Account(equity: 0));
    }

    [Fact]
    public void Versioned_ids_requests_and_results_are_immutable_at_the_boundary()
    {
        Assert.Throws<ArgumentException>(() => new RiskAssessmentId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new TradingDecisionId(Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RiskAssessmentRequest(
            RiskAssessmentId.New(), RiskTestData.Decision(), RiskTestData.Profile(), RiskTestData.Account(), RiskTestData.Specification(), timeout: TimeSpan.Zero));

        var sourceTraces = new List<DecisionTraceReference>();
        var decision = RiskTestData.Decision(traces: sourceTraces);
        sourceTraces.Add(new DecisionTraceReference(
            new AgentRunId("late-run"), new AgentId("late-agent"), AgentVersion.Parse("1.0.0"),
            new ContextSourceReference("late", "reference"), DecisionTraceRole.Risk));

        Assert.Empty(decision.Traces);
    }

    [Fact]
    public async Task Hard_additional_constraint_is_preserved_and_rejects()
    {
        var limit = new RiskLimit(RiskConstraintKind.Additional, new Money(10, RiskTestData.Usd), new Money(10, RiskTestData.Usd), "firm-hard-limit", true, false);
        var result = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(profile: RiskTestData.Profile(additional: [limit])), CancellationToken.None);

        Assert.Equal(RiskAssessmentStatus.Rejected, result.Status);
        Assert.Contains(result.Constraints, constraint => constraint.Source == "firm-hard-limit" && constraint.IsHard);
    }

    [Fact]
    public async Task Decimal_calculation_is_culture_independent_and_repeatable()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var first = await RiskTestData.Engine().AssessAsync(RiskTestData.Request(), CancellationToken.None);
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var second = await RiskTestData.Engine().AssessAsync(RiskTestData.Request(), CancellationToken.None);

            Assert.Equal(0.1m, first.PositionSize!.FinalQuantity);
            Assert.Equal(first.PositionSize!.FinalQuantity, second.PositionSize!.FinalQuantity);
            Assert.Equal("(StopDistance / TickSize) * TickValue", first.PositionSize!.Formula);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Permuting_targets_does_not_change_the_risk_output_order()
    {
        var first = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(RiskTestData.Decision(targets: [1.1300m, 1.1200m])), CancellationToken.None);
        var second = await RiskTestData.Engine().AssessAsync(
            RiskTestData.Request(RiskTestData.Decision(targets: [1.1200m, 1.1300m])), CancellationToken.None);

        Assert.Equal(first.RMultiples.Select(item => item.Value), second.RMultiples.Select(item => item.Value));
    }

    [Fact]
    public async Task Timeout_returns_structured_timed_out_result()
    {
        var result = await RiskTestData.Engine(
                options: new RiskEngineOptions { Timeout = TimeSpan.FromMilliseconds(20) },
                eligibility: new DelayedRiskEligibilityPolicy(TimeSpan.FromSeconds(1)))
            .AssessAsync(RiskTestData.Request(timeout: TimeSpan.FromMilliseconds(20)), CancellationToken.None);

        Assert.Equal(RiskAssessmentStatus.TimedOut, result.Status);
        Assert.Contains(result.Errors, error => error.Code == "RISK_TIMEOUT");
    }

    [Fact]
    public async Task External_cancellation_is_propagated_and_not_converted_to_a_failure()
    {
        using var cancellation = new CancellationTokenSource();
        var task = RiskTestData.Engine(eligibility: new DelayedRiskEligibilityPolicy(TimeSpan.FromSeconds(1)))
            .AssessAsync(RiskTestData.Request(timeout: TimeSpan.FromSeconds(5)), cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public void Options_and_di_registration_are_validated_without_captured_scopes()
    {
        using var services = new ServiceCollection()
            .AddTradeMindRiskEngine(options => options.MaximumRisks = 0)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<RiskEngineOptions>>().Value);
        using var scope = services.CreateScope();
        Assert.Throws<OptionsValidationException>(() => scope.ServiceProvider.GetRequiredService<IRiskEngine>());

        using var validProvider = new ServiceCollection()
            .AddTradeMindRiskEngine()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var validScope = validProvider.CreateScope();
        Assert.NotNull(validScope.ServiceProvider.GetRequiredService<IRiskEngine>());
    }

    [Fact]
    public async Task Cancellation_token_reaches_policy()
    {
        using var cancellation = new CancellationTokenSource();
        var policy = new CancellingEligibilityPolicy(cancellation);

        await Assert.ThrowsAsync<OperationCanceledException>(() => RiskTestData.Engine(eligibility: policy).AssessAsync(RiskTestData.Request(), cancellation.Token));
        Assert.True(policy.Observed);
    }

    [Fact]
    public void Public_risk_engine_has_no_execution_method_or_forbidden_dependency()
    {
        var methods = typeof(IRiskEngine).GetMethods().Select(method => method.Name).ToArray();
        Assert.DoesNotContain(methods, name => name.Contains("Execute", StringComparison.OrdinalIgnoreCase));
        var references = typeof(IRiskEngine).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.DoesNotContain(references, name => name is "Npgsql" or "Microsoft.EntityFrameworkCore" or "TradeMind.Api");
    }

    private sealed class CancellingEligibilityPolicy(CancellationTokenSource source) : IRiskEligibilityPolicy
    {
        public bool Observed { get; private set; }

        public ValueTask<RiskEligibilityDecision> EvaluateAsync(RiskAssessmentRequest request, CancellationToken cancellationToken)
        {
            Observed = cancellationToken.CanBeCanceled;
            source.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(RiskEligibilityDecision.Include());
        }
    }
}
