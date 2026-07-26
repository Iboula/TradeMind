using System.Globalization;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Consensus.Domain;
using TradeMind.AI.TradingDecisions.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.RiskEngine.Domain;

public sealed record RiskAssessmentId
{
    public RiskAssessmentId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Risk assessment id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static RiskAssessmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record CurrencyCode
{
    public CurrencyCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException("Currency code must contain exactly three ASCII letters.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct Money
{
    public Money(decimal amount, CurrencyCode currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }
    public CurrencyCode Currency { get; }

    public Money RequireNonNegative(string parameterName)
    {
        if (Amount < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Money amount cannot be negative.");
        }

        return this;
    }

    public Money Subtract(Money other)
    {
        EnsureCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    public void EnsureCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other.Currency);
        if (Currency != other.Currency)
        {
            throw new ArgumentException("Money values must use the same currency.", nameof(other));
        }
    }

    public override string ToString() => $"{Amount.ToString(CultureInfo.InvariantCulture)} {Currency.Value}";
}

public enum RiskAssessmentStatus
{
    Succeeded,
    PartiallySucceeded,
    Rejected,
    InsufficientData,
    Failed,
    TimedOut,
    Cancelled
}

public enum RiskRejectionCode
{
    UnsupportedRequestVersion,
    UnsupportedDecisionVersion,
    DecisionMismatch,
    DecisionStatusNotEligible,
    DecisionNotDirectional,
    InstrumentMismatch,
    TimeframeMismatch,
    CurrencyMismatch,
    PortfolioContextRequired,
    RiskBudgetUnavailable,
    ConstraintBreached,
    InvalidStop,
    InvalidSizing,
    QuantityBelowMinimum,
    ExposureLimitReached,
    IncompleteInstrumentSpecification,
    InvalidRequest,
    Unknown
}

public enum RiskVerdict
{
    Approved,
    Reduced,
    Rejected,
    InsufficientData,
    NoTrade
}

public enum RiskStrategy
{
    Conservative,
    Strict,
    Balanced
}

public enum RiskUnit
{
    Money,
    Percent,
    Quantity
}

public enum RiskConstraintKind
{
    RiskPercentPerTrade,
    RiskAmountPerTrade,
    DailyLoss,
    WeeklyLoss,
    Drawdown,
    PortfolioRisk,
    PortfolioExposure,
    AccountLimit,
    PlatformLimit,
    Additional
}

public enum RiskConstraintImpact
{
    NotBinding,
    Binding,
    Reduced,
    Rejected
}

public sealed record RiskLimit
{
    public RiskLimit(
        RiskConstraintKind kind,
        Money limit,
        Money consumed,
        string source,
        bool isHard = true,
        bool isReducible = true)
    {
        limit.RequireNonNegative(nameof(limit));
        consumed.RequireNonNegative(nameof(consumed));
        limit.EnsureCurrency(consumed);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        Kind = kind;
        Limit = limit;
        Consumed = consumed;
        Source = source.Trim();
        IsHard = isHard;
        IsReducible = isReducible;
    }

    public RiskConstraintKind Kind { get; }
    public Money Limit { get; }
    public Money Consumed { get; }
    public string Source { get; }
    public bool IsHard { get; }
    public bool IsReducible { get; }
}

public sealed record RiskProfile
{
    public RiskProfile(
        string id,
        CurrencyCode accountCurrency,
        decimal? maximumRiskPercentPerTrade = null,
        Money? maximumRiskAmountPerTrade = null,
        Money? dailyLossLimit = null,
        Money? weeklyLossLimit = null,
        Money? maximumDrawdown = null,
        Money? maximumPortfolioRisk = null,
        Money? maximumAccountRisk = null,
        Money? maximumPlatformRisk = null,
        decimal minimumRMultiple = 1,
        IReadOnlyCollection<RiskLimit>? additionalLimits = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(accountCurrency);
        if (maximumRiskPercentPerTrade is { } percent && (percent <= 0 || percent > 100))
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRiskPercentPerTrade));
        }

        if (minimumRMultiple < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumRMultiple));
        }

        foreach (var value in new[]
        {
            maximumRiskAmountPerTrade,
            dailyLossLimit,
            weeklyLossLimit,
            maximumDrawdown,
            maximumPortfolioRisk,
            maximumAccountRisk,
            maximumPlatformRisk
        })
        {
            if (value is { } money)
            {
                money.RequireNonNegative(nameof(accountCurrency));
                if (money.Currency != accountCurrency)
                {
                    throw new ArgumentException("Risk profile money limits must use the account currency.", nameof(accountCurrency));
                }
            }
        }

        ArgumentNullException.ThrowIfNull(additionalLimits);
        if (additionalLimits.Any(limit => limit.Limit.Currency != accountCurrency))
        {
            throw new ArgumentException("Additional risk limits must use the account currency.", nameof(additionalLimits));
        }

        Id = id.Trim();
        AccountCurrency = accountCurrency;
        MaximumRiskPercentPerTrade = maximumRiskPercentPerTrade;
        MaximumRiskAmountPerTrade = maximumRiskAmountPerTrade;
        DailyLossLimit = dailyLossLimit;
        WeeklyLossLimit = weeklyLossLimit;
        MaximumDrawdown = maximumDrawdown;
        MaximumPortfolioRisk = maximumPortfolioRisk;
        MaximumAccountRisk = maximumAccountRisk;
        MaximumPlatformRisk = maximumPlatformRisk;
        MinimumRMultiple = minimumRMultiple;
        AdditionalLimits = Array.AsReadOnly(additionalLimits.ToArray());
    }

    public string Id { get; }
    public CurrencyCode AccountCurrency { get; }
    public decimal? MaximumRiskPercentPerTrade { get; }
    public Money? MaximumRiskAmountPerTrade { get; }
    public Money? DailyLossLimit { get; }
    public Money? WeeklyLossLimit { get; }
    public Money? MaximumDrawdown { get; }
    public Money? MaximumPortfolioRisk { get; }
    public Money? MaximumAccountRisk { get; }
    public Money? MaximumPlatformRisk { get; }
    public decimal MinimumRMultiple { get; }
    public IReadOnlyList<RiskLimit> AdditionalLimits { get; }
}

public sealed record AccountRiskContext
{
    public AccountRiskContext(
        Money balance,
        Money equity,
        Money currentDailyLoss,
        Money currentWeeklyLoss,
        Money currentDrawdown,
        Money currentAccountRisk,
        Money currentPlatformRisk)
    {
        balance.RequireNonNegative(nameof(balance));
        equity.RequireNonNegative(nameof(equity));
        currentDailyLoss.RequireNonNegative(nameof(currentDailyLoss));
        currentWeeklyLoss.RequireNonNegative(nameof(currentWeeklyLoss));
        currentDrawdown.RequireNonNegative(nameof(currentDrawdown));
        currentAccountRisk.RequireNonNegative(nameof(currentAccountRisk));
        currentPlatformRisk.RequireNonNegative(nameof(currentPlatformRisk));
        if (balance.Amount <= 0 || equity.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(balance), "Balance and equity must be positive.");
        }

        EnsureSameCurrency(balance, equity, currentDailyLoss, currentWeeklyLoss, currentDrawdown, currentAccountRisk, currentPlatformRisk);
        Balance = balance;
        Equity = equity;
        CurrentDailyLoss = currentDailyLoss;
        CurrentWeeklyLoss = currentWeeklyLoss;
        CurrentDrawdown = currentDrawdown;
        CurrentAccountRisk = currentAccountRisk;
        CurrentPlatformRisk = currentPlatformRisk;
    }

    public Money Balance { get; }
    public Money Equity { get; }
    public Money CurrentDailyLoss { get; }
    public Money CurrentWeeklyLoss { get; }
    public Money CurrentDrawdown { get; }
    public Money CurrentAccountRisk { get; }
    public Money CurrentPlatformRisk { get; }
    public CurrencyCode Currency => Balance.Currency;

    private static void EnsureSameCurrency(params Money[] values)
    {
        var currency = values[0].Currency;
        if (values.Any(value => value.Currency != currency))
        {
            throw new ArgumentException("Account risk values must use the same currency.");
        }
    }
}

public sealed record InstrumentRiskSpecification
{
    public InstrumentRiskSpecification(
        Instrument instrument,
        CurrencyCode quoteCurrency,
        decimal tickSize,
        Money tickValue,
        decimal quantityStep,
        decimal minimumQuantity,
        decimal maximumQuantity,
        Money? exposurePerQuantity = null)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(quoteCurrency);
        if (tickSize <= 0 || quantityStep <= 0 || minimumQuantity <= 0 || maximumQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickSize), "Instrument risk dimensions must be positive.");
        }

        if (minimumQuantity > maximumQuantity)
        {
            throw new ArgumentException("Minimum quantity cannot exceed maximum quantity.", nameof(minimumQuantity));
        }

        tickValue.RequireNonNegative(nameof(tickValue));
        if (tickValue.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickValue), "Tick value must be positive.");
        }

        if (exposurePerQuantity is { } exposure)
        {
            exposure.RequireNonNegative(nameof(exposurePerQuantity));
            if (exposure.Amount <= 0 || exposure.Currency != tickValue.Currency)
            {
                throw new ArgumentException("Exposure per quantity must be positive and use the tick value currency.", nameof(exposurePerQuantity));
            }
        }

        Instrument = instrument;
        QuoteCurrency = quoteCurrency;
        TickSize = tickSize;
        TickValue = tickValue;
        QuantityStep = quantityStep;
        MinimumQuantity = minimumQuantity;
        MaximumQuantity = maximumQuantity;
        ExposurePerQuantity = exposurePerQuantity;
    }

    public Instrument Instrument { get; }
    public CurrencyCode QuoteCurrency { get; }
    public decimal TickSize { get; }
    public Money TickValue { get; }
    public decimal QuantityStep { get; }
    public decimal MinimumQuantity { get; }
    public decimal MaximumQuantity { get; }
    public Money? ExposurePerQuantity { get; }
}

public sealed record PortfolioRiskContext
{
    public PortfolioRiskContext(
        Money currentRisk,
        Money currentExposure,
        Money? maximumExposure = null)
    {
        currentRisk.RequireNonNegative(nameof(currentRisk));
        currentExposure.RequireNonNegative(nameof(currentExposure));
        if (maximumExposure is { } limit)
        {
            limit.RequireNonNegative(nameof(maximumExposure));
            if (limit.Currency != currentExposure.Currency)
            {
                throw new ArgumentException("Maximum exposure must use the portfolio currency.", nameof(maximumExposure));
            }
        }

        if (currentRisk.Currency != currentExposure.Currency)
        {
            throw new ArgumentException("Portfolio risk and exposure must use the same currency.");
        }

        CurrentRisk = currentRisk;
        CurrentExposure = currentExposure;
        MaximumExposure = maximumExposure;
    }

    public Money CurrentRisk { get; }
    public Money CurrentExposure { get; }
    public Money? MaximumExposure { get; }
    public CurrencyCode Currency => CurrentRisk.Currency;
}

public sealed record RiskAssessmentRequest
{
    public const int CurrentVersion = 1;

    public RiskAssessmentRequest(
        RiskAssessmentId assessmentId,
        TradingDecisionId decisionId,
        TradingDecisionResult decision,
        RiskProfile riskProfile,
        AccountRiskContext account,
        InstrumentRiskSpecification instrument,
        PortfolioRiskContext? portfolio = null,
        RiskStrategy strategy = RiskStrategy.Conservative,
        TimeSpan? timeout = null,
        int version = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(assessmentId);
        ArgumentNullException.ThrowIfNull(decisionId);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(riskProfile);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(instrument);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        if (timeout is { } value && value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        AssessmentId = assessmentId;
        DecisionId = decisionId;
        Decision = decision;
        RiskProfile = riskProfile;
        Account = account;
        Instrument = instrument;
        Portfolio = portfolio;
        Strategy = strategy;
        Timeout = timeout;
        Version = version;
    }

    public RiskAssessmentRequest(
        RiskAssessmentId assessmentId,
        TradingDecisionResult decision,
        RiskProfile riskProfile,
        AccountRiskContext account,
        InstrumentRiskSpecification instrument,
        PortfolioRiskContext? portfolio = null,
        RiskStrategy strategy = RiskStrategy.Conservative,
        TimeSpan? timeout = null,
        int version = CurrentVersion)
        : this(
            assessmentId,
            decision?.DecisionId ?? throw new ArgumentNullException(nameof(decision)),
            decision,
            riskProfile,
            account,
            instrument,
            portfolio,
            strategy,
            timeout,
            version)
    {
    }

    public int Version { get; }
    public RiskAssessmentId AssessmentId { get; }
    public TradingDecisionId DecisionId { get; }
    public TradingDecisionResult Decision { get; }
    public RiskProfile RiskProfile { get; }
    public AccountRiskContext Account { get; }
    public InstrumentRiskSpecification Instrument { get; }
    public PortfolioRiskContext? Portfolio { get; }
    public RiskStrategy Strategy { get; }
    public TimeSpan? Timeout { get; }
}

public sealed record RiskConstraintEvaluation
{
    public RiskConstraintEvaluation(
        RiskConstraintKind kind,
        decimal limit,
        decimal consumed,
        decimal remaining,
        RiskUnit unit,
        CurrencyCode? currency,
        string source,
        bool isHard,
        bool isReducible,
        RiskConstraintImpact impact)
    {
        if (limit < 0 || consumed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        Limit = limit;
        Consumed = consumed;
        Remaining = remaining;
        Unit = unit;
        Currency = currency;
        Source = source.Trim();
        IsHard = isHard;
        IsReducible = isReducible;
        Kind = kind;
        Impact = impact;
    }

    public RiskConstraintKind Kind { get; }
    public decimal Limit { get; }
    public decimal Consumed { get; }
    public decimal Remaining { get; }
    public RiskUnit Unit { get; }
    public CurrencyCode? Currency { get; }
    public string Source { get; }
    public bool IsHard { get; }
    public bool IsReducible { get; }
    public RiskConstraintImpact Impact { get; }
}

public sealed record EffectiveRiskBudget
{
    public EffectiveRiskBudget(
        Money amount,
        Money? requestedAmount,
        RiskConstraintKind limitingConstraint,
        string limitingSource,
        IReadOnlyCollection<RiskConstraintEvaluation> evaluations,
        bool isAvailable,
        bool isReduced)
    {
        amount.RequireNonNegative(nameof(amount));
        if (requestedAmount is { } requested)
        {
            requested.RequireNonNegative(nameof(requestedAmount));
            amount.EnsureCurrency(requested);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(limitingSource);
        ArgumentNullException.ThrowIfNull(evaluations);
        Amount = amount;
        RequestedAmount = requestedAmount;
        LimitingConstraint = limitingConstraint;
        LimitingSource = limitingSource.Trim();
        Evaluations = Array.AsReadOnly(evaluations.ToArray());
        IsAvailable = isAvailable;
        IsReduced = isReduced;
    }

    public Money Amount { get; }
    public Money? RequestedAmount { get; }
    public RiskConstraintKind LimitingConstraint { get; }
    public string LimitingSource { get; }
    public IReadOnlyList<RiskConstraintEvaluation> Evaluations { get; }
    public bool IsAvailable { get; }
    public bool IsReduced { get; }
}

public sealed record StopDistance
{
    public StopDistance(
        Price entry,
        Price stop,
        decimal distance,
        decimal tickSize,
        decimal tickCount,
        MarketDirection direction)
    {
        if (distance <= 0 || tickSize <= 0 || tickCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distance));
        }

        Entry = entry;
        Stop = stop;
        Distance = distance;
        TickSize = tickSize;
        TickCount = tickCount;
        Direction = direction;
    }

    public Price Entry { get; }
    public Price Stop { get; }
    public decimal Distance { get; }
    public decimal TickSize { get; }
    public decimal TickCount { get; }
    public MarketDirection Direction { get; }
}

public sealed record PositionSizeProposal
{
    public PositionSizeProposal(
        decimal rawQuantity,
        decimal roundedQuantity,
        decimal finalQuantity,
        Money riskPerQuantityUnit,
        Money actualRisk,
        Money? actualExposure,
        decimal quantityStep,
        decimal minimumQuantity,
        decimal maximumQuantity,
        bool reduced,
        IReadOnlyCollection<string>? reductionReasons = null,
        string formula = "(StopDistance / TickSize) * TickValue")
    {
        if (rawQuantity < 0 || roundedQuantity < 0 || finalQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawQuantity));
        }

        if (quantityStep <= 0 || minimumQuantity <= 0 || maximumQuantity < minimumQuantity)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityStep));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(formula);

        riskPerQuantityUnit.RequireNonNegative(nameof(riskPerQuantityUnit));
        actualRisk.RequireNonNegative(nameof(actualRisk));
        if (actualExposure is { } exposure)
        {
            exposure.RequireNonNegative(nameof(actualExposure));
            actualRisk.EnsureCurrency(exposure);
        }

        RawQuantity = rawQuantity;
        RoundedQuantity = roundedQuantity;
        FinalQuantity = finalQuantity;
        RiskPerQuantityUnit = riskPerQuantityUnit;
        ActualRisk = actualRisk;
        ActualExposure = actualExposure;
        QuantityStep = quantityStep;
        MinimumQuantity = minimumQuantity;
        MaximumQuantity = maximumQuantity;
        Reduced = reduced;
        ReductionReasons = Array.AsReadOnly((reductionReasons ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray());
        Formula = formula.Trim();
    }

    public decimal RawQuantity { get; }
    public decimal RoundedQuantity { get; }
    public decimal FinalQuantity { get; }
    public Money RiskPerQuantityUnit { get; }
    public Money ActualRisk { get; }
    public Money? ActualExposure { get; }
    public decimal QuantityStep { get; }
    public decimal MinimumQuantity { get; }
    public decimal MaximumQuantity { get; }
    public bool Reduced { get; }
    public IReadOnlyList<string> ReductionReasons { get; }
    public string Formula { get; }
}

public sealed record RMultipleAssessment
{
    public RMultipleAssessment(
        Price entry,
        Price stop,
        Price target,
        decimal value,
        decimal minimumRequired,
        bool meetsMinimum,
        string formula)
    {
        if (value < 0 || minimumRequired < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        Entry = entry;
        Stop = stop;
        Target = target;
        Value = value;
        MinimumRequired = minimumRequired;
        MeetsMinimum = meetsMinimum;
        Formula = formula.Trim();
    }

    public Price Entry { get; }
    public Price Stop { get; }
    public Price Target { get; }
    public decimal Value { get; }
    public decimal MinimumRequired { get; }
    public bool MeetsMinimum { get; }
    public string Formula { get; }
}

public sealed record DrawdownAssessment
{
    public DrawdownAssessment(Money current, Money? limit, Money? remaining, bool breached)
    {
        current.RequireNonNegative(nameof(current));
        if (limit is { } maximum)
        {
            maximum.RequireNonNegative(nameof(limit));
            current.EnsureCurrency(maximum);
        }

        if (remaining is { } capacity)
        {
            capacity.RequireNonNegative(nameof(remaining));
            current.EnsureCurrency(capacity);
        }

        Current = current;
        Limit = limit;
        Remaining = remaining;
        Breached = breached;
    }

    public Money Current { get; }
    public Money? Limit { get; }
    public Money? Remaining { get; }
    public bool Breached { get; }
}

public sealed record ExposureAssessment
{
    public ExposureAssessment(
        Money current,
        Money? limit,
        Money? remaining,
        Money? proposed,
        bool withinLimit,
        bool available)
    {
        current.RequireNonNegative(nameof(current));
        foreach (var value in new[] { limit, remaining, proposed })
        {
            if (value is { } money)
            {
                money.RequireNonNegative(nameof(limit));
                current.EnsureCurrency(money);
            }
        }

        Current = current;
        Limit = limit;
        Remaining = remaining;
        Proposed = proposed;
        WithinLimit = withinLimit;
        Available = available;
    }

    public Money Current { get; }
    public Money? Limit { get; }
    public Money? Remaining { get; }
    public Money? Proposed { get; }
    public bool WithinLimit { get; }
    public bool Available { get; }
}

public sealed record RiskWarning
{
    public RiskWarning(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
    }

    public string Code { get; }
    public string Message { get; }
}

public sealed record RiskError
{
    public RiskError(string code, string message, bool fatal = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
        Fatal = fatal;
    }

    public string Code { get; }
    public string Message { get; }
    public bool Fatal { get; }
}

public sealed record RiskAssessmentResult
{
    public const int CurrentSchemaVersion = 1;

    public RiskAssessmentResult(
        RiskAssessmentId assessmentId,
        TradingDecisionId decisionId,
        MarketContextId marketContextId,
        Instrument instrument,
        Timeframe timeframe,
        RiskStrategy strategy,
        RiskAssessmentStatus status,
        RiskVerdict verdict,
        EffectiveRiskBudget? effectiveRiskBudget,
        StopDistance? stopDistance,
        PositionSizeProposal? positionSize,
        IReadOnlyCollection<RMultipleAssessment> rMultiples,
        DrawdownAssessment? drawdown,
        ExposureAssessment? exposure,
        IReadOnlyCollection<RiskConstraintEvaluation> constraints,
        IReadOnlyCollection<DecisionRisk> risks,
        IReadOnlyCollection<DecisionTraceReference> traces,
        IReadOnlyCollection<string> limitations,
        IReadOnlyCollection<RiskWarning> warnings,
        IReadOnlyCollection<RiskError> errors,
        DateTimeOffset createdAtUtc,
        DateTimeOffset completedAtUtc,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(assessmentId);
        ArgumentNullException.ThrowIfNull(decisionId);
        ArgumentNullException.ThrowIfNull(marketContextId);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(timeframe);
        ArgumentNullException.ThrowIfNull(rMultiples);
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(risks);
        ArgumentNullException.ThrowIfNull(traces);
        ArgumentNullException.ThrowIfNull(limitations);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(errors);
        if (completedAtUtc < createdAtUtc)
        {
            throw new ArgumentException("Risk assessment completion cannot precede creation.", nameof(completedAtUtc));
        }

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        AssessmentId = assessmentId;
        DecisionId = decisionId;
        MarketContextId = marketContextId;
        Instrument = instrument;
        Timeframe = timeframe;
        Strategy = strategy;
        Status = status;
        Verdict = verdict;
        EffectiveRiskBudget = effectiveRiskBudget;
        StopDistance = stopDistance;
        PositionSize = positionSize;
        RMultiples = Array.AsReadOnly(rMultiples.ToArray());
        Drawdown = drawdown;
        Exposure = exposure;
        Constraints = Array.AsReadOnly(constraints.ToArray());
        Risks = Array.AsReadOnly(risks.ToArray());
        Traces = Array.AsReadOnly(traces.ToArray());
        Limitations = Array.AsReadOnly(limitations.ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
        Errors = Array.AsReadOnly(errors.ToArray());
        CreatedAtUtc = createdAtUtc;
        CompletedAtUtc = completedAtUtc;
        SchemaVersion = schemaVersion;
    }

    public RiskAssessmentId AssessmentId { get; }
    public TradingDecisionId DecisionId { get; }
    public MarketContextId MarketContextId { get; }
    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public RiskStrategy Strategy { get; }
    public RiskAssessmentStatus Status { get; }
    public RiskVerdict Verdict { get; }
    public EffectiveRiskBudget? EffectiveRiskBudget { get; }
    public StopDistance? StopDistance { get; }
    public PositionSizeProposal? PositionSize { get; }
    public IReadOnlyList<RMultipleAssessment> RMultiples { get; }
    public DrawdownAssessment? Drawdown { get; }
    public ExposureAssessment? Exposure { get; }
    public IReadOnlyList<RiskConstraintEvaluation> Constraints { get; }
    public IReadOnlyList<DecisionRisk> Risks { get; }
    public IReadOnlyList<DecisionTraceReference> Traces { get; }
    public IReadOnlyList<string> Limitations { get; }
    public IReadOnlyList<RiskWarning> Warnings { get; }
    public IReadOnlyList<RiskError> Errors { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public int SchemaVersion { get; }
}
