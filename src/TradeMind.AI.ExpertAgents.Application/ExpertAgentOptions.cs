using Microsoft.Extensions.Options;

namespace TradeMind.AI.ExpertAgents.Application;

public sealed class ExpertAgentOptions
{
    public TimeSpan DefaultExecutionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaximumExecutionTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public int MaximumObservations { get; set; } = 50;
    public int MaximumEvidence { get; set; } = 50;
    public int MaximumMarketLevels { get; set; } = 30;
    public int MaximumScenarios { get; set; } = 10;
    public int MaximumWarnings { get; set; } = 20;
    public int MaximumErrors { get; set; } = 20;
    public int MaximumInvalidations { get; set; } = 20;
    public int MaximumRisks { get; set; } = 20;
    public int MaximumLimitations { get; set; } = 20;
    public int MaximumSummaryCharacters { get; set; } = 2_000;
    public int MaximumDescriptionCharacters { get; set; } = 1_000;
    public int MaximumMetadataEntries { get; set; } = 20;
    public int MaximumMetadataValueCharacters { get; set; } = 256;
    public bool TruncateExcessCollections { get; set; } = true;
    public bool AllowExperimentalAgents { get; set; } = false;
    public bool AllowPreviewAgents { get; set; } = true;

    internal void Validate()
    {
        if (DefaultExecutionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(DefaultExecutionTimeout));
        }

        if (MaximumExecutionTimeout <= TimeSpan.Zero || DefaultExecutionTimeout > MaximumExecutionTimeout)
        {
            throw new ArgumentException("MaximumExecutionTimeout must be positive and at least the default timeout.", nameof(MaximumExecutionTimeout));
        }

        ValidatePositive(MaximumObservations, nameof(MaximumObservations));
        ValidatePositive(MaximumEvidence, nameof(MaximumEvidence));
        ValidatePositive(MaximumMarketLevels, nameof(MaximumMarketLevels));
        ValidatePositive(MaximumScenarios, nameof(MaximumScenarios));
        ValidatePositive(MaximumWarnings, nameof(MaximumWarnings));
        ValidatePositive(MaximumErrors, nameof(MaximumErrors));
        ValidatePositive(MaximumInvalidations, nameof(MaximumInvalidations));
        ValidatePositive(MaximumRisks, nameof(MaximumRisks));
        ValidatePositive(MaximumLimitations, nameof(MaximumLimitations));
        ValidatePositive(MaximumSummaryCharacters, nameof(MaximumSummaryCharacters));
        ValidatePositive(MaximumDescriptionCharacters, nameof(MaximumDescriptionCharacters));
        ValidatePositive(MaximumMetadataEntries, nameof(MaximumMetadataEntries));
        ValidatePositive(MaximumMetadataValueCharacters, nameof(MaximumMetadataValueCharacters));
    }

    private static void ValidatePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

public sealed class ExpertAgentOptionsValidator : IValidateOptions<ExpertAgentOptions>
{
    public ValidateOptionsResult Validate(string? name, ExpertAgentOptions options)
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
