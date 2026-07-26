using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.AI.RiskEngine.Domain;
using TradeMind.AI.TradingDecisions.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.RiskEngine.Application;

public interface IRiskEngine
{
    Task<RiskAssessmentResult> AssessAsync(
        RiskAssessmentRequest request,
        CancellationToken cancellationToken);
}

public sealed class RiskEngine(
    IRiskEligibilityPolicy eligibilityPolicy,
    IPositionSizingPolicy positionSizingPolicy,
    IRiskReductionPolicy riskReductionPolicy,
    IOptions<RiskEngineOptions> options,
    TimeProvider timeProvider,
    ILogger<RiskEngine> logger) : IRiskEngine
{
    private readonly RiskEngineOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly RiskBudgetCalculator _budgetCalculator = new();

    public async Task<RiskAssessmentResult> AssessAsync(
        RiskAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var createdAt = timeProvider.GetUtcNow();
        using var timeoutSource = new CancellationTokenSource(request.Timeout ?? _options.Timeout, timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            return await AssessCoreAsync(request, createdAt, linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Risk assessment {AssessmentId} was cancelled by the caller.", request.AssessmentId);
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            logger.LogWarning("Risk assessment {AssessmentId} exceeded its timeout.", request.AssessmentId);
            return Failure(request, createdAt, timeProvider.GetUtcNow(), RiskAssessmentStatus.TimedOut, "RISK_TIMEOUT", "The risk assessment exceeded its configured timeout.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Risk assessment {AssessmentId} failed unexpectedly.", request.AssessmentId);
            return Failure(request, createdAt, timeProvider.GetUtcNow(), RiskAssessmentStatus.Failed, "RISK_FAILURE", "The risk assessment failed unexpectedly.");
        }
    }

    private async Task<RiskAssessmentResult> AssessCoreAsync(
        RiskAssessmentRequest request,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var eligibility = await eligibilityPolicy.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!eligibility.Eligible)
        {
            return Rejected(request, createdAt, eligibility.Code ?? RiskRejectionCode.Unknown, eligibility.Message ?? "The risk assessment request is not eligible.");
        }

        var decision = request.Decision;
        var entry = decision.Entry!.Price;
        var stop = decision.Stop!.Price;
        var direction = decision.Type == TradingDecisionType.LongSetup ? MarketDirection.Long : MarketDirection.Short;
        if ((direction == MarketDirection.Long && stop.Value >= entry.Value)
            || (direction == MarketDirection.Short && stop.Value <= entry.Value))
        {
            return Rejected(request, createdAt, RiskRejectionCode.InvalidStop, "The explicit stop is incoherent with the decision direction.");
        }

        var distance = decimal.Abs(entry.Value - stop.Value);
        var stopDistance = new StopDistance(
            entry,
            stop,
            distance,
            request.Instrument.TickSize,
            distance / request.Instrument.TickSize,
            direction);
        var budget = _budgetCalculator.Calculate(request);
        var drawdown = BuildDrawdown(request);
        var exposure = BuildExposure(request, null);
        var constraints = budget.Evaluations.ToList();
        AddExposureConstraint(request, constraints);
        var warnings = new List<RiskWarning>();
        var errors = new List<RiskError>();
        var limitations = BaseLimitations();

        if (request.Portfolio is null)
        {
            warnings.Add(new RiskWarning("PORTFOLIO_CONTEXT_UNAVAILABLE", "Portfolio exposure was not evaluated because no portfolio context was supplied."));
        }

        if (!budget.IsAvailable)
        {
            return Materialize(
                request,
                createdAt,
                RiskAssessmentStatus.Rejected,
                RiskVerdict.Rejected,
                budget,
                stopDistance,
                null,
                [],
                drawdown,
                exposure,
                constraints,
                warnings,
                [new RiskError((budget.LimitingConstraint == RiskConstraintKind.Additional ? RiskRejectionCode.RiskBudgetUnavailable : RiskRejectionCode.ConstraintBreached).ToString(), "No positive effective risk budget remains.", true)],
                limitations);
        }

        var rMultiples = BuildRMultiples(request, entry, stop, direction, warnings);
        if (rMultiples.Any(item => !item.MeetsMinimum))
        {
            return Materialize(
                request,
                createdAt,
                RiskAssessmentStatus.Rejected,
                RiskVerdict.Rejected,
                budget,
                stopDistance,
                null,
                rMultiples,
                drawdown,
                exposure,
                constraints,
                warnings,
                [new RiskError(RiskRejectionCode.InvalidSizing.ToString(), "At least one explicit target does not meet the configured minimum R multiple.", true)],
                limitations);
        }

        var sizing = await positionSizingPolicy.CalculateAsync(request, budget, stopDistance, cancellationToken).ConfigureAwait(false);
        if (!sizing.Accepted || sizing.Proposal is null)
        {
            return Materialize(
                request,
                createdAt,
                RiskAssessmentStatus.Rejected,
                RiskVerdict.Rejected,
                budget,
                stopDistance,
                null,
                rMultiples,
                drawdown,
                exposure,
                constraints,
                warnings,
                [new RiskError((sizing.Code ?? RiskRejectionCode.InvalidSizing).ToString(), sizing.Message ?? "Position sizing was rejected.", true)],
                limitations);
        }

        var reduction = await riskReductionPolicy.EvaluateAsync(request, sizing.Proposal, cancellationToken).ConfigureAwait(false);
        if (!reduction.Accepted)
        {
            return Materialize(
                request,
                createdAt,
                RiskAssessmentStatus.Rejected,
                RiskVerdict.Rejected,
                budget,
                stopDistance,
                null,
                rMultiples,
                drawdown,
                exposure,
                constraints,
                warnings,
                [new RiskError((reduction.Code ?? RiskRejectionCode.InvalidSizing).ToString(), reduction.Message ?? "Risk reduction rejected the proposal.", true)],
                limitations);
        }

        var proposal = ApplyReduction(request, sizing.Proposal, reduction);
        if (reduction.Reduced || budget.IsReduced || proposal.Reduced)
        {
            warnings.Add(new RiskWarning("RISK_REDUCED", "The position size was reduced to satisfy the most restrictive applicable risk constraint."));
        }

        exposure = BuildExposure(request, proposal.ActualExposure);
        if (exposure is { Available: true, WithinLimit: false })
        {
            return Materialize(
                request,
                createdAt,
                RiskAssessmentStatus.Rejected,
                RiskVerdict.Rejected,
                budget,
                stopDistance,
                null,
                rMultiples,
                drawdown,
                exposure,
                constraints,
                warnings,
                [new RiskError(RiskRejectionCode.ExposureLimitReached.ToString(), "The proposed exposure exceeds the portfolio exposure limit.", true)],
                limitations);
        }

        return Materialize(
            request,
            createdAt,
            decision.Status == TradingDecisionStatus.PartiallySucceeded ? RiskAssessmentStatus.PartiallySucceeded : RiskAssessmentStatus.Succeeded,
            proposal.Reduced || budget.IsReduced || reduction.Reduced ? RiskVerdict.Reduced : RiskVerdict.Approved,
            budget,
            stopDistance,
            proposal,
            rMultiples,
            drawdown,
            exposure,
            constraints,
            warnings,
            errors,
            limitations);
    }

    private PositionSizeProposal ApplyReduction(
        RiskAssessmentRequest request,
        PositionSizeProposal proposal,
        RiskReductionDecision reduction)
    {
        var quantity = reduction.FinalQuantity;
        if (quantity == proposal.FinalQuantity && !reduction.Reduced)
        {
            return proposal;
        }

        var reasons = proposal.ReductionReasons.Concat(reduction.Reasons).Distinct(StringComparer.Ordinal).ToArray();
        var actualRisk = proposal.RiskPerQuantityUnit.Multiply(quantity);
        var actualExposure = request.Instrument.ExposurePerQuantity is { } exposure
            ? (Money?)exposure.Multiply(quantity)
            : null;
        return new PositionSizeProposal(
            proposal.RawQuantity,
            proposal.RoundedQuantity,
            quantity,
            proposal.RiskPerQuantityUnit,
            actualRisk,
            actualExposure,
            proposal.QuantityStep,
            proposal.MinimumQuantity,
            proposal.MaximumQuantity,
            true,
            reasons,
            proposal.Formula);
    }

    private List<RMultipleAssessment> BuildRMultiples(
        RiskAssessmentRequest request,
        Price entry,
        Price stop,
        MarketDirection direction,
        ICollection<RiskWarning> warnings)
    {
        var targets = request.Decision.Targets
            .OrderBy(target => target.Price.Value)
            .ThenBy(target => target.Ordinal)
            .ToArray();
        var result = new List<RMultipleAssessment>();
        foreach (var target in targets)
        {
            var coherent = direction == MarketDirection.Long ? target.Price.Value > entry.Value : target.Price.Value < entry.Value;
            var value = decimal.Abs(target.Price.Value - entry.Value) / decimal.Abs(entry.Value - stop.Value);
            result.Add(new RMultipleAssessment(
                entry,
                stop,
                target.Price,
                coherent ? value : 0,
                request.RiskProfile.MinimumRMultiple,
                coherent && value >= request.RiskProfile.MinimumRMultiple,
                "abs(Target - Entry) / abs(Entry - Stop)"));
        }

        if (result.Count == 0)
        {
            warnings.Add(new RiskWarning("R_MULTIPLE_UNAVAILABLE", "No explicit target was supplied; no R multiple was calculated."));
        }
        else if (result.Count > _options.MaximumRMultiples)
        {
            warnings.Add(new RiskWarning("R_MULTIPLES_TRUNCATED", "R multiple assessments were truncated at the configured limit."));
            return result.Take(_options.MaximumRMultiples).ToList();
        }

        return result;
    }

    private static DrawdownAssessment BuildDrawdown(RiskAssessmentRequest request)
    {
        var current = request.Account.CurrentDrawdown;
        var limit = request.RiskProfile.MaximumDrawdown;
        var remaining = limit is { } maximum
            ? (Money?)new Money(Math.Max(0, maximum.Amount - current.Amount), current.Currency)
            : null;
        return new DrawdownAssessment(current, limit, remaining, limit is { } cap && current.Amount >= cap.Amount);
    }

    private static ExposureAssessment? BuildExposure(RiskAssessmentRequest request, Money? proposed)
    {
        if (request.Portfolio is not { } portfolio)
        {
            return null;
        }

        var limit = portfolio.MaximumExposure;
        var remaining = limit is { } maximum
            ? (Money?)new Money(Math.Max(0, maximum.Amount - portfolio.CurrentExposure.Amount), portfolio.Currency)
            : null;
        var within = limit is null || proposed is null || proposed.Value.Amount <= remaining!.Value.Amount;
        return new ExposureAssessment(portfolio.CurrentExposure, limit, remaining, proposed, within, limit is not null);
    }

    private static void AddExposureConstraint(
        RiskAssessmentRequest request,
        ICollection<RiskConstraintEvaluation> constraints)
    {
        if (request.Portfolio?.MaximumExposure is not { } limit)
        {
            return;
        }

        var remaining = limit.Amount - request.Portfolio.CurrentExposure.Amount;
        constraints.Add(new RiskConstraintEvaluation(
            RiskConstraintKind.PortfolioExposure,
            limit.Amount,
            request.Portfolio.CurrentExposure.Amount,
            remaining,
            RiskUnit.Money,
            limit.Currency,
            "portfolio.maximum-exposure",
            true,
            true,
            remaining <= 0 ? RiskConstraintImpact.Rejected : RiskConstraintImpact.NotBinding));
    }

    private RiskAssessmentResult Rejected(
        RiskAssessmentRequest request,
        DateTimeOffset createdAt,
        RiskRejectionCode code,
        string message)
    {
        var status = request.Decision.Type == TradingDecisionType.InsufficientData
            || request.Decision.Status == TradingDecisionStatus.InsufficientData
            ? RiskAssessmentStatus.InsufficientData
            : RiskAssessmentStatus.Rejected;
        var verdict = status == RiskAssessmentStatus.InsufficientData
            ? RiskVerdict.InsufficientData
            : code == RiskRejectionCode.DecisionNotDirectional ? RiskVerdict.NoTrade : RiskVerdict.Rejected;
        return Materialize(
            request,
            createdAt,
            status,
            verdict,
            null,
            null,
            null,
            [],
            BuildDrawdown(request),
            BuildExposure(request, null),
            [],
            [],
            [new RiskError(code.ToString(), message, true)],
            BaseLimitations());
    }

    private RiskAssessmentResult Failure(
        RiskAssessmentRequest request,
        DateTimeOffset createdAt,
        DateTimeOffset completedAt,
        RiskAssessmentStatus status,
        string code,
        string message)
    {
        return Materialize(
            request,
            createdAt,
            status,
            RiskVerdict.Rejected,
            null,
            null,
            null,
            [],
            null,
            null,
            [],
            [],
            [new RiskError(code, message, true)],
            BaseLimitations(),
            completedAt);
    }

    private RiskAssessmentResult Materialize(
        RiskAssessmentRequest request,
        DateTimeOffset createdAt,
        RiskAssessmentStatus status,
        RiskVerdict verdict,
        EffectiveRiskBudget? budget,
        StopDistance? stopDistance,
        PositionSizeProposal? positionSize,
        IReadOnlyCollection<RMultipleAssessment> rMultiples,
        DrawdownAssessment? drawdown,
        ExposureAssessment? exposure,
        IReadOnlyCollection<RiskConstraintEvaluation> constraints,
        IReadOnlyCollection<RiskWarning> warnings,
        IReadOnlyCollection<RiskError> errors,
        IReadOnlyCollection<string> limitations,
        DateTimeOffset? completedAt = null)
    {
        var completed = completedAt ?? timeProvider.GetUtcNow();
        var decision = request.Decision;
        return new RiskAssessmentResult(
            request.AssessmentId,
            request.DecisionId,
            decision.MarketContextId,
            request.Instrument.Instrument,
            decision.Timeframe,
            request.Strategy,
            status,
            verdict,
            budget,
            stopDistance,
            positionSize,
            rMultiples,
            drawdown,
            exposure,
            Truncate(constraints, _options.MaximumConstraints),
            TruncateCriticalRisks(decision.Risks, _options.MaximumRisks),
            Truncate(decision.Traces, _options.MaximumTraces),
            limitations.Take(_options.MaximumLimitations).ToArray(),
            warnings.Take(_options.MaximumWarnings).ToArray(),
            errors,
            createdAt,
            completed);
    }

    private static IReadOnlyCollection<DecisionRisk> TruncateCriticalRisks(IReadOnlyCollection<DecisionRisk> source, int maximum)
    {
        var ordered = source
            .OrderByDescending(risk => risk.Source.Severity)
            .ThenBy(risk => risk.Source.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var result = ordered.Take(maximum).ToList();
        foreach (var critical in ordered.Where(risk => risk.IsCritical))
        {
            if (!result.Contains(critical))
            {
                result.Add(critical);
            }
        }

        return result;
    }

    private static IReadOnlyCollection<T> Truncate<T>(IReadOnlyCollection<T> source, int maximum) => source.Take(maximum).ToArray();

    private static IReadOnlyCollection<string> BaseLimitations() =>
    [
        "This assessment is a risk calculation only; it is not an order, execution instruction or promise of gain.",
        "The engine does not infer broker metadata, tick values, currency conversion, probability or expected return."
    ];
}
