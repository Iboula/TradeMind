namespace TradeMind.Trading.Analytics;

public sealed class RiskDriftAnalyzer : IRiskDriftAnalyzer
{
    private readonly ITradingStatisticsCalculator _statistics;

    public RiskDriftAnalyzer(ITradingStatisticsCalculator statistics)
    {
        _statistics = statistics;
    }

    public RiskDriftAnalysis Analyze(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        TradeMind.Trading.Coaching.TradingCoachProfile profile,
        int minimumTradesForTrend)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(profile);
        var riskPoints = trades.Select((trade, position) => new RiskPoint(
                trade,
                position,
                TradingAnalyticsFacts.RiskPercentage(trade)))
            .Where(point => point.Risk is not null)
            .ToArray();
        var sufficient = riskPoints.Length >= minimumTradesForTrend;
        if (riskPoints.Length == 0)
        {
            return new RiskDriftAnalysis(false, TradingTrendDirection.InsufficientData, TradingAnalyticsSeverity.Information,
                [], null, null, ["No documented risk percentage was available."], 0, false);
        }

        var split = Math.Max(1, riskPoints.Length / 2);
        var baseline = _statistics.Mean(riskPoints.Take(split).Select(point => point.Risk!.Value));
        var recent = _statistics.Mean(riskPoints.TakeLast(split).Select(point => point.Risk!.Value));
        var threshold = Math.Max(0.1m, (baseline ?? 0) * 0.1m);
        var change = (recent ?? 0) - (baseline ?? 0);
        var direction = !sufficient
            ? TradingTrendDirection.InsufficientData
            : change > threshold
                ? TradingTrendDirection.Worsening
                : change < -threshold
                    ? TradingTrendDirection.Improving
                    : TradingTrendDirection.Stable;

        var observations = new List<string>();
        var support = new HashSet<int>();
        if (direction == TradingTrendDirection.Worsening)
        {
            observations.Add("Documented risk increased between the baseline and recent portions of the supplied history.");
            AddIndexes(riskPoints.TakeLast(split), support);
        }
        else if (direction == TradingTrendDirection.Improving)
        {
            observations.Add("Documented risk decreased between the baseline and recent portions of the supplied history.");
            AddIndexes(riskPoints.TakeLast(split), support);
        }

        var afterLoss = FindRiskIncreasesAfterLoss(trades, requiredLosses: 1);
        if (afterLoss.Count > 0)
        {
            observations.Add("An observed association exists between a preceding loss and higher documented risk on a following trade.");
            support.UnionWith(afterLoss);
        }

        var afterLossStreak = FindRiskIncreasesAfterLoss(trades, requiredLosses: 2);
        if (afterLossStreak.Count > 0)
        {
            observations.Add("An observed association exists between two preceding losses and higher documented risk on a following trade.");
            support.UnionWith(afterLossStreak);
        }

        var afterLargeWin = FindRiskIncreasesAfterLargeWin(trades);
        if (afterLargeWin.Count > 0)
        {
            observations.Add("An observed association exists between a large documented gain and higher risk on a following trade.");
            support.UnionWith(afterLargeWin);
        }

        var exceeded = profile.MaximumRiskPerTrade is { } maximum
            ? riskPoints.Where(point => point.Risk > maximum).Select(point => point.Trade.OriginalIndex).ToArray()
            : [];
        if (exceeded.Length >= 2)
        {
            observations.Add("The supplied coaching-profile risk maximum was exceeded repeatedly.");
            support.UnionWith(exceeded);
        }

        var highVariation = FindHighVariation(riskPoints, baseline ?? riskPoints.Average(point => point.Risk!.Value));
        if (highVariation.Count > 0)
        {
            observations.Add("Large between-trade variation in documented risk was observed.");
            support.UnionWith(highVariation);
        }

        if (!sufficient)
        {
            observations.Add("The sample is too small to classify a risk trend reliably.");
        }

        var driftDetected = sufficient && (direction == TradingTrendDirection.Worsening
            || afterLoss.Count > 0
            || afterLossStreak.Count > 0
            || afterLargeWin.Count > 0
            || exceeded.Length >= 2
            || highVariation.Count > 0);
        var severity = afterLossStreak.Count > 0 || exceeded.Length >= 2
            ? TradingAnalyticsSeverity.Critical
            : driftDetected
                ? TradingAnalyticsSeverity.Warning
                : TradingAnalyticsSeverity.Information;
        var confidence = Math.Clamp(riskPoints.Length / (decimal)Math.Max(minimumTradesForTrend, 1), 0, 1)
            * (riskPoints.Length / (decimal)Math.Max(trades.Count, 1));

        return new RiskDriftAnalysis(
            driftDetected,
            direction,
            severity,
            support,
            baseline,
            recent,
            observations,
            Round(confidence),
            sufficient);
    }

    private static List<int> FindRiskIncreasesAfterLoss(
        IReadOnlyList<TradingJournalTradeAnalysis> trades,
        int requiredLosses)
    {
        var indexes = new List<int>();
        for (var index = requiredLosses; index < trades.Count; index++)
        {
            var priorLosses = true;
            for (var offset = 1; offset <= requiredLosses; offset++)
            {
                priorLosses &= TradingAnalyticsFacts.ResultR(trades[index - offset]) < 0;
            }

            var currentRisk = TradingAnalyticsFacts.RiskPercentage(trades[index]);
            var priorRisk = TradingAnalyticsFacts.RiskPercentage(trades[index - 1]);
            if (priorLosses && currentRisk is not null && priorRisk is not null && currentRisk > priorRisk + 0.1m)
            {
                indexes.Add(trades[index].OriginalIndex);
            }
        }

        return indexes;
    }

    private static List<int> FindRiskIncreasesAfterLargeWin(IReadOnlyList<TradingJournalTradeAnalysis> trades)
    {
        var indexes = new List<int>();
        for (var index = 1; index < trades.Count; index++)
        {
            var currentRisk = TradingAnalyticsFacts.RiskPercentage(trades[index]);
            var priorRisk = TradingAnalyticsFacts.RiskPercentage(trades[index - 1]);
            if (TradingAnalyticsFacts.ResultR(trades[index - 1]) >= 2
                && currentRisk is not null
                && priorRisk is not null
                && currentRisk > priorRisk + 0.1m)
            {
                indexes.Add(trades[index].OriginalIndex);
            }
        }

        return indexes;
    }

    private static List<int> FindHighVariation(IReadOnlyList<RiskPoint> points, decimal baseline)
    {
        var indexes = new List<int>();
        var threshold = Math.Max(0.5m, baseline * 0.5m);
        for (var index = 1; index < points.Count; index++)
        {
            if (Math.Abs(points[index].Risk!.Value - points[index - 1].Risk!.Value) > threshold)
            {
                indexes.Add(points[index].Trade.OriginalIndex);
            }
        }

        return indexes;
    }

    private static void AddIndexes(IEnumerable<RiskPoint> points, ISet<int> destination)
    {
        foreach (var point in points)
        {
            destination.Add(point.Trade.OriginalIndex);
        }
    }

    private static decimal Round(decimal value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private sealed record RiskPoint(TradingJournalTradeAnalysis Trade, int Position, decimal? Risk);
}
