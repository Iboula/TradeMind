using Microsoft.Extensions.Options;

namespace TradeMind.AI.RiskEngine.Application;

public sealed class RiskEngineOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public int MaximumConstraints { get; set; } = 50;
    public int MaximumRisks { get; set; } = 50;
    public int MaximumWarnings { get; set; } = 50;
    public int MaximumLimitations { get; set; } = 50;
    public int MaximumTraces { get; set; } = 100;
    public int MaximumRMultiples { get; set; } = 10;

    internal void Validate()
    {
        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        }

        ValidatePositive(MaximumConstraints, nameof(MaximumConstraints));
        ValidatePositive(MaximumRisks, nameof(MaximumRisks));
        ValidatePositive(MaximumWarnings, nameof(MaximumWarnings));
        ValidatePositive(MaximumLimitations, nameof(MaximumLimitations));
        ValidatePositive(MaximumTraces, nameof(MaximumTraces));
        ValidatePositive(MaximumRMultiples, nameof(MaximumRMultiples));
    }

    private static void ValidatePositive(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

public sealed class RiskEngineOptionsValidator : IValidateOptions<RiskEngineOptions>
{
    public ValidateOptionsResult Validate(string? name, RiskEngineOptions options)
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
