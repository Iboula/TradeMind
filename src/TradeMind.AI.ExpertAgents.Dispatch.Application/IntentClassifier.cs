using TradeMind.AI.ExpertAgents.Dispatch.Domain;

namespace TradeMind.AI.ExpertAgents.Dispatch.Application;

public interface IAnalysisIntentClassifier
{
    AnalysisIntentClassificationResult Classify(
        AgentDispatchRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RuleBasedAnalysisIntentClassifier : IAnalysisIntentClassifier
{
    private static readonly IReadOnlyList<IntentRule> Rules =
    [
        new(
            new AnalysisIntent("technical-analysis", AnalysisIntentKind.TechnicalAnalysis, ["technical", "technique", "chart", "trend", "structure", "price", "support", "resistance", "ict", "smc", "wyckoff"]),
            ["technical", "technique", "chart", "trend", "structure", "price", "support", "resistance", "ict", "smc", "wyckoff", "analyse", "analyze", "analysis", "analyse technique", "technical analysis"]),
        new(
            new AnalysisIntent("risk-assessment", AnalysisIntentKind.RiskAssessment, ["risk", "risque", "drawdown", "stop", "exposure", "exposition"]),
            ["risk", "risque", "drawdown", "stop", "exposure", "exposition", "position size", "sizing"]),
        new(
            new AnalysisIntent("educational-explanation", AnalysisIntentKind.EducationalExplanation, ["explain", "expliquer", "learn", "apprendre", "why", "pourquoi"]),
            ["explain", "expliquer", "learn", "apprendre", "why", "pourquoi", "educational", "pédagogique"]),
        new(
            new AnalysisIntent("macro-analysis", AnalysisIntentKind.MacroAnalysis, ["macro", "inflation", "rates", "taux", "central bank", "banque centrale", "news"]),
            ["macro", "inflation", "rates", "taux", "central bank", "banque centrale", "news", "economic"]),
        new(
            new AnalysisIntent("validation", AnalysisIntentKind.Validation, ["validate", "validation", "check", "vérifie", "confirm", "confirme"]),
            ["validate", "validation", "check", "vérifie", "confirm", "confirme", "review"]),
        new(
            new AnalysisIntent("market-overview", AnalysisIntentKind.MarketOverview, ["overview", "résumé", "summary", "market", "marché"]),
            ["overview", "résumé", "summary", "market", "marché", "current state", "état"])
    ];

    public AnalysisIntentClassificationResult Classify(
        AgentDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.ExplicitIntent is not null)
        {
            return new AnalysisIntentClassificationResult(
                request.ExplicitIntent,
                100,
                false,
                false,
                false,
                ["The caller supplied an explicit analysis intent."]);
        }

        var input = $"{request.Objective} {request.Question}".Trim().ToLowerInvariant();
        var scored = Rules
            .Select(rule =>
            {
                var matched = rule.Keywords
                    .Where(keyword => input.Contains(keyword, StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                return new ScoredIntent(rule.Intent, matched);
            })
            .Where(item => item.Matches.Count > 0)
            .OrderByDescending(item => item.Matches.Count)
            .ThenBy(item => item.Intent.Key, StringComparer.Ordinal)
            .ToArray();

        if (scored.Length == 0)
        {
            var fallback = request.FallbackMode != AgentDispatchFallbackMode.None;
            return new AnalysisIntentClassificationResult(
                AnalysisIntent.Unknown,
                0,
                false,
                !fallback,
                fallback,
                [fallback
                    ? "No rule matched; the configured fallback keeps the intent unknown."
                    : "No intent rule matched the objective or question; clarification is required."]);
        }

        var top = scored[0];
        var second = scored.Length > 1 ? scored[1] : null;
        var ambiguous = second is not null && second.Matches.Count == top.Matches.Count;
        var confidence = Math.Min(100, 40 + (top.Matches.Count * 15) - (ambiguous ? 10 : 0));
        var alternatives = scored
            .Skip(1)
            .Take(3)
            .Select(item => item.Intent)
            .ToArray();
        var reasons = top.Matches
            .Select(match => $"Matched rule keyword '{match}'.")
            .ToArray();
        if (ambiguous)
        {
            reasons = reasons.Append("Multiple intents have the same deterministic rule score.").ToArray();
        }

        return new AnalysisIntentClassificationResult(
            top.Intent,
            confidence,
            ambiguous,
            ambiguous && request.FallbackMode == AgentDispatchFallbackMode.None,
            request.FallbackMode != AgentDispatchFallbackMode.None && ambiguous,
            reasons,
            alternatives);
    }

    private sealed record IntentRule(AnalysisIntent Intent, IReadOnlyCollection<string> Keywords);

    private sealed record ScoredIntent(AnalysisIntent Intent, IReadOnlyCollection<string> Matches);
}
