using System.Globalization;

namespace TradeMind.Trading.Coaching;

public sealed class TradingJournalNormalizer : ITradingJournalNormalizer
{
    public TradingJournalNormalizationResult Normalize(TradingJournalAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var derivedFields = new Dictionary<string, string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var actualRiskPercentage = request.ActualRiskPercentage;
        var resultRMultiple = request.ResultRMultiple;

        if (actualRiskPercentage is null
            && request.RiskAmount is { } riskAmount
            && request.AccountBalance is > 0)
        {
            actualRiskPercentage = Round(riskAmount / request.AccountBalance.Value * 100);
            derivedFields[nameof(request.ActualRiskPercentage)] = actualRiskPercentage.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (resultRMultiple is null
            && request.ResultAmount is { } resultAmount
            && request.RiskAmount is > 0)
        {
            resultRMultiple = Round(resultAmount / request.RiskAmount.Value);
            derivedFields[nameof(request.ResultRMultiple)] = resultRMultiple.Value.ToString(CultureInfo.InvariantCulture);
        }

        var normalized = new TradingJournalAnalysisRequest(
            Normalize(request.JournalEntryId),
            Normalize(request.Instrument),
            Normalize(request.Market),
            NormalizeDirection(request.Direction),
            request.EntryPrice,
            request.ExitPrice,
            request.StopLoss,
            request.TakeProfit,
            request.PositionSize,
            request.AccountBalance,
            request.RiskAmount,
            request.PlannedRiskPercentage,
            actualRiskPercentage,
            request.ResultAmount,
            resultRMultiple,
            Normalize(request.SetupName),
            NormalizeTimeframe(request.Timeframe),
            Normalize(request.EntryReason),
            Normalize(request.ExitReason),
            Normalize(request.PlanBeforeTrade),
            Normalize(request.ExecutionNotes),
            Normalize(request.EmotionsBefore),
            Normalize(request.EmotionsDuring),
            Normalize(request.EmotionsAfter),
            NormalizeCollection(request.RulesRespected, lowerCase: false),
            NormalizeCollection(request.Mistakes, lowerCase: false),
            NormalizeCollection(request.Lessons, lowerCase: false),
            NormalizeCollection(request.Tags, lowerCase: true),
            request.OpenedAtUtc,
            request.ClosedAtUtc);

        var completeness = CalculateCompleteness(normalized);
        if (completeness < 60)
        {
            warnings.Add("Journal completeness is below the preferred analysis threshold.");
        }

        return new TradingJournalNormalizationResult(
            normalized,
            derivedFields,
            warnings,
            completeness);
    }

    private static string? NormalizeDirection(string? value)
    {
        var normalized = Normalize(value)?.ToLowerInvariant();
        return normalized switch
        {
            "buy" or "achat" => "long",
            "sell" or "vente" => "short",
            _ => normalized
        };
    }

    private static string? NormalizeTimeframe(string? value)
    {
        var normalized = Normalize(value)?.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return normalized switch
        {
            "M1" => "1M",
            "M5" => "5M",
            "M15" => "15M",
            "M30" => "30M",
            "H1" or "1HOUR" => "1H",
            "H4" or "4HOURS" => "4H",
            "D1" or "1DAY" => "1D",
            "W1" or "1WEEK" => "1W",
            _ => normalized
        };
    }

    private static IReadOnlyCollection<string> NormalizeCollection(
        IEnumerable<string> values,
        bool lowerCase)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Select(value => lowerCase ? value.ToLowerInvariant() : value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static decimal CalculateCompleteness(TradingJournalAnalysisRequest request)
    {
        var fields = new[]
        {
            HasText(request.Instrument), HasText(request.Direction), request.EntryPrice is not null,
            request.StopLoss is not null, request.RiskAmount is not null || request.ActualRiskPercentage is not null,
            request.AccountBalance is not null, request.PlannedRiskPercentage is not null, HasText(request.SetupName),
            HasText(request.Timeframe), HasText(request.EntryReason), HasText(request.ExitReason),
            HasText(request.PlanBeforeTrade), HasText(request.ExecutionNotes),
            HasText(request.EmotionsBefore) || HasText(request.EmotionsDuring) || HasText(request.EmotionsAfter),
            request.RulesRespected.Count > 0, request.Mistakes.Count > 0, request.Lessons.Count > 0,
            request.Tags.Count > 0, request.OpenedAtUtc is not null, request.ClosedAtUtc is not null
        };

        return Round(fields.Count(value => value) * 100m / fields.Length);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
    private static decimal Round(decimal value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);
}
