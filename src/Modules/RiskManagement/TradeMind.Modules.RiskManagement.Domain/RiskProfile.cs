using TradeMind.BuildingBlocks.Domain;

namespace TradeMind.Modules.RiskManagement.Domain;

public sealed class RiskProfile : AggregateRoot<Guid>
{
    private RiskProfile(Guid id, decimal accountBalance, decimal maxRiskPerTradePercent, decimal maxDailyLossPercent)
        : base(id)
    {
        AccountBalance = accountBalance;
        MaxRiskPerTradePercent = maxRiskPerTradePercent;
        MaxDailyLossPercent = maxDailyLossPercent;
    }

    public decimal AccountBalance { get; private set; }
    public decimal MaxRiskPerTradePercent { get; private set; }
    public decimal MaxDailyLossPercent { get; private set; }

    public static RiskProfile Create(decimal accountBalance, decimal maxRiskPerTradePercent, decimal maxDailyLossPercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountBalance);
        ValidatePercent(maxRiskPerTradePercent, nameof(maxRiskPerTradePercent));
        ValidatePercent(maxDailyLossPercent, nameof(maxDailyLossPercent));
        return new RiskProfile(Guid.NewGuid(), accountBalance, maxRiskPerTradePercent, maxDailyLossPercent);
    }

    public decimal MaximumRiskAmount() => decimal.Round(AccountBalance * MaxRiskPerTradePercent / 100m, 2);

    private static void ValidatePercent(decimal value, string name)
    {
        if (value <= 0 || value > 100) throw new ArgumentOutOfRangeException(name);
    }
}
