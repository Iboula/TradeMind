using Microsoft.Extensions.Options;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.AI.RiskEngine.Domain;
using TradeMind.AI.TradingDecisions.Domain;

namespace TradeMind.AI.RiskEngine.Application;

public sealed record RiskEligibilityDecision
{
    private RiskEligibilityDecision(bool eligible, RiskRejectionCode? code, string? message)
    {
        Eligible = eligible;
        Code = code;
        Message = message;
    }

    public bool Eligible { get; }
    public RiskRejectionCode? Code { get; }
    public string? Message { get; }

    public static RiskEligibilityDecision Include() => new(true, null, null);

    public static RiskEligibilityDecision Reject(RiskRejectionCode code, string message) => new(false, code, message);
}

public interface IRiskEligibilityPolicy
{
    ValueTask<RiskEligibilityDecision> EvaluateAsync(
        RiskAssessmentRequest request,
        CancellationToken cancellationToken);
}

public sealed class DefaultRiskEligibilityPolicy(
    IOptions<RiskEngineOptions> options) : IRiskEligibilityPolicy
{
    private readonly RiskEngineOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public ValueTask<RiskEligibilityDecision> EvaluateAsync(
        RiskAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var decision = request.Decision;

        if (request.Version != RiskAssessmentRequest.CurrentVersion)
        {
            return Reject(RiskRejectionCode.UnsupportedRequestVersion, "The risk assessment request version is not supported.");
        }

        if (decision.SchemaVersion != TradingDecisionResult.CurrentSchemaVersion)
        {
            return Reject(RiskRejectionCode.UnsupportedDecisionVersion, "The trading decision schema version is not supported.");
        }

        if (decision.DecisionId != request.DecisionId)
        {
            return Reject(RiskRejectionCode.DecisionMismatch, "The request decision id does not match the supplied decision.");
        }

        if (decision.Status is not (TradingDecisionStatus.Succeeded or TradingDecisionStatus.PartiallySucceeded))
        {
            return Reject(RiskRejectionCode.DecisionStatusNotEligible, "The trading decision status cannot produce a risk assessment.");
        }

        if (decision.Type is not (TradingDecisionType.LongSetup or TradingDecisionType.ShortSetup))
        {
            return Reject(RiskRejectionCode.DecisionNotDirectional, "Only a directional setup can produce position sizing.");
        }

        if (decision.Entry is null)
        {
            return Reject(RiskRejectionCode.InvalidStop, "A directional decision must contain an explicit entry.");
        }

        if (decision.Stop is null)
        {
            return Reject(RiskRejectionCode.InvalidStop, "A directional decision must contain an explicit stop.");
        }

        if (decision.Instrument != request.Instrument.Instrument)
        {
            return Reject(RiskRejectionCode.InstrumentMismatch, "The decision instrument does not match the risk specification.");
        }

        if (decision.Timeframe.Code != decision.Entry.Timeframe.Code
            || decision.Timeframe.Code != decision.Stop.Timeframe.Code)
        {
            return Reject(RiskRejectionCode.TimeframeMismatch, "Decision levels must use the decision timeframe.");
        }

        if (request.RiskProfile.AccountCurrency != request.Account.Currency
            || request.RiskProfile.AccountCurrency != request.Instrument.TickValue.Currency)
        {
            return Reject(RiskRejectionCode.CurrencyMismatch, "Risk profile, account and tick value currencies must match.");
        }

        if (request.Portfolio is null
            && (request.RiskProfile.MaximumPortfolioRisk is not null
                || request.Portfolio?.MaximumExposure is not null))
        {
            return Reject(RiskRejectionCode.PortfolioContextRequired, "Portfolio context is required for the configured portfolio limits.");
        }

        if (request.RiskProfile.MaximumPortfolioRisk is not null
            && request.Portfolio is not null
            && request.Portfolio.Currency != request.RiskProfile.AccountCurrency)
        {
            return Reject(RiskRejectionCode.CurrencyMismatch, "Portfolio limits must use the account currency.");
        }

        return ValueTask.FromResult(RiskEligibilityDecision.Include());
    }

    private static ValueTask<RiskEligibilityDecision> Reject(RiskRejectionCode code, string message) =>
        ValueTask.FromResult(RiskEligibilityDecision.Reject(code, message));
}

public sealed record PositionSizingDecision
{
    private PositionSizingDecision(bool accepted, PositionSizeProposal? proposal, RiskRejectionCode? code, string? message)
    {
        Accepted = accepted;
        Proposal = proposal;
        Code = code;
        Message = message;
    }

    public bool Accepted { get; }
    public PositionSizeProposal? Proposal { get; }
    public RiskRejectionCode? Code { get; }
    public string? Message { get; }

    public static PositionSizingDecision Include(PositionSizeProposal proposal) => new(true, proposal, null, null);

    public static PositionSizingDecision Reject(RiskRejectionCode code, string message) => new(false, null, code, message);
}

public interface IPositionSizingPolicy
{
    ValueTask<PositionSizingDecision> CalculateAsync(
        RiskAssessmentRequest request,
        EffectiveRiskBudget budget,
        StopDistance stopDistance,
        CancellationToken cancellationToken);
}

public sealed class DefaultPositionSizingPolicy : IPositionSizingPolicy
{
    public ValueTask<PositionSizingDecision> CalculateAsync(
        RiskAssessmentRequest request,
        EffectiveRiskBudget budget,
        StopDistance stopDistance,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(stopDistance);
        cancellationToken.ThrowIfCancellationRequested();
        if (!budget.IsAvailable || budget.Amount.Amount <= 0)
        {
            return ValueTask.FromResult(PositionSizingDecision.Reject(
                RiskRejectionCode.RiskBudgetUnavailable,
                "No positive effective risk budget is available."));
        }

        var specification = request.Instrument;
        var riskPerUnitAmount = stopDistance.TickCount * specification.TickValue.Amount;
        if (riskPerUnitAmount <= 0)
        {
            return ValueTask.FromResult(PositionSizingDecision.Reject(
                RiskRejectionCode.InvalidSizing,
                "Risk per quantity unit must be positive."));
        }

        var riskPerUnit = new Money(riskPerUnitAmount, specification.TickValue.Currency);
        var rawQuantity = budget.Amount.Amount / riskPerUnitAmount;
        var roundedQuantity = FloorToStep(rawQuantity, specification.QuantityStep);
        if (roundedQuantity < specification.MinimumQuantity)
        {
            return ValueTask.FromResult(PositionSizingDecision.Reject(
                RiskRejectionCode.QuantityBelowMinimum,
                "The minimum quantity would exceed the effective risk budget."));
        }

        var finalQuantity = roundedQuantity;
        var reduced = false;
        var reductions = new List<string>();
        if (finalQuantity > specification.MaximumQuantity)
        {
            finalQuantity = FloorToStep(specification.MaximumQuantity, specification.QuantityStep);
            reduced = true;
            reductions.Add("maximum-quantity-limit");
        }

        if (finalQuantity < specification.MinimumQuantity)
        {
            return ValueTask.FromResult(PositionSizingDecision.Reject(
                RiskRejectionCode.QuantityBelowMinimum,
                "The quantity step and maximum quantity cannot satisfy the minimum quantity."));
        }

        var actualRisk = new Money(finalQuantity * riskPerUnitAmount, riskPerUnit.Currency);
        var actualExposure = specification.ExposurePerQuantity is { } exposure
            ? (Money?)new Money(finalQuantity * exposure.Amount, exposure.Currency)
            : null;
        var proposal = new PositionSizeProposal(
            rawQuantity,
            roundedQuantity,
            finalQuantity,
            riskPerUnit,
            actualRisk,
            actualExposure,
            specification.QuantityStep,
            specification.MinimumQuantity,
            specification.MaximumQuantity,
            reduced,
            reductions);
        return ValueTask.FromResult(PositionSizingDecision.Include(proposal));
    }

    private static decimal FloorToStep(decimal value, decimal step) => decimal.Floor(value / step) * step;
}

public sealed record RiskReductionDecision
{
    private RiskReductionDecision(bool accepted, decimal finalQuantity, bool reduced, IReadOnlyCollection<string> reasons, RiskRejectionCode? code, string? message)
    {
        Accepted = accepted;
        FinalQuantity = finalQuantity;
        Reduced = reduced;
        Reasons = Array.AsReadOnly(reasons.ToArray());
        Code = code;
        Message = message;
    }

    public bool Accepted { get; }
    public decimal FinalQuantity { get; }
    public bool Reduced { get; }
    public IReadOnlyList<string> Reasons { get; }
    public RiskRejectionCode? Code { get; }
    public string? Message { get; }

    public static RiskReductionDecision Keep(decimal quantity) => new(true, quantity, false, [], null, null);

    public static RiskReductionDecision Reduce(decimal quantity, IReadOnlyCollection<string> reasons) => new(true, quantity, true, reasons, null, null);

    public static RiskReductionDecision Reject(RiskRejectionCode code, string message) => new(false, 0, false, [], code, message);
}

public interface IRiskReductionPolicy
{
    ValueTask<RiskReductionDecision> EvaluateAsync(
        RiskAssessmentRequest request,
        PositionSizeProposal proposal,
        CancellationToken cancellationToken);
}

public sealed class DefaultRiskReductionPolicy : IRiskReductionPolicy
{
    public ValueTask<RiskReductionDecision> EvaluateAsync(
        RiskAssessmentRequest request,
        PositionSizeProposal proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(proposal);
        cancellationToken.ThrowIfCancellationRequested();
        var portfolio = request.Portfolio;
        if (portfolio?.MaximumExposure is null)
        {
            return ValueTask.FromResult(RiskReductionDecision.Keep(proposal.FinalQuantity));
        }

        if (request.Instrument.ExposurePerQuantity is null)
        {
            return ValueTask.FromResult(RiskReductionDecision.Reject(
                RiskRejectionCode.IncompleteInstrumentSpecification,
                "Exposure per quantity is required to evaluate the portfolio exposure limit."));
        }

        var remaining = portfolio.MaximumExposure.Value.Amount - portfolio.CurrentExposure.Amount;
        if (remaining <= 0)
        {
            return ValueTask.FromResult(RiskReductionDecision.Reject(
                RiskRejectionCode.ExposureLimitReached,
                "Portfolio exposure capacity is zero or negative."));
        }

        var exposurePerQuantity = request.Instrument.ExposurePerQuantity.Value.Amount;
        var maximumByExposure = FloorToStep(
            remaining / exposurePerQuantity,
            request.Instrument.QuantityStep);
        if (maximumByExposure < request.Instrument.MinimumQuantity)
        {
            return ValueTask.FromResult(RiskReductionDecision.Reject(
                RiskRejectionCode.ExposureLimitReached,
                "The minimum quantity exceeds the remaining portfolio exposure capacity."));
        }

        if (maximumByExposure < proposal.FinalQuantity)
        {
            return ValueTask.FromResult(RiskReductionDecision.Reduce(
                maximumByExposure,
                ["portfolio-exposure-limit"]));
        }

        return ValueTask.FromResult(RiskReductionDecision.Keep(proposal.FinalQuantity));
    }

    private static decimal FloorToStep(decimal value, decimal step) => decimal.Floor(value / step) * step;
}
