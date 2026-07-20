using Microsoft.Extensions.Options;
using TradeMind.AI.ExpertAgents.Consensus.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.AI.RiskEngine.Domain;
using TradeMind.AI.TradingPlans.Domain;
using TradeMind.AI.TradingDecisions.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.TradingPlans.Application;

public sealed record TradingPlanEligibilityDecision
{
    private TradingPlanEligibilityDecision(bool eligible, TradingPlanEligibilityCode? code, string? message)
    {
        Eligible = eligible;
        Code = code;
        Message = message;
    }

    public bool Eligible { get; }
    public TradingPlanEligibilityCode? Code { get; }
    public string? Message { get; }

    public static TradingPlanEligibilityDecision Include() => new(true, null, null);

    public static TradingPlanEligibilityDecision Reject(TradingPlanEligibilityCode code, string message) => new(false, code, message);
}

public interface ITradingPlanEligibilityPolicy
{
    ValueTask<TradingPlanEligibilityDecision> EvaluateAsync(
        TradingPlanRequest request,
        CancellationToken cancellationToken);
}

public sealed class DefaultTradingPlanEligibilityPolicy(
    IOptions<TradingPlanOptions> options) : ITradingPlanEligibilityPolicy
{
    private readonly TradingPlanOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public ValueTask<TradingPlanEligibilityDecision> EvaluateAsync(
        TradingPlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var decision = request.Decision;
        var risk = request.Risk;

        if (request.Version != TradingPlanRequest.CurrentVersion)
        {
            return Reject(TradingPlanEligibilityCode.UnsupportedRequestVersion, "The trading plan request version is not supported.");
        }

        if (decision.SchemaVersion != TradingDecisionResult.CurrentSchemaVersion)
        {
            return Reject(TradingPlanEligibilityCode.UnsupportedDecisionVersion, "The trading decision schema version is not supported.");
        }

        if (risk.SchemaVersion != RiskAssessmentResult.CurrentSchemaVersion)
        {
            return Reject(TradingPlanEligibilityCode.UnsupportedRiskVersion, "The risk assessment schema version is not supported.");
        }

        if (decision.DecisionId != request.DecisionId || decision.DecisionId != risk.DecisionId)
        {
            return Reject(TradingPlanEligibilityCode.DecisionIdMismatch, "The plan decision id does not match the decision and risk assessment.");
        }

        if (risk.AssessmentId != request.RiskAssessmentId)
        {
            return Reject(TradingPlanEligibilityCode.RiskAssessmentIdMismatch, "The plan risk assessment id does not match the supplied risk assessment.");
        }

        if (decision.MarketContextId != request.MarketContextId || risk.MarketContextId != request.MarketContextId)
        {
            return Reject(TradingPlanEligibilityCode.ContextMismatch, "The decision and risk assessment use different market contexts.");
        }

        if (decision.Instrument != risk.Instrument)
        {
            return Reject(TradingPlanEligibilityCode.InstrumentMismatch, "The decision and risk assessment instruments diverge.");
        }

        if (decision.Timeframe.Code != risk.Timeframe.Code)
        {
            return Reject(TradingPlanEligibilityCode.TimeframeMismatch, "The decision and risk assessment timeframes diverge.");
        }

        if (decision.Status is TradingDecisionStatus.Failed or TradingDecisionStatus.TimedOut or TradingDecisionStatus.Cancelled)
        {
            return Reject(TradingPlanEligibilityCode.DecisionNotEligible, "The trading decision did not complete successfully.");
        }

        if (decision.Type is not (TradingDecisionType.LongSetup or TradingDecisionType.ShortSetup))
        {
            return ValueTask.FromResult(TradingPlanEligibilityDecision.Include());
        }

        if (decision.PrimaryScenario is null)
        {
            return Reject(TradingPlanEligibilityCode.MissingScenario, "A directional plan requires the selected decision scenario.");
        }

        if (decision.Entry is null)
        {
            return Reject(TradingPlanEligibilityCode.MissingEntry, "A directional plan requires the explicit decision entry.");
        }

        if (decision.Stop is null)
        {
            return Reject(TradingPlanEligibilityCode.MissingStop, "A directional plan requires the explicit decision stop.");
        }

        if (_options.RequireTargets && decision.Targets.Count == 0)
        {
            return Reject(TradingPlanEligibilityCode.MissingTargets, "The configured plan policy requires at least one explicit target.");
        }

        if (decision.Entry.Instrument != decision.Instrument || decision.Stop.Instrument != decision.Instrument)
        {
            return Reject(TradingPlanEligibilityCode.InstrumentMismatch, "Decision entry and stop instruments must match the decision instrument.");
        }

        if (decision.Entry.Timeframe.Code != decision.Timeframe.Code || decision.Stop.Timeframe.Code != decision.Timeframe.Code)
        {
            return Reject(TradingPlanEligibilityCode.TimeframeMismatch, "Decision entry and stop timeframes must match the decision timeframe.");
        }

        if (decision.Targets.Any(target => target.Instrument != decision.Instrument))
        {
            return Reject(TradingPlanEligibilityCode.InstrumentMismatch, "Decision target instruments must match the decision instrument.");
        }

        if (decision.Targets.Any(target => target.Timeframe.Code != decision.Timeframe.Code))
        {
            return Reject(TradingPlanEligibilityCode.TimeframeMismatch, "Decision target timeframes must match the decision timeframe.");
        }

        var expectedDirection = decision.Type == TradingDecisionType.LongSetup ? AgentDirectionalBias.Bullish : AgentDirectionalBias.Bearish;
        if (!decision.Invalidations.Any(item => item.IsCoherent && item.Direction == expectedDirection))
        {
            return Reject(TradingPlanEligibilityCode.MissingInvalidation, "A directional plan requires a coherent explicit invalidation.");
        }

        if (risk.PositionSize is null || risk.PositionSize.FinalQuantity <= 0)
        {
            return Reject(TradingPlanEligibilityCode.MissingQuantity, "The plan quantity must come from a positive Risk Engine proposal.");
        }

        if (risk.StopDistance is null
            || risk.StopDistance.Entry.Value != decision.Entry.Price.Value
            || risk.StopDistance.Stop.Value != decision.Stop.Price.Value)
        {
            return Reject(TradingPlanEligibilityCode.DivergentLevels, "Risk Engine levels do not match the decision entry and stop.");
        }

        var expectedMarketDirection = decision.Type == TradingDecisionType.LongSetup ? MarketDirection.Long : MarketDirection.Short;
        if (risk.StopDistance.Direction != expectedMarketDirection)
        {
            return Reject(TradingPlanEligibilityCode.DivergentDirection, "Risk Engine direction does not match the decision direction.");
        }

        if (risk.Status is not (RiskAssessmentStatus.Succeeded or RiskAssessmentStatus.PartiallySucceeded))
        {
            return Reject(TradingPlanEligibilityCode.RiskNotEligible, "The risk assessment did not complete successfully.");
        }

        if (risk.Verdict is not (RiskVerdict.Approved or RiskVerdict.Reduced))
        {
            return Reject(TradingPlanEligibilityCode.RiskNotEligible, "The risk assessment does not approve a plan quantity.");
        }

        if (decision.Conflicts.Any(conflict => conflict.Severity == ConsensusConflictSeverity.Critical))
        {
            return Reject(TradingPlanEligibilityCode.BlockingConflict, "A critical decision conflict blocks plan generation.");
        }

        return ValueTask.FromResult(TradingPlanEligibilityDecision.Include());
    }

    private static ValueTask<TradingPlanEligibilityDecision> Reject(TradingPlanEligibilityCode code, string message) =>
        ValueTask.FromResult(TradingPlanEligibilityDecision.Reject(code, message));
}

public sealed record TradingPlanExpirationDecision
{
    public TradingPlanExpirationDecision(
        DateTimeOffset expiresAtUtc,
        DateTimeOffset oldestSourceAtUtc,
        bool expired,
        bool stale,
        string? reason)
    {
        ExpiresAtUtc = expiresAtUtc;
        OldestSourceAtUtc = oldestSourceAtUtc;
        Expired = expired;
        Stale = stale;
        Reason = reason;
    }

    public DateTimeOffset ExpiresAtUtc { get; }
    public DateTimeOffset OldestSourceAtUtc { get; }
    public bool Expired { get; }
    public bool Stale { get; }
    public string? Reason { get; }
}

public interface ITradingPlanExpirationPolicy
{
    ValueTask<TradingPlanExpirationDecision> EvaluateAsync(
        TradingPlanRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

public sealed class DefaultTradingPlanExpirationPolicy(
    IOptions<TradingPlanOptions> options) : ITradingPlanExpirationPolicy
{
    private readonly TradingPlanOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public ValueTask<TradingPlanExpirationDecision> EvaluateAsync(
        TradingPlanRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var expiresAt = request.ExpiresAtUtc ?? nowUtc.Add(_options.DefaultPlanLifetime);
        var oldestSource = request.Decision.CompletedAtUtc <= request.Risk.CompletedAtUtc
            ? request.Decision.CompletedAtUtc
            : request.Risk.CompletedAtUtc;
        var sourceAge = nowUtc - oldestSource;
        var stale = sourceAge > _options.MaximumSourceAge;
        var expired = nowUtc >= expiresAt;
        var reason = expired
            ? "The requested plan expiration has passed."
            : stale
                ? "The decision or risk source is older than the configured freshness window."
                : null;
        return ValueTask.FromResult(new TradingPlanExpirationDecision(expiresAt, oldestSource, expired, stale, reason));
    }
}
