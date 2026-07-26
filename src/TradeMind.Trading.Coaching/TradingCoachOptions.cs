using Microsoft.Extensions.Options;

namespace TradeMind.Trading.Coaching;

public sealed class TradingJournalValidationOptions
{
    public const string SectionName = "TradingCoach:Validation";

    public int MaximumTextLength { get; set; } = 4_000;
    public decimal MaximumAbsoluteRMultiple { get; set; } = 100;
    public decimal PercentageConsistencyTolerance { get; set; } = 0.01m;
    public decimal MinimumCompletenessScore { get; set; } = 50;
}

public sealed class TradingCoachSafetyOptions
{
    public const string SectionName = "TradingCoach:Safety";

    public bool FailClosed { get; set; } = true;
}

internal sealed class TradingJournalValidationOptionsValidator : IValidateOptions<TradingJournalValidationOptions>
{
    public ValidateOptionsResult Validate(string? name, TradingJournalValidationOptions options)
    {
        if (options.MaximumTextLength is < 100 or > 100_000)
        {
            return ValidateOptionsResult.Fail("MaximumTextLength must be between 100 and 100000.");
        }

        if (options.MaximumAbsoluteRMultiple is <= 0 or > 10_000)
        {
            return ValidateOptionsResult.Fail("MaximumAbsoluteRMultiple must be positive and no greater than 10000.");
        }

        if (options.PercentageConsistencyTolerance is < 0 or > 10)
        {
            return ValidateOptionsResult.Fail("PercentageConsistencyTolerance must be between 0 and 10.");
        }

        if (options.MinimumCompletenessScore is < 0 or > 100)
        {
            return ValidateOptionsResult.Fail("MinimumCompletenessScore must be between 0 and 100.");
        }

        return ValidateOptionsResult.Success;
    }
}
