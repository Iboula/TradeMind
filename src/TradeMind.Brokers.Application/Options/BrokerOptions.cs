using Microsoft.Extensions.Options;

namespace TradeMind.Brokers.Application.Options;

public sealed class BrokerOptions
{
    public bool Enabled { get; set; }
    public string DefaultMode { get; set; } = "Simulation";
    public bool AllowDemoExecution { get; set; }
    public bool AllowLiveExecution { get; set; }
    public bool RequireExplicitLiveConfirmation { get; set; } = true;
    public decimal MaximumOrderQuantity { get; set; } = 1_000_000m;
    public int MaximumConcurrentExecutions { get; set; } = 16;
    public int IdempotencyTtlMinutes { get; set; } = 1440;
    public int OperationTimeoutSeconds { get; set; } = 30;
    public int OrphanPositionStaleAfterSeconds { get; set; } = 300;
    public bool RequireTradingPlanAndRiskApproval { get; set; } = true;
    public string[] AllowedConnectors { get; set; } = [];
    public TimeSpan OperationTimeout => TimeSpan.FromSeconds(OperationTimeoutSeconds);
}

public sealed class BrokerOptionsValidator : IValidateOptions<BrokerOptions>
{
    public ValidateOptionsResult Validate(string? name, BrokerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        if (!Enum.TryParse<TradeMind.Brokers.Domain.BrokerExecutionMode>(options.DefaultMode, true, out _)) errors.Add("TradeMind:Brokers:DefaultMode is invalid.");
        if (options.MaximumOrderQuantity <= 0) errors.Add("TradeMind:Brokers:MaximumOrderQuantity must be positive.");
        if (options.MaximumConcurrentExecutions is < 1 or > 1024) errors.Add("TradeMind:Brokers:MaximumConcurrentExecutions must be between 1 and 1024.");
        if (options.IdempotencyTtlMinutes is < 1 or > 10080) errors.Add("TradeMind:Brokers:IdempotencyTtlMinutes must be between 1 and 10080.");
        if (options.OperationTimeoutSeconds is < 1 or > 300) errors.Add("TradeMind:Brokers:OperationTimeoutSeconds must be between 1 and 300.");
        if (options.OrphanPositionStaleAfterSeconds is < 1 or > 86400) errors.Add("TradeMind:Brokers:OrphanPositionStaleAfterSeconds must be between 1 and 86400.");
        if (options.AllowLiveExecution && !options.RequireExplicitLiveConfirmation) errors.Add("Live execution must require explicit confirmation.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
