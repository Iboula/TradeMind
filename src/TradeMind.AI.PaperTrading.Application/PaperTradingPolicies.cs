using TradeMind.AI.PaperTrading.Domain;
using TradeMind.AI.TradingPlans.Domain;
using TradeMind.AI.TradingWorkspace.Domain;

namespace TradeMind.AI.PaperTrading.Application;

public sealed record PaperTradingEligibilityDecision
{
    public PaperTradingEligibilityDecision(
        bool canSimulate,
        bool isNoTrade,
        bool isExpired,
        IReadOnlyCollection<PaperTradingWarning> warnings,
        IReadOnlyCollection<PaperTradingError> errors)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(errors);
        CanSimulate = canSimulate;
        IsNoTrade = isNoTrade;
        IsExpired = isExpired;
        Warnings = Array.AsReadOnly(warnings.ToArray());
        Errors = Array.AsReadOnly(errors.ToArray());
    }

    public bool CanSimulate { get; }
    public bool IsNoTrade { get; }
    public bool IsExpired { get; }
    public IReadOnlyList<PaperTradingWarning> Warnings { get; }
    public IReadOnlyList<PaperTradingError> Errors { get; }
}

public interface IPaperTradingEligibilityPolicy
{
    PaperTradingEligibilityDecision Evaluate(PaperTradingRequest request, DateTimeOffset evaluatedAtUtc);
}

public sealed class DefaultPaperTradingEligibilityPolicy : IPaperTradingEligibilityPolicy
{
    public PaperTradingEligibilityDecision Evaluate(PaperTradingRequest request, DateTimeOffset evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        var warnings = new List<PaperTradingWarning>();
        var errors = new List<PaperTradingError>();
        var workspace = request.Workspace;
        var plan = request.Plan;

        if (request.Version != PaperTradingRequest.CurrentVersion)
        {
            errors.Add(new("REQUEST_VERSION_UNSUPPORTED", "The paper trading request version is not supported."));
        }

        if (workspace.SchemaVersion != TradingWorkspaceResult.CurrentSchemaVersion)
        {
            errors.Add(new("WORKSPACE_VERSION_UNSUPPORTED", "The workspace schema version is not supported."));
        }

        if (plan.SchemaVersion != TradingPlanResult.CurrentSchemaVersion)
        {
            errors.Add(new("PLAN_VERSION_UNSUPPORTED", "The trading plan schema version is not supported."));
        }

        if (workspace.Plan.PlanId != plan.PlanId)
        {
            errors.Add(new("PLAN_ID_MISMATCH", "The workspace and plan identifiers do not match."));
        }

        if (workspace.MarketContextId != plan.MarketContextId)
        {
            errors.Add(new("CONTEXT_ID_MISMATCH", "The workspace and plan context identifiers do not match."));
        }

        if (workspace.State != TradingWorkspaceState.PlanReady)
        {
            errors.Add(new("WORKSPACE_NOT_PLAN_READY", "The workspace is not ready for paper simulation."));
        }

        if (workspace.Status is not (TradingWorkspaceStatus.Succeeded or TradingWorkspaceStatus.PartiallySucceeded))
        {
            errors.Add(new("WORKSPACE_STATUS_INELIGIBLE", "The workspace did not complete successfully."));
        }

        if (plan.Status is TradingPlanStatus.Expired or TradingPlanStatus.TimedOut or TradingPlanStatus.Cancelled)
        {
            return Decision(false, false, true, warnings, [new("PLAN_EXPIRED", "The trading plan is expired or no longer active.")], errors);
        }

        if (plan.ExpiresAtUtc <= evaluatedAtUtc)
        {
            return Decision(false, false, true, warnings, [new("PLAN_EXPIRED", "The trading plan has passed its expiration time.")], errors);
        }

        if (plan.Type is not TradingPlanType.ExecutableCandidate)
        {
            warnings.Add(new("NO_TRADE_PLAN", "The plan does not contain a directional executable candidate."));
            return Decision(false, true, false, warnings, [], errors);
        }

        if (plan.Status is not (TradingPlanStatus.Succeeded or TradingPlanStatus.PartiallySucceeded))
        {
            errors.Add(new("PLAN_STATUS_INELIGIBLE", "The trading plan is not in a successful state."));
        }

        if (plan.RiskOutcome is not (TradingPlanRiskOutcome.Approved or TradingPlanRiskOutcome.ApprovedWithReduction))
        {
            errors.Add(new("PLAN_RISK_NOT_APPROVED", "The plan risk outcome does not authorize simulation."));
        }

        if (plan.Direction is not (TradingPlanDirection.Long or TradingPlanDirection.Short))
        {
            errors.Add(new("DIRECTION_MISSING", "A paper position requires a directional plan."));
        }

        if (plan.Entry is null)
        {
            errors.Add(new("ENTRY_MISSING", "The plan does not provide an entry level."));
        }

        if (plan.Stop is null)
        {
            errors.Add(new("STOP_MISSING", "The plan does not provide a stop level."));
        }

        if (plan.Quantity is null || plan.Quantity.FinalQuantity <= 0)
        {
            errors.Add(new("QUANTITY_MISSING", "The plan does not provide a positive approved quantity."));
        }

        if (plan.Targets.Count == 0)
        {
            errors.Add(new("TARGETS_MISSING", "The plan does not provide a target level."));
        }

        if (plan.Invalidations.Count == 0 || plan.Invalidations.Any(invalidation => !invalidation.IsCoherent))
        {
            errors.Add(new("INVALIDATION_MISSING", "The plan does not provide a coherent invalidation."));
        }

        if (plan.Entry is not null && plan.Stop is not null && plan.Targets.Count > 0)
        {
            var entry = plan.Entry.Price.Value;
            var stop = plan.Stop.Price.Value;
            var target = plan.Targets.OrderBy(item => item.Ordinal).ThenBy(item => item.Price.Value).First().Price.Value;
            var coherent = plan.Direction switch
            {
                TradingPlanDirection.Long => stop < entry && target > entry,
                TradingPlanDirection.Short => stop > entry && target < entry,
                _ => false
            };
            if (!coherent)
            {
                errors.Add(new("LEVELS_INCOHERENT", "Entry, stop and target levels are not coherent with direction."));
            }
        }

        if (workspace.Plan.Direction != plan.Direction || workspace.Plan.Type != plan.Type)
        {
            errors.Add(new("WORKSPACE_PLAN_MISMATCH", "The workspace plan summary disagrees with the supplied plan."));
        }

        return Decision(errors.Count == 0, false, false, warnings, [], errors);
    }

    private static PaperTradingEligibilityDecision Decision(
        bool canSimulate,
        bool isNoTrade,
        bool isExpired,
        IReadOnlyCollection<PaperTradingWarning> warnings,
        IReadOnlyCollection<PaperTradingError> directErrors,
        IReadOnlyCollection<PaperTradingError> accumulatedErrors)
    {
        var errors = accumulatedErrors.Concat(directErrors).ToArray();
        return new PaperTradingEligibilityDecision(canSimulate && errors.Length == 0, isNoTrade, isExpired, warnings, errors);
    }
}
