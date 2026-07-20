using TradeMind.AI.RiskEngine.Domain;

namespace TradeMind.AI.RiskEngine.Application;

internal sealed class RiskBudgetCalculator
{
    public EffectiveRiskBudget Calculate(RiskAssessmentRequest request)
    {
        var candidates = new List<Candidate>();
        var profile = request.RiskProfile;
        var account = request.Account;

        if (profile.MaximumRiskPercentPerTrade is { } percent)
        {
            var amount = account.Equity.Multiply(percent / 100m);
            candidates.Add(new Candidate(
                RiskConstraintKind.RiskPercentPerTrade,
                percent,
                0,
                RiskUnit.Percent,
                amount,
                "risk-profile.maximum-percent-per-trade",
                true,
                false));
        }

        AddMoneyCandidate(candidates, RiskConstraintKind.RiskAmountPerTrade, profile.MaximumRiskAmountPerTrade, null,
            "risk-profile.maximum-amount-per-trade", true, false);
        AddMoneyCandidate(candidates, RiskConstraintKind.DailyLoss, profile.DailyLossLimit, account.CurrentDailyLoss,
            "risk-profile.daily-loss", true, false);
        AddMoneyCandidate(candidates, RiskConstraintKind.WeeklyLoss, profile.WeeklyLossLimit, account.CurrentWeeklyLoss,
            "risk-profile.weekly-loss", true, false);
        AddMoneyCandidate(candidates, RiskConstraintKind.Drawdown, profile.MaximumDrawdown, account.CurrentDrawdown,
            "risk-profile.maximum-drawdown", true, false);
        if (profile.MaximumPortfolioRisk is { } portfolioRisk && request.Portfolio is { } portfolio)
        {
            AddMoneyCandidate(candidates, RiskConstraintKind.PortfolioRisk, portfolioRisk, portfolio.CurrentRisk,
                "risk-profile.maximum-portfolio-risk", true, false);
        }

        AddMoneyCandidate(candidates, RiskConstraintKind.AccountLimit, profile.MaximumAccountRisk, account.CurrentAccountRisk,
            "risk-profile.maximum-account-risk", true, false);
        AddMoneyCandidate(candidates, RiskConstraintKind.PlatformLimit, profile.MaximumPlatformRisk, account.CurrentPlatformRisk,
            "risk-profile.maximum-platform-risk", true, false);

        foreach (var limit in profile.AdditionalLimits.OrderBy(item => item.Kind).ThenBy(item => item.Source, StringComparer.Ordinal))
        {
            AddMoneyCandidate(candidates, limit.Kind, limit.Limit, limit.Consumed, limit.Source, limit.IsHard, limit.IsReducible);
        }

        var evaluations = candidates
            .Select(candidate => candidate.ToEvaluation())
            .ToList();

        if (candidates.Count == 0)
        {
            return new EffectiveRiskBudget(
                new Money(0, account.Currency),
                null,
                RiskConstraintKind.Additional,
                "risk-budget.unavailable",
                evaluations,
                false,
                false);
        }

        var breached = candidates
            .Where(candidate => candidate.Remaining <= 0)
            .OrderBy(candidate => candidate.Remaining)
            .ThenBy(candidate => candidate.Kind)
            .ThenBy(candidate => candidate.Source, StringComparer.Ordinal)
            .FirstOrDefault();
        if (breached is not null)
        {
            return new EffectiveRiskBudget(
                new Money(0, account.Currency),
                RequestedAmount(candidates),
                breached.Kind,
                breached.Source,
                evaluations,
                false,
                false);
        }

        var limiting = candidates
            .OrderBy(candidate => candidate.RemainingMoney.Amount)
            .ThenBy(candidate => candidate.Kind)
            .ThenBy(candidate => candidate.Source, StringComparer.Ordinal)
            .First();
        var requested = RequestedAmount(candidates);
        var reduced = requested is { } requestedValue && limiting.RemainingMoney.Amount < requestedValue.Amount;
        return new EffectiveRiskBudget(
            limiting.RemainingMoney,
            requested,
            limiting.Kind,
            limiting.Source,
            evaluations,
            true,
            reduced);
    }

    private static Money? RequestedAmount(IEnumerable<Candidate> candidates)
    {
        var candidate = candidates
            .Where(item => item.Kind is RiskConstraintKind.RiskPercentPerTrade or RiskConstraintKind.RiskAmountPerTrade)
            .OrderBy(item => item.RemainingMoney.Amount)
            .ThenBy(item => item.Kind)
            .FirstOrDefault();
        return candidate?.RemainingMoney;
    }

    private static void AddMoneyCandidate(
        ICollection<Candidate> candidates,
        RiskConstraintKind kind,
        Money? limit,
        Money? consumed,
        string source,
        bool isHard,
        bool isReducible)
    {
        if (limit is not { } maximum)
        {
            return;
        }

        var used = consumed ?? new Money(0, maximum.Currency);
        maximum.EnsureCurrency(used);
        candidates.Add(new Candidate(
            kind,
            maximum.Amount,
            used.Amount,
            RiskUnit.Money,
            maximum.Subtract(used),
            source,
            isHard,
            isReducible));
    }

    private sealed record Candidate(
        RiskConstraintKind Kind,
        decimal DisplayLimit,
        decimal DisplayConsumed,
        RiskUnit Unit,
        Money RemainingMoney,
        string Source,
        bool IsHard,
        bool IsReducible)
    {
        public decimal Remaining => DisplayLimit - DisplayConsumed;

        public RiskConstraintEvaluation ToEvaluation()
        {
            var impact = Remaining <= 0
                ? RiskConstraintImpact.Rejected
                : IsReducible && RemainingMoney.Amount > 0
                    ? RiskConstraintImpact.NotBinding
                    : RiskConstraintImpact.Binding;
            return new RiskConstraintEvaluation(
                Kind,
                DisplayLimit,
                DisplayConsumed,
                Remaining,
                Unit,
                RemainingMoney.Currency,
                Source,
                IsHard,
                IsReducible,
                impact);
        }
    }
}
