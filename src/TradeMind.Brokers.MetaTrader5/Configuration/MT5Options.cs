using Microsoft.Extensions.Options;
using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.MetaTrader5.Configuration;

public sealed class MT5Options
{
    public const string SectionName = "TradeMind:Brokers:MetaTrader5";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 0;
    public int TimeoutSeconds { get; set; } = 10;
    public int HeartbeatSeconds { get; set; } = 30;
    public int ReconnectAttempts { get; set; } = 3;
    public string Mode { get; set; } = nameof(BrokerExecutionMode.Simulation);
    public bool AllowDemo { get; set; }
    public bool AllowLive { get; set; }

    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);
    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(HeartbeatSeconds);
}

public sealed class MT5OptionsValidator : IValidateOptions<MT5Options>
{
    public ValidateOptionsResult Validate(string? name, MT5Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Host)) errors.Add("TradeMind:Brokers:MetaTrader5:Host is required.");
        if (options.Port is < 0 or > 65535) errors.Add("TradeMind:Brokers:MetaTrader5:Port must be between 0 and 65535.");
        if (options.TimeoutSeconds is < 1 or > 300) errors.Add("TradeMind:Brokers:MetaTrader5:TimeoutSeconds must be between 1 and 300.");
        if (options.HeartbeatSeconds is < 1 or > 3600) errors.Add("TradeMind:Brokers:MetaTrader5:HeartbeatSeconds must be between 1 and 3600.");
        if (options.ReconnectAttempts is < 0 or > 10) errors.Add("TradeMind:Brokers:MetaTrader5:ReconnectAttempts must be between 0 and 10.");
        if (!Enum.TryParse<BrokerExecutionMode>(options.Mode, true, out var mode)) errors.Add("TradeMind:Brokers:MetaTrader5:Mode is invalid.");
        else if (mode == BrokerExecutionMode.Live || options.AllowLive) errors.Add("Live MetaTrader 5 execution is not implemented in Sprint 29.");
        if (mode == BrokerExecutionMode.Demo && !options.AllowDemo) errors.Add("Demo mode requires AllowDemo=true.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
