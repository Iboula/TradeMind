using Microsoft.Extensions.Options;

namespace TradeMind.AI.ExpertAgents.Consensus.Application;

public sealed class ConsensusOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public double MinimumConfidenceScore { get; set; } = 20;
    public double MinimumFreshnessScore { get; set; } = 20;
    public double PartiallySucceededPenalty { get; set; } = 0.75;
    public double MaximumAgentContributionShare { get; set; } = 0.45;
    public double MinimumMinorityShare { get; set; } = 0.15;
    public double CriticalConflictShare { get; set; } = 0.25;
    public decimal LevelMergeTolerance { get; set; } = 0.0005m;
    public int MaximumMarketLevels { get; set; } = 20;
    public int MaximumScenarios { get; set; } = 20;
    public int MaximumRisks { get; set; } = 50;
    public int MaximumInvalidations { get; set; } = 50;
    public int MaximumSources { get; set; } = 100;
    public int MaximumConclusionCharacters { get; set; } = 1_000;

    internal void Validate()
    {
        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        }

        ValidatePercent(MinimumConfidenceScore, nameof(MinimumConfidenceScore));
        ValidatePercent(MinimumFreshnessScore, nameof(MinimumFreshnessScore));
        if (PartiallySucceededPenalty is < 0 or > 1 || double.IsNaN(PartiallySucceededPenalty) || double.IsInfinity(PartiallySucceededPenalty))
        {
            throw new ArgumentOutOfRangeException(nameof(PartiallySucceededPenalty));
        }

        ValidateFraction(MaximumAgentContributionShare, nameof(MaximumAgentContributionShare));
        ValidateFraction(MinimumMinorityShare, nameof(MinimumMinorityShare));
        ValidateFraction(CriticalConflictShare, nameof(CriticalConflictShare));
        if (LevelMergeTolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LevelMergeTolerance));
        }

        ValidatePositive(MaximumMarketLevels, nameof(MaximumMarketLevels));
        ValidatePositive(MaximumScenarios, nameof(MaximumScenarios));
        ValidatePositive(MaximumRisks, nameof(MaximumRisks));
        ValidatePositive(MaximumInvalidations, nameof(MaximumInvalidations));
        ValidatePositive(MaximumSources, nameof(MaximumSources));
        ValidatePositive(MaximumConclusionCharacters, nameof(MaximumConclusionCharacters));
    }

    private static void ValidatePercent(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidateFraction(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value is <= 0 or > 1)
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

public sealed class ConsensusOptionsValidator : IValidateOptions<ConsensusOptions>
{
    public ValidateOptionsResult Validate(string? name, ConsensusOptions options)
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
