using Microsoft.Extensions.Options;
using TradeMind.AI.PaperTrading.Domain;

namespace TradeMind.AI.PaperTrading.Application;

public sealed class PaperTradingOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaximumPricePoints { get; set; } = 10_000;
    public int MaximumMarketTicks { get; set; } = 10_000;
    public int MaximumOrders { get; set; } = 16;
    public int MaximumFills { get; set; } = 16;
    public int MaximumExecutions { get; set; } = 16;
    public int MaximumPositions { get; set; } = 16;
    public int MaximumEquityPoints { get; set; } = 10_000;
    public int MaximumTimelineEvents { get; set; } = 20_000;
    public int MaximumWarnings { get; set; } = 100;
    public int MaximumErrors { get; set; } = 100;
    public decimal PointValue { get; set; } = 1m;
    public decimal CommissionPerUnit { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal SlippagePerUnit { get; set; }
    public decimal SlippageTicks { get; set; }
    public decimal TickSize { get; set; }
    public int? PriceDecimals { get; set; }
    public int? QuantityDecimals { get; set; }
    public PaperTradingFillBehavior FillBehavior { get; set; } = PaperTradingFillBehavior.Touch;
    public PaperSpreadHandling SpreadHandling { get; set; } = PaperSpreadHandling.UseBidAsk;
    public PaperAmbiguousTriggerPolicy AmbiguousTriggerPolicy { get; set; } = PaperAmbiguousTriggerPolicy.ConservativeStopFirst;
    public PaperTriggerTieBreakPolicy TriggerTieBreakPolicy { get; set; } = PaperTriggerTieBreakPolicy.StopLossFirst;
}

public sealed class PaperTradingOptionsValidator : IValidateOptions<PaperTradingOptions>
{
    public ValidateOptionsResult Validate(string? name, PaperTradingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.Timeout <= TimeSpan.Zero)
        {
            failures.Add("Timeout must be positive.");
        }

        if (options.MaximumPricePoints <= 0 || options.MaximumMarketTicks <= 0 || options.MaximumOrders <= 0 || options.MaximumFills <= 0 || options.MaximumExecutions <= 0 || options.MaximumPositions <= 0 ||
            options.MaximumEquityPoints <= 0 || options.MaximumTimelineEvents <= 0 || options.MaximumWarnings <= 0 ||
            options.MaximumErrors <= 0)
        {
            failures.Add("All paper trading limits must be positive.");
        }

        if (options.PointValue <= 0 || options.CommissionPerUnit < 0 || options.CommissionRate < 0 || options.SlippagePerUnit < 0 || options.SlippageTicks < 0 || options.TickSize < 0)
        {
            failures.Add("PointValue must be positive and cost options cannot be negative.");
        }

        if (options.PriceDecimals is < 0 or > 18 || options.QuantityDecimals is < 0 or > 18)
        {
            failures.Add("Precision must be between zero and eighteen decimals.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
