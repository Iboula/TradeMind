using Microsoft.Extensions.Options;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Agents;

public sealed class AIAgentFrameworkOptions
{
    public TimeSpan DefaultExecutionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan MaximumExecutionTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public bool EnableDevelopmentAgents { get; set; }

    public bool IncludeDetailedInternalErrors { get; set; }

    public AIAgentVersionSelection DefaultVersionSelection { get; set; } = AIAgentVersionSelection.LatestStable;

    public AIToolSideEffectLevel MaximumAllowedSideEffectLevel { get; set; } = AIToolSideEffectLevel.ReadOnly;
}

public sealed class AIAgentFrameworkOptionsValidator : IValidateOptions<AIAgentFrameworkOptions>
{
    public ValidateOptionsResult Validate(string? name, AIAgentFrameworkOptions options)
    {
        var failures = new List<string>();
        if (options.DefaultExecutionTimeout <= TimeSpan.Zero)
        {
            failures.Add("DefaultExecutionTimeout must be positive.");
        }

        if (options.MaximumExecutionTimeout <= TimeSpan.Zero)
        {
            failures.Add("MaximumExecutionTimeout must be positive.");
        }

        if (options.DefaultExecutionTimeout > options.MaximumExecutionTimeout)
        {
            failures.Add("DefaultExecutionTimeout cannot exceed MaximumExecutionTimeout.");
        }

        if (options.DefaultVersionSelection == AIAgentVersionSelection.Exact)
        {
            failures.Add("DefaultVersionSelection cannot be Exact because no default exact version exists.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
