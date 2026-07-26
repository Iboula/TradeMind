using Microsoft.Extensions.Options;

namespace TradeMind.AI.Tools;

public sealed class AIToolEngineOptions
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaximumTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public AIToolSideEffectLevel MaximumAllowedSideEffectLevel { get; set; } = AIToolSideEffectLevel.ReadOnly;

    public bool RejectUnknownArguments { get; set; } = true;

    public bool IncludeDetailedInternalErrors { get; set; }

    public bool EnableDevelopmentTools { get; set; }

    public int MaximumComposedResultCharacters { get; set; } = 8_000;
}

internal sealed class AIToolEngineOptionsValidator : IValidateOptions<AIToolEngineOptions>
{
    public ValidateOptionsResult Validate(string? name, AIToolEngineOptions options)
    {
        if (options.DefaultTimeout <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("DefaultTimeout must be positive.");
        }

        if (options.MaximumTimeout <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("MaximumTimeout must be positive.");
        }

        if (options.DefaultTimeout > options.MaximumTimeout)
        {
            return ValidateOptionsResult.Fail("DefaultTimeout cannot exceed MaximumTimeout.");
        }

        if (!Enum.IsDefined(options.MaximumAllowedSideEffectLevel))
        {
            return ValidateOptionsResult.Fail("MaximumAllowedSideEffectLevel is invalid.");
        }

        if (options.MaximumComposedResultCharacters <= 0)
        {
            return ValidateOptionsResult.Fail("MaximumComposedResultCharacters must be positive.");
        }

        return ValidateOptionsResult.Success;
    }
}
