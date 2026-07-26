using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace TradeMind.Trading.Coaching;

public sealed class TradingCoachSafetyFilter : ITradingCoachSafetyFilter
{
    private readonly TradingCoachSafetyOptions _options;

    public TradingCoachSafetyFilter(IOptions<TradingCoachSafetyOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public TradingCoachAnalysis Validate(TradingCoachAnalysis analysis, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        if (!AllText(analysis).Any(TradingCoachSafetyPolicy.ContainsProhibitedContent))
        {
            return analysis;
        }

        if (_options.FailClosed)
        {
            throw new TradingCoachSafetyException(analysis.AnalysisId, correlationId);
        }

        return Clean(analysis);
    }

    private static TradingCoachAnalysis Clean(TradingCoachAnalysis analysis) => new(
        SafeOrRemoved(analysis.Summary),
        SafeOrRemoved(analysis.DataQuality),
        Safe(analysis.Strengths),
        analysis.RuleViolations.Where(IsSafe).ToArray(),
        Safe(analysis.RiskObservations),
        Safe(analysis.ExecutionObservations),
        Safe(analysis.PsychologyObservations),
        Safe(analysis.MissingInformation),
        analysis.PriorityIssues.Where(IsSafe).ToArray(),
        analysis.RecommendedActions.Where(IsSafe).ToArray(),
        Safe(analysis.NextTradeChecklist),
        analysis.Scores,
        SafeOrRemoved(analysis.Disclaimer),
        analysis.GeneratedAtUtc,
        analysis.AgentVersion,
        analysis.AnalysisId,
        analysis.Metrics);

    private static IEnumerable<string> AllText(TradingCoachAnalysis analysis)
    {
        yield return analysis.Summary;
        yield return analysis.DataQuality;
        yield return analysis.Disclaimer;
        foreach (var value in analysis.Strengths
            .Concat(analysis.RiskObservations)
            .Concat(analysis.ExecutionObservations)
            .Concat(analysis.PsychologyObservations)
            .Concat(analysis.MissingInformation)
            .Concat(analysis.NextTradeChecklist))
        {
            yield return value;
        }

        foreach (var finding in analysis.RuleViolations.Concat(analysis.PriorityIssues))
        {
            yield return finding.Message;
        }

        foreach (var action in analysis.RecommendedActions)
        {
            yield return action.Action;
        }

        foreach (var explanation in analysis.Scores.Explanations)
        {
            foreach (var factor in explanation.Factors)
            {
                yield return factor;
            }
        }
    }

    private static IReadOnlyCollection<string> Safe(IEnumerable<string> values) =>
        values.Where(value => !TradingCoachSafetyPolicy.ContainsProhibitedContent(value)).ToArray();

    private static bool IsSafe(TradingCoachFinding finding) =>
        !TradingCoachSafetyPolicy.ContainsProhibitedContent(finding.Message);

    private static bool IsSafe(TradingCoachRecommendedAction action) =>
        !TradingCoachSafetyPolicy.ContainsProhibitedContent(action.Action);

    private static string SafeOrRemoved(string value) =>
        TradingCoachSafetyPolicy.ContainsProhibitedContent(value) ? "Content removed by safety policy." : value;
}

public static class TradingCoachSafetyPolicy
{
    private static readonly Regex[] ProhibitedPatterns =
    [
        Create(@"\bbuy\s+now\b|\bachete[rz]?\s+maintenant\b"),
        Create(@"\bsell\s+now\b|\bvendez?\s+maintenant\b"),
        Create(@"\benter\s+(?:a\s+)?long\b|\bentre[rz]?\s+(?:en\s+)?long\b"),
        Create(@"\benter\s+(?:a\s+)?short\b|\bentre[rz]?\s+(?:en\s+)?short\b"),
        Create(@"\bguaranteed\s+(?:profit|win\s*rate|return)\b|\b(?:profit|gain|rendement)\s+garanti\b"),
        Create(@"\buse\s+(?:the\s+)?maximum\s+leverage\b|\butilise[rz]?\s+(?:le\s+)?levier\s+maximal\b"),
        Create(@"\b(?:price|prix)\b.{0,30}\b(?:will|va|sera|atteindra|reach)\b.{0,20}\d"),
        Create(@"\b(?:place|execute|submit)\s+(?:the\s+|an?\s+)?(?:buy|sell|market|limit)?\s*order\b|\b(?:place[rz]?|execute[rz]?)\s+(?:un\s+)?ordre\b"),
        Create(@"\b(?:promise|promesse)\b.{0,20}\b(?:return|profit|gain|rendement)\b"),
        Create(@"\b(?:recommend|recommande[rz]?|should)\b.{0,20}\b(?:buy|sell|acheter|vendre)\b"),
        Create(@"\b(?:buy|sell|acheter|vendre)\s+(?:[a-z]{2,10}(?:usd|usdt)?|this\s+asset|cet\s+actif)\b"),
        Create(@"\b[a-z]{2,12}\b.{0,20}\b(?:will|va)\s+(?:rise|fall|reach|monter|baisser|atteindre)\b"),
        Create(@"\b(?:will|va|garanti\s+de)\s+(?:return|yield|profit|gain|rapporter|produire)\b"),
        Create(@"\b(?:certain|sure|assured|sans\s+risque)\b.{0,20}\b(?:profit|return|yield|gain|rendement)\b")
    ];

    public static bool ContainsProhibitedContent(string value) =>
        ProhibitedPatterns.Any(pattern => pattern.IsMatch(value));

    private static Regex Create(string pattern) => new(
        pattern,
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
}
