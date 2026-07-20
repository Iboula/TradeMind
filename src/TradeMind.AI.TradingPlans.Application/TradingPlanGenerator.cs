using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.ExpertAgents.Consensus.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.AI.RiskEngine.Domain;
using TradeMind.AI.TradingDecisions.Domain;
using TradeMind.AI.TradingPlans.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.TradingPlans.Application;

public interface ITradingPlanGenerator
{
    Task<TradingPlanResult> GenerateAsync(
        TradingPlanRequest request,
        CancellationToken cancellationToken);
}

public sealed class TradingPlanGenerator(
    ITradingPlanEligibilityPolicy eligibilityPolicy,
    ITradingPlanExpirationPolicy expirationPolicy,
    IOptions<TradingPlanOptions> options,
    TimeProvider timeProvider,
    ILogger<TradingPlanGenerator> logger) : ITradingPlanGenerator
{
    private readonly TradingPlanOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task<TradingPlanResult> GenerateAsync(
        TradingPlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var createdAt = timeProvider.GetUtcNow();
        using var timeoutSource = new CancellationTokenSource(request.Timeout ?? _options.Timeout, timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            return await GenerateCoreAsync(request, createdAt, linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Trading plan {PlanId} was cancelled by the caller.", request.PlanId);
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            logger.LogWarning("Trading plan {PlanId} exceeded its timeout.", request.PlanId);
            return Failure(request, createdAt, timeProvider.GetUtcNow(), TradingPlanStatus.TimedOut, "PLAN_TIMEOUT", "The trading plan operation exceeded its configured timeout.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Trading plan {PlanId} failed unexpectedly.", request.PlanId);
            return Failure(request, createdAt, timeProvider.GetUtcNow(), TradingPlanStatus.Failed, "PLAN_FAILURE", "The trading plan operation failed unexpectedly.");
        }
    }

    private async Task<TradingPlanResult> GenerateCoreAsync(
        TradingPlanRequest request,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var expiration = await expirationPolicy.EvaluateAsync(request, createdAtUtc, cancellationToken).ConfigureAwait(false);
        if (expiration.Expired)
        {
            return Fallback(
                request,
                createdAtUtc,
                expiration.ExpiresAtUtc,
                TradingPlanStatus.Expired,
                TradingPlanType.ExpiredPlan,
                new TradingPlanError("PLAN_EXPIRED", expiration.Reason ?? "The trading plan has expired."),
                "The trading plan expired before it could be used.");
        }

        if (expiration.Stale)
        {
            return Fallback(
                request,
                createdAtUtc,
                expiration.ExpiresAtUtc,
                TradingPlanStatus.InsufficientData,
                TradingPlanType.InsufficientDataPlan,
                new TradingPlanError("CONTEXT_STALE", expiration.Reason ?? "The decision or risk source is stale."),
                "The trading plan requires a fresh decision and risk assessment.");
        }

        var eligibility = await eligibilityPolicy.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!eligibility.Eligible)
        {
            return Fallback(
                request,
                createdAtUtc,
                expiration.ExpiresAtUtc,
                StatusForEligibility(request, eligibility.Code),
                TypeForEligibility(request, eligibility.Code),
                new TradingPlanError(
                    eligibility.Code?.ToString() ?? TradingPlanEligibilityCode.Unknown.ToString(),
                    eligibility.Message ?? "The trading plan request is not eligible."),
                eligibility.Message ?? "The trading plan request is not eligible.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Assemble(request, createdAtUtc, expiration.ExpiresAtUtc);
    }

    private TradingPlanResult Assemble(
        TradingPlanRequest request,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        var decision = request.Decision;
        var risk = request.Risk;
        var direction = DirectionFor(decision.Type);
        var primary = BuildPrimaryScenario(decision);
        var alternatives = BuildAlternatives(decision);
        var warnings = new List<TradingPlanWarning>();
        var targets = BuildTargets(decision, warnings);
        var conditions = BuildConditions(primary, warnings);
        var invalidations = BuildInvalidations(decision, warnings);
        var abandonCriteria = BuildAbandonCriteria(invalidations, warnings);
        var approvedQuantity = IsRiskApproved(risk) && risk.PositionSize is not null
            ? new TradingPlanQuantity(risk.PositionSize)
            : null;
        var isDirectional = direction is TradingPlanDirection.Long or TradingPlanDirection.Short;
        var type = isDirectional ? TradingPlanType.ExecutableCandidate : TypeForDecision(decision.Type);
        var status = decision.Status == TradingDecisionStatus.PartiallySucceeded || risk.Status == RiskAssessmentStatus.PartiallySucceeded
            ? TradingPlanStatus.PartiallySucceeded
            : decision.Type == TradingDecisionType.InsufficientData
                ? TradingPlanStatus.InsufficientData
                : TradingPlanStatus.Succeeded;

        if (risk.Verdict == RiskVerdict.Reduced)
        {
            warnings.Add(new TradingPlanWarning("RISK_QUANTITY_REDUCED", "The plan preserves the quantity reduced by the Risk Engine."));
        }

        if (decision.Conflicts.Count > 0)
        {
            warnings.Add(new TradingPlanWarning("DECISION_CONFLICTS_PRESERVED", "Decision conflicts are preserved and require user review."));
        }

        var checklist = BuildChecklist(decision, risk, isDirectional, contextFresh: true);
        var rules = BuildRules(decision, isDirectional);
        var risks = BuildRisks(decision, warnings);
        var traces = BuildTraces(decision, risk, warnings);
        var limitations = BaseLimitations();
        var summary = BuildSummary(type, direction, decision, targets.Count, approvedQuantity);

        return Materialize(
            request,
            createdAtUtc,
            timeProvider.GetUtcNow(),
            expiresAtUtc,
            direction,
            status,
            type,
            risk.Verdict,
            primary,
            alternatives,
            decision.Entry is null ? null : new TradingPlanEntry(decision.Entry),
            decision.Stop is null ? null : new TradingPlanStop(decision.Stop),
            targets,
            approvedQuantity,
            conditions,
            invalidations,
            abandonCriteria,
            checklist,
            rules,
            risks,
            traces,
            limitations,
            warnings,
            [],
            summary);
    }

    private TradingPlanResult Fallback(
        TradingPlanRequest request,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc,
        TradingPlanStatus status,
        TradingPlanType type,
        TradingPlanError error,
        string summary)
    {
        var decision = request.Decision;
        var risk = request.Risk;
        var direction = DirectionFor(decision.Type);
        var primary = BuildPrimaryScenario(decision);
        var warnings = new List<TradingPlanWarning>();
        var targets = BuildTargets(decision, warnings);
        var conditions = BuildConditions(primary, warnings);
        var invalidations = BuildInvalidations(decision, warnings);
        var abandonCriteria = BuildAbandonCriteria(invalidations, warnings);
        var quantity = IsRiskApproved(risk) && risk.PositionSize is not null
            ? new TradingPlanQuantity(risk.PositionSize)
            : null;
        var checklist = BuildChecklist(decision, risk, false, status != TradingPlanStatus.InsufficientData);
        var risks = BuildRisks(decision, warnings);
        var traces = BuildTraces(decision, risk, warnings);
        var alternatives = BuildAlternatives(decision);
        var limitations = BaseLimitations();

        if (status == TradingPlanStatus.Expired)
        {
            warnings.Add(new TradingPlanWarning("PLAN_NOT_EXECUTABLE", "An expired plan is informational only and cannot be an executable candidate."));
        }

        return Materialize(
            request,
            createdAtUtc,
            timeProvider.GetUtcNow(),
            expiresAtUtc,
            direction,
            status,
            type,
            risk.Verdict,
            primary,
            alternatives,
            decision.Entry is null ? null : new TradingPlanEntry(decision.Entry),
            decision.Stop is null ? null : new TradingPlanStop(decision.Stop),
            targets,
            quantity,
            conditions,
            invalidations,
            abandonCriteria,
            checklist,
            BuildRules(decision, direction is not TradingPlanDirection.None),
            risks,
            traces,
            limitations,
            warnings,
            [error],
            summary);
    }

    private TradingPlanResult Failure(
        TradingPlanRequest request,
        DateTimeOffset createdAtUtc,
        DateTimeOffset completedAtUtc,
        TradingPlanStatus status,
        string code,
        string message) =>
        Materialize(
            request,
            createdAtUtc,
            completedAtUtc,
            request.ExpiresAtUtc ?? createdAtUtc.Add(_options.DefaultPlanLifetime),
            DirectionFor(request.Decision.Type),
            status,
            TradingPlanType.NoTradePlan,
            request.Risk.Verdict,
            BuildPrimaryScenario(request.Decision),
            BuildAlternatives(request.Decision),
            null,
            null,
            [],
            null,
            [],
            [],
            [],
            new PreTradeChecklist([]),
            [],
            [],
            [],
            BaseLimitations(),
            [],
            [new TradingPlanError(code, message)],
            "No trading plan was produced because the generator failed.");

    private TradingPlanResult Materialize(
        TradingPlanRequest request,
        DateTimeOffset createdAtUtc,
        DateTimeOffset completedAtUtc,
        DateTimeOffset expiresAtUtc,
        TradingPlanDirection direction,
        TradingPlanStatus status,
        TradingPlanType type,
        RiskVerdict riskVerdict,
        TradingPlanScenario? primary,
        IReadOnlyCollection<TradingPlanScenario> alternatives,
        TradingPlanEntry? entry,
        TradingPlanStop? stop,
        IReadOnlyCollection<TradingPlanTarget> targets,
        TradingPlanQuantity? quantity,
        IReadOnlyCollection<TradingPlanCondition> conditions,
        IReadOnlyCollection<TradingPlanInvalidation> invalidations,
        IReadOnlyCollection<TradingPlanAbandonCriterion> abandonCriteria,
        PreTradeChecklist checklist,
        IReadOnlyCollection<DeclarativeTradeManagementRule> rules,
        IReadOnlyCollection<TradingPlanRisk> risks,
        IReadOnlyCollection<TradingPlanTraceReference> traces,
        IReadOnlyCollection<TradingPlanLimitation> limitations,
        IReadOnlyCollection<TradingPlanWarning> warnings,
        IReadOnlyCollection<TradingPlanError> errors,
        string summary) =>
        new(
            request.PlanId,
            request.DecisionId,
            request.RiskAssessmentId,
            request.MarketContextId,
            request.Decision.Instrument,
            request.Decision.Timeframe,
            direction,
            request.Strategy,
            status,
            type,
            riskVerdict,
            primary,
            alternatives,
            entry,
            stop,
            targets,
            quantity,
            conditions,
            invalidations,
            abandonCriteria,
            checklist,
            rules,
            risks,
            traces,
            limitations.Take(_options.MaximumLimitations).ToArray(),
            warnings.Take(_options.MaximumWarnings).ToArray(),
            errors.Take(_options.MaximumErrors).ToArray(),
            summary,
            createdAtUtc,
            completedAtUtc,
            expiresAtUtc);

    private List<TradingPlanScenario> BuildAlternatives(TradingDecisionResult decision) =>
        decision.AlternativeScenarios
            .OrderBy(item => item.Source.Id, StringComparer.Ordinal)
            .ThenByDescending(item => item.SelectionScore)
            .ThenBy(item => item.SelectionReason, StringComparer.Ordinal)
            .Take(_options.MaximumAlternatives)
            .Select(item => new TradingPlanScenario(item, false))
            .ToList();

    private static TradingPlanScenario? BuildPrimaryScenario(TradingDecisionResult decision) =>
        decision.PrimaryScenario is null ? null : new TradingPlanScenario(decision.PrimaryScenario, true);

    private List<TradingPlanTarget> BuildTargets(TradingDecisionResult decision, ICollection<TradingPlanWarning> warnings)
    {
        var ordered = decision.Targets
            .OrderBy(item => item.Ordinal)
            .ThenBy(item => item.Price.Value)
            .ThenBy(item => item.Rationale, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length > _options.MaximumTargets)
        {
            warnings.Add(new TradingPlanWarning("TARGETS_TRUNCATED", "Targets were truncated at the configured plan limit."));
        }

        return ordered.Take(_options.MaximumTargets).Select(item => new TradingPlanTarget(item)).ToList();
    }

    private List<TradingPlanCondition> BuildConditions(TradingPlanScenario? primary, ICollection<TradingPlanWarning> warnings)
    {
        if (primary is null)
        {
            return [];
        }

        var conditions = primary.Source.Source.ActivationConditions
            .Select(condition => new TradingPlanCondition(TradingPlanConditionType.Activation, condition, $"scenario:{primary.Id}"))
            .Take(_options.MaximumConditions)
            .ToList();
        if (primary.Source.Source.ActivationConditions.Count > conditions.Count)
        {
            warnings.Add(new TradingPlanWarning("CONDITIONS_TRUNCATED", "Activation conditions were truncated at the configured plan limit."));
        }

        return conditions;
    }

    private List<TradingPlanInvalidation> BuildInvalidations(TradingDecisionResult decision, ICollection<TradingPlanWarning> warnings)
    {
        var ordered = decision.Invalidations
            .OrderBy(item => item.Source.Description, StringComparer.Ordinal)
            .ThenBy(item => item.Direction)
            .ThenBy(item => item.Rationale, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length > _options.MaximumInvalidations)
        {
            warnings.Add(new TradingPlanWarning("INVALIDATIONS_TRUNCATED", "Invalidations were truncated at the configured plan limit."));
        }

        return ordered.Take(_options.MaximumInvalidations).Select(item => new TradingPlanInvalidation(item)).ToList();
    }

    private List<TradingPlanAbandonCriterion> BuildAbandonCriteria(
        IReadOnlyCollection<TradingPlanInvalidation> invalidations,
        ICollection<TradingPlanWarning> warnings)
    {
        var criteria = invalidations
            .Select(item => new TradingPlanAbandonCriterion(item.Description, "decision-invalidation"))
            .Take(_options.MaximumAbandonCriteria)
            .ToList();
        if (invalidations.Count > criteria.Count)
        {
            warnings.Add(new TradingPlanWarning("ABANDON_CRITERIA_TRUNCATED", "Abandon criteria were truncated at the configured plan limit."));
        }

        return criteria;
    }

    private List<DeclarativeTradeManagementRule> BuildRules(TradingDecisionResult decision, bool directional)
    {
        var rules = new List<DeclarativeTradeManagementRule>();
        if (decision.PrimaryScenario is not null)
        {
            rules.AddRange(decision.PrimaryScenario.Source.ActivationConditions
                .Select(condition => new DeclarativeTradeManagementRule(
                    TradingPlanRuleType.WaitForActivation,
                    $"Do nothing before activation condition: {condition}",
                    $"scenario:{decision.PrimaryScenario.Source.Id}")));
        }

        if (directional)
        {
            rules.Add(new DeclarativeTradeManagementRule(TradingPlanRuleType.CancelOnInvalidation, "Abandon the plan when an explicit invalidation is reached.", "decision-invalidations"));
            rules.Add(new DeclarativeTradeManagementRule(TradingPlanRuleType.DoNotExceedApprovedQuantity, "Never exceed the exact quantity approved by the Risk Engine.", "risk-engine"));
            rules.Add(new DeclarativeTradeManagementRule(TradingPlanRuleType.DoNotWidenStop, "Do not widen the explicit stop.", "decision-stop"));
            rules.Add(new DeclarativeTradeManagementRule(TradingPlanRuleType.RespectTargets, "Respect the explicit target order from the decision.", "decision-targets"));
            rules.Add(new DeclarativeTradeManagementRule(TradingPlanRuleType.ReevaluateRiskOnEntryChange, "Re-evaluate risk if the entry changes beyond the configured tolerance.", "plan-options", _options.EntryChangeTolerance));
        }

        rules.Add(new DeclarativeTradeManagementRule(TradingPlanRuleType.ExpirePlan, "Do not use the plan after its expiration timestamp.", "plan-expiration"));
        rules.Add(new DeclarativeTradeManagementRule(TradingPlanRuleType.RebuildContextWhenStale, "Rebuild context when source data becomes stale.", "context-freshness"));
        return rules.Take(_options.MaximumRules).ToList();
    }

    private List<TradingPlanRisk> BuildRisks(TradingDecisionResult decision, ICollection<TradingPlanWarning> warnings)
    {
        var ordered = decision.Risks
            .OrderByDescending(item => item.Source.Severity)
            .ThenBy(item => item.Source.Description, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Rationale, StringComparer.Ordinal)
            .Select(item => new TradingPlanRisk(item))
            .ToList();
        var critical = ordered.Where(item => item.IsCritical).ToArray();
        var result = ordered.Take(_options.MaximumRisks).ToList();
        foreach (var item in critical)
        {
            if (!result.Contains(item))
            {
                result.Add(item);
            }
        }

        if (result.Count < ordered.Count)
        {
            warnings.Add(new TradingPlanWarning("RISKS_TRUNCATED", "Non-critical risks were truncated at the configured plan limit."));
        }

        return result;
    }

    private List<TradingPlanTraceReference> BuildTraces(
        TradingDecisionResult decision,
        RiskAssessmentResult risk,
        ICollection<TradingPlanWarning> warnings)
    {
        var traces = decision.Traces.Select(trace => new TradingPlanTraceReference(trace, "decision"))
            .Concat(risk.Traces.Select(trace => new TradingPlanTraceReference(trace, "risk-engine")))
            .OrderBy(item => item.Source.AgentRunId.Value, StringComparer.Ordinal)
            .ThenBy(item => item.Source.AgentId.Value, StringComparer.Ordinal)
            .ThenBy(item => item.Source.AgentVersion.ToString(), StringComparer.Ordinal)
            .ThenBy(item => item.Source.Reference.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Source.Reference.Value, StringComparer.Ordinal)
            .ThenBy(item => item.Origin, StringComparer.Ordinal)
            .GroupBy(item => string.Join("|", item.Source.AgentRunId.Value, item.Source.AgentId.Value, item.Source.AgentVersion, item.Source.Reference.Kind, item.Source.Reference.Value, item.Source.Role, item.Origin), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        if (traces.Count > _options.MaximumTraces)
        {
            warnings.Add(new TradingPlanWarning("TRACES_TRUNCATED", "Source traces were truncated at the configured plan limit."));
        }

        return traces.Take(_options.MaximumTraces).ToList();
    }

    private static PreTradeChecklist BuildChecklist(
        TradingDecisionResult decision,
        RiskAssessmentResult risk,
        bool candidate,
        bool contextFresh)
    {
        var directional = decision.Type is TradingDecisionType.LongSetup or TradingDecisionType.ShortSetup;
        var expectedDirection = decision.Type == TradingDecisionType.LongSetup ? MarketDirection.Long : MarketDirection.Short;
        var expectedBias = decision.Type == TradingDecisionType.LongSetup ? AgentDirectionalBias.Bullish : AgentDirectionalBias.Bearish;
        var riskApproved = IsRiskApproved(risk);
        var criticalRisksAcknowledged = !decision.Risks.Any(item => item.IsCritical);
        var noBlockingConflict = decision.Conflicts.All(item => item.Severity != ConsensusConflictSeverity.Critical);
        var invalidationPresent = !directional || decision.Invalidations.Any(item => item.IsCoherent && item.Direction == expectedBias);
        var constraintsValid = risk.Constraints.All(item => item.Kind is not (RiskConstraintKind.DailyLoss or RiskConstraintKind.WeeklyLoss) || item.Impact != RiskConstraintImpact.Rejected);

        return new PreTradeChecklist(
        [
            new(PreTradeChecklistItemType.DecisionValid, "decision-valid", "The decision completed with a supported schema.", decision.SchemaVersion == TradingDecisionResult.CurrentSchemaVersion && decision.Status is TradingDecisionStatus.Succeeded or TradingDecisionStatus.PartiallySucceeded, true, "decision"),
            new(PreTradeChecklistItemType.RiskApproved, "risk-approved", "The Risk Engine approves the supplied risk state.", riskApproved, true, "risk-engine"),
            new(PreTradeChecklistItemType.ContextFresh, "context-fresh", "The decision and risk sources are within the freshness window.", contextFresh, true, "context-policy"),
            new(PreTradeChecklistItemType.InstrumentCoherent, "instrument-coherent", "Decision and Risk Engine instruments match.", decision.Instrument == risk.Instrument, true, "cross-validation"),
            new(PreTradeChecklistItemType.DirectionCoherent, "direction-coherent", "The direction is coherent with the Risk Engine stop direction.", !directional || risk.StopDistance?.Direction == expectedDirection, true, "cross-validation"),
            new(PreTradeChecklistItemType.EntryPresent, "entry-present", "An explicit decision entry is present.", !directional || decision.Entry is not null, true, "decision"),
            new(PreTradeChecklistItemType.StopPresent, "stop-present", "An explicit decision stop is present.", !directional || decision.Stop is not null, true, "decision"),
            new(PreTradeChecklistItemType.TargetsPresent, "targets-present", "Targets satisfy the configured plan policy.", !directional || decision.Targets.Count > 0, true, "plan-options"),
            new(PreTradeChecklistItemType.QuantityFromRiskEngine, "quantity-from-risk-engine", "The quantity is copied from the Risk Engine.", !directional || risk.PositionSize is { FinalQuantity: > 0 }, true, "risk-engine"),
            new(PreTradeChecklistItemType.InvalidationPresent, "invalidation-present", "A coherent explicit invalidation is present.", invalidationPresent, true, "decision"),
            new(PreTradeChecklistItemType.DailyWeeklyConstraintsValid, "daily-weekly-valid", "Daily and weekly risk constraints are not breached.", constraintsValid, true, "risk-engine"),
            new(PreTradeChecklistItemType.CriticalRisksAcknowledged, "critical-risks-acknowledged", "Critical risks require explicit user acknowledgement.", criticalRisksAcknowledged, true, "decision-risks"),
            new(PreTradeChecklistItemType.NoBlockingConflict, "no-blocking-conflict", "No critical decision conflict is present.", noBlockingConflict, true, "decision-conflicts"),
            new(PreTradeChecklistItemType.UserConfirmationRequired, "user-confirmation-required", "User confirmation is required before any future execution.", false, true, "plan-policy")
        ]);
    }

    private static bool IsRiskApproved(RiskAssessmentResult risk) =>
        risk.Status is RiskAssessmentStatus.Succeeded or RiskAssessmentStatus.PartiallySucceeded
        && risk.Verdict is RiskVerdict.Approved or RiskVerdict.Reduced;

    private static TradingPlanDirection DirectionFor(TradingDecisionType type) => type switch
    {
        TradingDecisionType.LongSetup => TradingPlanDirection.Long,
        TradingDecisionType.ShortSetup => TradingPlanDirection.Short,
        _ => TradingPlanDirection.None
    };

    private static TradingPlanType TypeForDecision(TradingDecisionType type) => type switch
    {
        TradingDecisionType.Wait => TradingPlanType.WaitPlan,
        TradingDecisionType.Monitor => TradingPlanType.MonitorPlan,
        TradingDecisionType.Conflicted => TradingPlanType.ConflictedPlan,
        TradingDecisionType.InsufficientData => TradingPlanType.InsufficientDataPlan,
        _ => TradingPlanType.NoTradePlan
    };

    private static TradingPlanType TypeForEligibility(TradingPlanRequest request, TradingPlanEligibilityCode? code) =>
        request.Decision.Type == TradingDecisionType.InsufficientData || code == TradingPlanEligibilityCode.DecisionNotEligible
            ? TradingPlanType.InsufficientDataPlan
            : TradingPlanType.NoTradePlan;

    private static TradingPlanStatus StatusForEligibility(TradingPlanRequest request, TradingPlanEligibilityCode? code) =>
        request.Decision.Type == TradingDecisionType.InsufficientData || code == TradingPlanEligibilityCode.DecisionNotEligible
            ? TradingPlanStatus.InsufficientData
            : TradingPlanStatus.Rejected;

    private static string BuildSummary(
        TradingPlanType type,
        TradingPlanDirection direction,
        TradingDecisionResult decision,
        int targetCount,
        TradingPlanQuantity? quantity) =>
        $"{type} for {decision.Instrument.Symbol} {decision.Timeframe.Code}; direction={direction}; targets={targetCount}; quantity-source={(quantity is null ? "none" : "risk-engine")}.";

    private static IReadOnlyCollection<TradingPlanLimitation> BaseLimitations() =>
    [
        new("NO_EXECUTION", "This plan is declarative and never places, modifies or manages an order."),
        new("NO_RECALCULATION", "The generator does not recalculate consensus, confidence, risk, sizing, prices, currency conversion or expected return."),
        new("USER_CONFIRMATION", "A future execution workflow would require explicit user confirmation and a fresh validation.")
    ];
}
