using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics;

public sealed class TradingJournalCollectionNormalizer : ITradingJournalCollectionNormalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ITradingJournalNormalizer _normalizer;

    public TradingJournalCollectionNormalizer(ITradingJournalNormalizer normalizer)
    {
        _normalizer = normalizer;
    }

    public TradingJournalCollectionNormalizationResult Normalize(TradingJournalAnalyticsValidationResult validationResult)
    {
        ArgumentNullException.ThrowIfNull(validationResult);
        var selected = new Dictionary<string, Candidate>(StringComparer.Ordinal);
        var duplicateCount = 0;
        var conflicts = new List<int>();
        var warnings = new List<string>();

        foreach (var validated in validationResult.ValidTrades.OrderBy(item => item.OriginalIndex))
        {
            var normalization = _normalizer.Normalize(validated.Trade);
            var contentChecksum = ContentChecksum(normalization.NormalizedRequest);
            var journalId = normalization.NormalizedRequest.JournalEntryId;
            var key = string.IsNullOrWhiteSpace(journalId)
                ? $"checksum:{contentChecksum}"
                : $"id:{journalId.Trim().ToLowerInvariant()}";
            var candidate = new Candidate(validated.OriginalIndex, normalization, key, contentChecksum);

            if (!selected.TryGetValue(key, out var existing))
            {
                selected.Add(key, candidate);
                continue;
            }

            duplicateCount++;
            var isConflict = key.StartsWith("id:", StringComparison.Ordinal)
                && !string.Equals(existing.ContentChecksum, candidate.ContentChecksum, StringComparison.Ordinal);
            if (isConflict)
            {
                conflicts.Add(existing.OriginalIndex);
                conflicts.Add(candidate.OriginalIndex);
                warnings.Add($"A conflicting duplicate JournalEntryId was detected at trade index {candidate.OriginalIndex}.");
            }

            if (candidate.Normalization.CompletenessScore > existing.Normalization.CompletenessScore)
            {
                selected[key] = candidate;
            }
        }

        var normalized = selected.Values
            .OrderBy(candidate => candidate.Normalization.NormalizedRequest.OpenedAtUtc
                ?? candidate.Normalization.NormalizedRequest.ClosedAtUtc
                ?? DateTimeOffset.MaxValue)
            .ThenBy(candidate => candidate.OriginalIndex)
            .Select(candidate => new NormalizedTradingJournalTrade(
                candidate.OriginalIndex,
                candidate.Normalization,
                candidate.Key))
            .ToArray();

        return new TradingJournalCollectionNormalizationResult(
            normalized,
            duplicateCount,
            conflicts,
            warnings,
            normalized.Sum(trade => trade.Normalization.DerivedFields.Count));
    }

    private static string ContentChecksum(TradingJournalAnalysisRequest trade)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            trade.Instrument,
            trade.Market,
            trade.Direction,
            EntryPrice = Invariant(trade.EntryPrice),
            ExitPrice = Invariant(trade.ExitPrice),
            StopLoss = Invariant(trade.StopLoss),
            TakeProfit = Invariant(trade.TakeProfit),
            PositionSize = Invariant(trade.PositionSize),
            AccountBalance = Invariant(trade.AccountBalance),
            RiskAmount = Invariant(trade.RiskAmount),
            PlannedRiskPercentage = Invariant(trade.PlannedRiskPercentage),
            ActualRiskPercentage = Invariant(trade.ActualRiskPercentage),
            ResultAmount = Invariant(trade.ResultAmount),
            ResultRMultiple = Invariant(trade.ResultRMultiple),
            trade.SetupName,
            trade.Timeframe,
            trade.EntryReason,
            trade.ExitReason,
            trade.PlanBeforeTrade,
            trade.ExecutionNotes,
            trade.EmotionsBefore,
            trade.EmotionsDuring,
            trade.EmotionsAfter,
            RulesRespected = trade.RulesRespected.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            Mistakes = trade.Mistakes.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            Lessons = trade.Lessons.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            Tags = trade.Tags.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            OpenedAtUtc = trade.OpenedAtUtc?.ToString("O", CultureInfo.InvariantCulture),
            ClosedAtUtc = trade.ClosedAtUtc?.ToString("O", CultureInfo.InvariantCulture)
        }, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string? Invariant(decimal? value) => value?.ToString(CultureInfo.InvariantCulture);

    private sealed record Candidate(
        int OriginalIndex,
        TradingJournalNormalizationResult Normalization,
        string Key,
        string ContentChecksum);
}
