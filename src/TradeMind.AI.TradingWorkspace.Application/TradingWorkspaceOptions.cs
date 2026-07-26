using Microsoft.Extensions.Options;

namespace TradeMind.AI.TradingWorkspace.Application;

public sealed class TradingWorkspaceOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan ContextMaximumAge { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan ConsensusMaximumAge { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan DecisionMaximumAge { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan AccountInstrumentMaximumAge { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RiskMaximumAge { get; set; } = TimeSpan.FromMinutes(15);
    public int MaximumTimelineEntries { get; set; } = 100;
    public int MaximumAlerts { get; set; } = 50;
    public int MaximumBlockers { get; set; } = 50;
    public int MaximumNextActions { get; set; } = 20;
    public int MaximumTraces { get; set; } = 100;
    public int MaximumWarnings { get; set; } = 50;
    public int MaximumErrors { get; set; } = 50;
    public int MaximumLimitations { get; set; } = 50;
    public int MaximumNonCriticalRisks { get; set; } = 50;

    internal void Validate()
    {
        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        }

        ValidateAge(ContextMaximumAge, nameof(ContextMaximumAge));
        ValidateAge(ConsensusMaximumAge, nameof(ConsensusMaximumAge));
        ValidateAge(DecisionMaximumAge, nameof(DecisionMaximumAge));
        ValidateAge(AccountInstrumentMaximumAge, nameof(AccountInstrumentMaximumAge));
        ValidateAge(RiskMaximumAge, nameof(RiskMaximumAge));
        ValidateCount(MaximumTimelineEntries, nameof(MaximumTimelineEntries));
        ValidateCount(MaximumAlerts, nameof(MaximumAlerts));
        ValidateCount(MaximumBlockers, nameof(MaximumBlockers));
        ValidateCount(MaximumNextActions, nameof(MaximumNextActions));
        ValidateCount(MaximumTraces, nameof(MaximumTraces));
        ValidateCount(MaximumWarnings, nameof(MaximumWarnings));
        ValidateCount(MaximumErrors, nameof(MaximumErrors));
        ValidateCount(MaximumLimitations, nameof(MaximumLimitations));
        ValidateCount(MaximumNonCriticalRisks, nameof(MaximumNonCriticalRisks));
    }

    private static void ValidateAge(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidateCount(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

public sealed class TradingWorkspaceOptionsValidator : IValidateOptions<TradingWorkspaceOptions>
{
    public ValidateOptionsResult Validate(string? name, TradingWorkspaceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
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
