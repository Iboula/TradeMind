using Microsoft.Extensions.Options;

namespace TradeMind.AI.TradingDecisions.Application;

public sealed class TradingDecisionOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public double MinimumConsensusConfidence { get; set; } = 60;
    public double MinimumConsensusCoverage { get; set; } = 60;
    public double MinimumScenarioConfidence { get; set; } = 40;
    public int MaximumAlternativeScenarios { get; set; } = 5;
    public int MaximumTargets { get; set; } = 5;
    public int MaximumRisks { get; set; } = 50;
    public int MaximumInvalidations { get; set; } = 50;
    public int MaximumTraces { get; set; } = 100;
    public int MaximumWarnings { get; set; } = 50;

    internal void Validate()
    {
        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        }

        ValidatePercent(MinimumConsensusConfidence, nameof(MinimumConsensusConfidence));
        ValidatePercent(MinimumConsensusCoverage, nameof(MinimumConsensusCoverage));
        ValidatePercent(MinimumScenarioConfidence, nameof(MinimumScenarioConfidence));
        ValidatePositive(MaximumAlternativeScenarios, nameof(MaximumAlternativeScenarios));
        ValidatePositive(MaximumTargets, nameof(MaximumTargets));
        ValidatePositive(MaximumRisks, nameof(MaximumRisks));
        ValidatePositive(MaximumInvalidations, nameof(MaximumInvalidations));
        ValidatePositive(MaximumTraces, nameof(MaximumTraces));
        ValidatePositive(MaximumWarnings, nameof(MaximumWarnings));
    }

    private static void ValidatePercent(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value is < 0 or > 100)
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

public sealed class TradingDecisionOptionsValidator : IValidateOptions<TradingDecisionOptions>
{
    public ValidateOptionsResult Validate(string? name, TradingDecisionOptions options)
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
