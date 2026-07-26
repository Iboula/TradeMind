using Microsoft.Extensions.Options;

namespace TradeMind.AI.ExpertAgents.Dispatch.Application;

public sealed class AgentDispatchOptions
{
    public TimeSpan DefaultGlobalTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaximumGlobalTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan DefaultPerAgentTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan MaximumPerAgentTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public int DefaultMinimumAgents { get; set; } = 1;
    public int DefaultMaximumAgents { get; set; } = 8;
    public int MaximumAgents { get; set; } = 32;
    public int MaximumParallelism { get; set; } = 4;
    public decimal DefaultMaximumBudgetUnits { get; set; } = 100;
    public decimal DefaultAgentCostUnits { get; set; } = 1;
    public TimeSpan DefaultEstimatedAgentDuration { get; set; } = TimeSpan.FromSeconds(1);
    public bool AllowExperimentalAgents { get; set; } = false;

    internal void Validate()
    {
        ValidateTimeout(DefaultGlobalTimeout, nameof(DefaultGlobalTimeout));
        ValidateTimeout(MaximumGlobalTimeout, nameof(MaximumGlobalTimeout));
        ValidateTimeout(DefaultPerAgentTimeout, nameof(DefaultPerAgentTimeout));
        ValidateTimeout(MaximumPerAgentTimeout, nameof(MaximumPerAgentTimeout));
        if (DefaultGlobalTimeout > MaximumGlobalTimeout)
        {
            throw new ArgumentException("Default global timeout cannot exceed its maximum.", nameof(DefaultGlobalTimeout));
        }

        if (DefaultPerAgentTimeout > MaximumPerAgentTimeout)
        {
            throw new ArgumentException("Default per-agent timeout cannot exceed its maximum.", nameof(DefaultPerAgentTimeout));
        }

        if (DefaultMinimumAgents <= 0
            || DefaultMaximumAgents <= 0
            || DefaultMinimumAgents > DefaultMaximumAgents
            || DefaultMaximumAgents > MaximumAgents)
        {
            throw new ArgumentException("Dispatch agent limits are invalid.", nameof(DefaultMinimumAgents));
        }

        if (MaximumParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumParallelism));
        }

        var maximumBudgetAsDouble = decimal.ToDouble(DefaultMaximumBudgetUnits);
        if (DefaultMaximumBudgetUnits < 0 || double.IsNaN(maximumBudgetAsDouble) || double.IsInfinity(maximumBudgetAsDouble))
        {
            throw new ArgumentOutOfRangeException(nameof(DefaultMaximumBudgetUnits));
        }

        var agentCostAsDouble = decimal.ToDouble(DefaultAgentCostUnits);
        if (DefaultAgentCostUnits <= 0 || double.IsNaN(agentCostAsDouble) || double.IsInfinity(agentCostAsDouble))
        {
            throw new ArgumentOutOfRangeException(nameof(DefaultAgentCostUnits));
        }

        ValidateTimeout(DefaultEstimatedAgentDuration, nameof(DefaultEstimatedAgentDuration));
    }

    private static void ValidateTimeout(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

public sealed class AgentDispatchOptionsValidator : IValidateOptions<AgentDispatchOptions>
{
    public ValidateOptionsResult Validate(string? name, AgentDispatchOptions options)
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
