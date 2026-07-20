using Microsoft.Extensions.Options;

namespace TradeMind.AI.TradingPlans.Application;

public sealed class TradingPlanOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan DefaultPlanLifetime { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan MaximumSourceAge { get; set; } = TimeSpan.FromMinutes(15);
    public decimal EntryChangeTolerance { get; set; }
    public bool RequireTargets { get; set; } = true;
    public int MaximumTargets { get; set; } = 10;
    public int MaximumAlternatives { get; set; } = 10;
    public int MaximumConditions { get; set; } = 50;
    public int MaximumInvalidations { get; set; } = 50;
    public int MaximumAbandonCriteria { get; set; } = 50;
    public int MaximumRules { get; set; } = 50;
    public int MaximumRisks { get; set; } = 50;
    public int MaximumTraces { get; set; } = 100;
    public int MaximumWarnings { get; set; } = 50;
    public int MaximumErrors { get; set; } = 50;
    public int MaximumLimitations { get; set; } = 50;

    internal void Validate()
    {
        ValidatePositive(Timeout, nameof(Timeout));
        ValidatePositive(DefaultPlanLifetime, nameof(DefaultPlanLifetime));
        ValidatePositive(MaximumSourceAge, nameof(MaximumSourceAge));
        if (EntryChangeTolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(EntryChangeTolerance));
        }

        ValidatePositive(MaximumTargets, nameof(MaximumTargets));
        ValidatePositive(MaximumAlternatives, nameof(MaximumAlternatives));
        ValidatePositive(MaximumConditions, nameof(MaximumConditions));
        ValidatePositive(MaximumInvalidations, nameof(MaximumInvalidations));
        ValidatePositive(MaximumAbandonCriteria, nameof(MaximumAbandonCriteria));
        ValidatePositive(MaximumRules, nameof(MaximumRules));
        ValidatePositive(MaximumRisks, nameof(MaximumRisks));
        ValidatePositive(MaximumTraces, nameof(MaximumTraces));
        ValidatePositive(MaximumWarnings, nameof(MaximumWarnings));
        ValidatePositive(MaximumErrors, nameof(MaximumErrors));
        ValidatePositive(MaximumLimitations, nameof(MaximumLimitations));
    }

    private static void ValidatePositive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidatePositive(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

public sealed class TradingPlanOptionsValidator : IValidateOptions<TradingPlanOptions>
{
    public ValidateOptionsResult Validate(string? name, TradingPlanOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return ValidateOptionsResult.Fail(exception.Message);
        }
    }
}
