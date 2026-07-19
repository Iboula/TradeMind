using System.Globalization;
using System.Text;

namespace TradeMind.Trading.Coaching;

public sealed class TradingBehaviorPatternDetector : ITradingBehaviorPatternDetector
{
    private static readonly IReadOnlyDictionary<string, string[]> Patterns = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["FOMO"] = ["fomo", "fear of missing out", "peur de manquer"],
        ["REVENGE_TRADING"] = ["revenge trading", "revenge trade", "trading de revanche", "trade de revanche", "me refaire", "recuperer mes pertes"],
        ["OVERTRADING"] = ["overtrading", "over trading", "surtrading", "sur-trading", "trop de trades"],
        ["HESITATION"] = ["hesitation", "hesite", "indecis", "indecision"],
        ["IMPULSIVE_ENTRY"] = ["impulsive entry", "entered impulsively", "entree impulsive", "entre impulsivement", "entree precipitee"],
        ["MOVED_STOP"] = ["moved stop", "moved my stop", "widened stop", "deplace le stop", "deplace mon stop", "bouge mon stop", "elargi le stop"],
        ["EARLY_EXIT"] = ["early exit", "closed early", "exited early", "sortie trop tot", "sorti trop tot", "ferme trop tot"],
        ["IGNORED_PLAN"] = ["ignored plan", "ignored my plan", "did not follow plan", "hors plan", "ignore le plan", "pas respecte le plan"],
        ["OVERSIZED_POSITION"] = ["oversized position", "position too large", "oversized", "taille excessive", "position trop grosse", "position surdimensionnee"]
    };

    public IReadOnlyList<string> Detect(TradingJournalAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = Normalize(string.Join(
            ' ',
            TextValues(request).Where(value => !string.IsNullOrWhiteSpace(value))));

        return Array.AsReadOnly(Patterns
            .Where(pattern => pattern.Value.Any(candidate => text.Contains(candidate, StringComparison.Ordinal)))
            .Select(pattern => pattern.Key)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray());
    }

    private static IEnumerable<string?> TextValues(TradingJournalAnalysisRequest request)
    {
        yield return request.EntryReason;
        yield return request.ExitReason;
        yield return request.PlanBeforeTrade;
        yield return request.ExecutionNotes;
        yield return request.EmotionsBefore;
        yield return request.EmotionsDuring;
        yield return request.EmotionsAfter;
        foreach (var value in request.RulesRespected.Concat(request.Mistakes).Concat(request.Lessons).Concat(request.Tags))
        {
            yield return value;
        }
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
