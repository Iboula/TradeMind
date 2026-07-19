namespace TradeMind.Trading.Coaching;

public sealed class TradeMetricsCalculator : ITradeMetricsCalculator
{
    public TradeMetricsResult Calculate(TradingJournalNormalizationResult normalizationResult)
    {
        ArgumentNullException.ThrowIfNull(normalizationResult);
        var request = normalizationResult.NormalizedRequest;
        var metrics = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var unavailable = new List<string>();
        var warnings = new List<string>();
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);

        AddRiskPercentage(request, normalizationResult, metrics, unavailable, sources);
        AddDistanceMetric(TradeMetricNames.StopDistance, request.EntryPrice, request.StopLoss, metrics, unavailable, sources);
        AddDistanceMetric(TradeMetricNames.TargetDistance, request.EntryPrice, request.TakeProfit, metrics, unavailable, sources);
        AddRewardToRisk(request, metrics, unavailable, sources);
        AddRealizedR(request, normalizationResult, metrics, unavailable, warnings, sources);
        AddDuration(request, metrics, unavailable, sources);
        AddResultPercentage(request, metrics, unavailable, sources);
        AddRiskDelta(request, metrics, unavailable, sources);

        return new TradeMetricsResult(metrics, unavailable, warnings, sources);
    }

    private static void AddRiskPercentage(
        TradingJournalAnalysisRequest request,
        TradingJournalNormalizationResult normalization,
        IDictionary<string, decimal> metrics,
        ICollection<string> unavailable,
        IDictionary<string, string> sources)
    {
        if (request.ActualRiskPercentage is { } explicitValue)
        {
            metrics[TradeMetricNames.RiskPercentage] = explicitValue;
            sources[TradeMetricNames.RiskPercentage] = normalization.DerivedFields.ContainsKey(nameof(request.ActualRiskPercentage))
                ? "riskAmount/accountBalance"
                : "request.actualRiskPercentage";
            return;
        }

        unavailable.Add(TradeMetricNames.RiskPercentage);
    }

    private static void AddDistanceMetric(
        string name,
        decimal? entry,
        decimal? endpoint,
        IDictionary<string, decimal> metrics,
        ICollection<string> unavailable,
        IDictionary<string, string> sources)
    {
        if (entry is not null && endpoint is not null)
        {
            metrics[name] = Round(Math.Abs(endpoint.Value - entry.Value));
            sources[name] = name == TradeMetricNames.StopDistance ? "entryPrice/stopLoss" : "entryPrice/takeProfit";
            return;
        }

        unavailable.Add(name);
    }

    private static void AddRewardToRisk(
        TradingJournalAnalysisRequest request,
        IDictionary<string, decimal> metrics,
        ICollection<string> unavailable,
        IDictionary<string, string> sources)
    {
        if (request.EntryPrice is { } entry
            && request.StopLoss is { } stop
            && request.TakeProfit is { } target)
        {
            var stopDistance = Math.Abs(entry - stop);
            if (stopDistance > 0)
            {
                metrics[TradeMetricNames.PlannedRewardToRisk] = Round(Math.Abs(target - entry) / stopDistance);
                sources[TradeMetricNames.PlannedRewardToRisk] = "entryPrice/stopLoss/takeProfit";
                return;
            }
        }

        unavailable.Add(TradeMetricNames.PlannedRewardToRisk);
    }

    private static void AddRealizedR(
        TradingJournalAnalysisRequest request,
        TradingJournalNormalizationResult normalization,
        IDictionary<string, decimal> metrics,
        ICollection<string> unavailable,
        ICollection<string> warnings,
        IDictionary<string, string> sources)
    {
        if (request.ResultRMultiple is { } explicitValue)
        {
            metrics[TradeMetricNames.RealizedRMultiple] = explicitValue;
            sources[TradeMetricNames.RealizedRMultiple] = normalization.DerivedFields.ContainsKey(nameof(request.ResultRMultiple))
                ? "resultAmount/riskAmount"
                : "request.resultRMultiple";

            if (!normalization.DerivedFields.ContainsKey(nameof(request.ResultRMultiple))
                && request.ResultAmount is { } result
                && request.RiskAmount is > 0
                && Math.Abs(result / request.RiskAmount.Value - explicitValue) > 0.01m)
            {
                warnings.Add("The explicit R multiple is inconsistent with result amount and risk amount; the explicit value was preserved.");
            }

            return;
        }

        unavailable.Add(TradeMetricNames.RealizedRMultiple);
    }

    private static void AddDuration(
        TradingJournalAnalysisRequest request,
        IDictionary<string, decimal> metrics,
        ICollection<string> unavailable,
        IDictionary<string, string> sources)
    {
        if (request.OpenedAtUtc is { } opened && request.ClosedAtUtc is { } closed)
        {
            metrics[TradeMetricNames.DurationMinutes] = Round((decimal)(closed - opened).TotalMinutes);
            sources[TradeMetricNames.DurationMinutes] = "openedAtUtc/closedAtUtc";
            return;
        }

        unavailable.Add(TradeMetricNames.DurationMinutes);
    }

    private static void AddResultPercentage(
        TradingJournalAnalysisRequest request,
        IDictionary<string, decimal> metrics,
        ICollection<string> unavailable,
        IDictionary<string, string> sources)
    {
        if (request.ResultAmount is { } result && request.AccountBalance is > 0)
        {
            metrics[TradeMetricNames.ResultPercentageOfBalance] = Round(result / request.AccountBalance.Value * 100);
            sources[TradeMetricNames.ResultPercentageOfBalance] = "resultAmount/accountBalance";
            return;
        }

        unavailable.Add(TradeMetricNames.ResultPercentageOfBalance);
    }

    private static void AddRiskDelta(
        TradingJournalAnalysisRequest request,
        IDictionary<string, decimal> metrics,
        ICollection<string> unavailable,
        IDictionary<string, string> sources)
    {
        if (request.PlannedRiskPercentage is { } planned && request.ActualRiskPercentage is { } actual)
        {
            metrics[TradeMetricNames.PlannedActualRiskDelta] = Round(actual - planned);
            sources[TradeMetricNames.PlannedActualRiskDelta] = "actualRiskPercentage-plannedRiskPercentage";
            return;
        }

        unavailable.Add(TradeMetricNames.PlannedActualRiskDelta);
    }

    private static decimal Round(decimal value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);
}
