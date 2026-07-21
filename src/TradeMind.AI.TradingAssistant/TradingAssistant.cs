using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradeMind.AI.TradingWorkspace.Domain;

namespace TradeMind.AI.TradingAssistant;

public enum TradingAssistantIntent
{
    Summary,
    Decision,
    Risk,
    Plan,
    NextActions,
    Issues,
    Explain
}

public sealed record TradingAssistantRequest
{
    public TradingAssistantRequest(
        TradingWorkspaceResult workspace,
        string question,
        string locale = "en",
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        Workspace = workspace;
        Question = question.Trim();
        Locale = NormalizeLocale(locale);
        CorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId.Trim();
    }

    public TradingWorkspaceResult Workspace { get; }
    public string Question { get; }
    public string Locale { get; }
    public string CorrelationId { get; }

    private static string NormalizeLocale(string locale) =>
        string.Equals(locale?.Trim(), "fr", StringComparison.OrdinalIgnoreCase) ? "fr" : "en";
}

public sealed record TradingAssistantCitation(string Source, string Value);

public sealed record TradingAssistantRecommendation(
    string Code,
    string Description,
    bool Blocking,
    WorkspaceNextActionType? NextAction = null);

public sealed record TradingAssistantResponse
{
    public TradingAssistantResponse(
        TradingAssistantIntent intent,
        string answer,
        IReadOnlyCollection<string> facts,
        IReadOnlyCollection<TradingAssistantRecommendation> recommendations,
        IReadOnlyCollection<TradingAssistantCitation> citations,
        string disclaimer,
        string correlationId,
        DateTimeOffset generatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(answer);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(recommendations);
        ArgumentNullException.ThrowIfNull(citations);
        ArgumentException.ThrowIfNullOrWhiteSpace(disclaimer);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        Intent = intent;
        Answer = answer.Trim();
        Facts = Array.AsReadOnly(facts.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray());
        Recommendations = Array.AsReadOnly(recommendations.ToArray());
        Citations = Array.AsReadOnly(citations.ToArray());
        Disclaimer = disclaimer.Trim();
        CorrelationId = correlationId.Trim();
        GeneratedAtUtc = generatedAtUtc;
    }

    public TradingAssistantIntent Intent { get; }
    public string Answer { get; }
    public IReadOnlyList<string> Facts { get; }
    public IReadOnlyList<TradingAssistantRecommendation> Recommendations { get; }
    public IReadOnlyList<TradingAssistantCitation> Citations { get; }
    public string Disclaimer { get; }
    public string CorrelationId { get; }
    public DateTimeOffset GeneratedAtUtc { get; }
}

public interface ITradingAssistant
{
    Task<TradingAssistantResponse> AnswerAsync(
        TradingAssistantRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITradingAssistantIntentClassifier
{
    TradingAssistantIntent Classify(string question);
}

public sealed class DeterministicTradingAssistantIntentClassifier : ITradingAssistantIntentClassifier
{
    public TradingAssistantIntent Classify(string question)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        var value = question.Trim().ToLowerInvariant();

        if (ContainsAny(value, "risk", "risque", "drawdown", "exposure", "exposition"))
            return TradingAssistantIntent.Risk;
        if (ContainsAny(value, "plan", "entry", "entrée", "stop", "target", "take profit", "objectif"))
            return TradingAssistantIntent.Plan;
        if (ContainsAny(value, "decision", "décision", "long", "short", "buy", "sell", "acheter", "vendre"))
            return TradingAssistantIntent.Decision;
        if (ContainsAny(value, "next", "prochaine", "action", "quoi faire", "what should"))
            return TradingAssistantIntent.NextActions;
        if (ContainsAny(value, "issue", "error", "warning", "problème", "erreur", "alerte", "bloqué"))
            return TradingAssistantIntent.Issues;
        if (ContainsAny(value, "why", "explain", "pourquoi", "explique"))
            return TradingAssistantIntent.Explain;

        return TradingAssistantIntent.Summary;
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.Ordinal));
}

public sealed class DeterministicTradingAssistant : ITradingAssistant
{
    private readonly ITradingAssistantIntentClassifier _classifier;
    private readonly ILogger<DeterministicTradingAssistant> _logger;

    public DeterministicTradingAssistant(
        ITradingAssistantIntentClassifier classifier,
        ILogger<DeterministicTradingAssistant> logger)
    {
        _classifier = classifier;
        _logger = logger;
    }

    public Task<TradingAssistantResponse> AnswerAsync(
        TradingAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var intent = _classifier.Classify(request.Question);
        var workspace = request.Workspace;
        var french = request.Locale == "fr";

        _logger.LogInformation(
            "Trading assistant request {CorrelationId} classified as {Intent} for workspace {WorkspaceId}",
            request.CorrelationId,
            intent,
            workspace.WorkspaceId);

        var facts = BuildFacts(workspace, intent, french);
        var recommendations = BuildRecommendations(workspace, french);
        var citations = BuildCitations(workspace);
        var answer = BuildAnswer(workspace, intent, facts, french);
        var disclaimer = french
            ? "Réponse informative et non exécutable. Elle ne constitue pas un conseil financier ni un ordre de trading."
            : "Informational, non-executable response. It is not financial advice or a trading order.";

        return Task.FromResult(new TradingAssistantResponse(
            intent,
            answer,
            facts,
            recommendations,
            citations,
            disclaimer,
            request.CorrelationId,
            DateTimeOffset.UtcNow));
    }

    private static IReadOnlyCollection<string> BuildFacts(
        TradingWorkspaceResult workspace,
        TradingAssistantIntent intent,
        bool french)
    {
        var facts = new List<string>();
        var statusLabel = french ? "Statut" : "Status";
        var stateLabel = french ? "État" : "State";
        facts.Add($"{statusLabel}: {workspace.Status}.");
        facts.Add($"{stateLabel}: {workspace.State}.");
        facts.Add($"{(french ? "Complétude" : "Completeness")}: {workspace.Completeness.Score:0.#}%.");

        if (workspace.Instrument is not null)
            facts.Add($"{(french ? "Instrument" : "Instrument")}: {workspace.Instrument}.");
        if (workspace.Timeframe is not null)
            facts.Add($"Timeframe: {workspace.Timeframe}.");

        if (intent is TradingAssistantIntent.Decision or TradingAssistantIntent.Explain or TradingAssistantIntent.Summary)
        {
            facts.Add(workspace.Decision.IsAvailable
                ? $"{(french ? "Décision" : "Decision")}: {workspace.Decision.Type}; {(french ? "confiance" : "confidence")}: {FormatPercent(workspace.Decision.Confidence)}."
                : french ? "Décision indisponible." : "Decision unavailable.");
        }

        if (intent is TradingAssistantIntent.Risk or TradingAssistantIntent.Explain or TradingAssistantIntent.Summary)
        {
            facts.Add(workspace.Risk.IsAvailable
                ? $"{(french ? "Verdict risque" : "Risk verdict")}: {workspace.Risk.Verdict}; {(french ? "quantité" : "quantity")}: {workspace.Risk.Quantity?.ToString() ?? "n/a"}."
                : french ? "Évaluation du risque indisponible." : "Risk assessment unavailable.");
        }

        if (intent is TradingAssistantIntent.Plan or TradingAssistantIntent.Explain or TradingAssistantIntent.Summary)
        {
            facts.Add(workspace.Plan.IsAvailable
                ? $"{(french ? "Plan" : "Plan")}: {workspace.Plan.Type}; {(french ? "direction" : "direction")}: {workspace.Plan.Direction}; {(french ? "expiré" : "expired")}: {workspace.Plan.IsExpired}."
                : french ? "Plan de trading indisponible." : "Trading plan unavailable.");
        }

        if (intent is TradingAssistantIntent.Issues or TradingAssistantIntent.Explain or TradingAssistantIntent.Summary)
        {
            facts.Add($"{(french ? "Bloqueurs" : "Blockers")}: {workspace.Blockers.Count}; {(french ? "avertissements" : "warnings")}: {workspace.Warnings.Count}; {(french ? "erreurs" : "errors")}: {workspace.Errors.Count}.");
        }

        return new ReadOnlyCollection<string>(facts);
    }

    private static IReadOnlyCollection<TradingAssistantRecommendation> BuildRecommendations(
        TradingWorkspaceResult workspace,
        bool french)
    {
        var values = workspace.NextActions
            .Select(action => new TradingAssistantRecommendation(
                action.Type.ToString(),
                action.Reason,
                action.Blocking,
                action.Type))
            .ToList();

        if (workspace.Freshness.IsStale || workspace.Freshness.PlanExpired)
        {
            values.Add(new TradingAssistantRecommendation(
                "REFRESH_WORKSPACE",
                french ? "Rafraîchir le workspace avant toute décision." : "Refresh the workspace before making any decision.",
                true,
                WorkspaceNextActionType.RefreshContext));
        }

        if (workspace.Blockers.Count > 0)
        {
            values.Add(new TradingAssistantRecommendation(
                "RESOLVE_BLOCKERS",
                french ? "Résoudre les bloqueurs avant de poursuivre." : "Resolve blockers before proceeding.",
                true));
        }

        return new ReadOnlyCollection<TradingAssistantRecommendation>(
            values.GroupBy(value => value.Code, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray());
    }

    private static IReadOnlyCollection<TradingAssistantCitation> BuildCitations(TradingWorkspaceResult workspace)
    {
        var citations = workspace.Traces
            .Select(trace => new TradingAssistantCitation(trace.Origin, $"{trace.Kind}:{trace.Value}"))
            .ToList();

        citations.Add(new TradingAssistantCitation("workspace", workspace.WorkspaceId.ToString()));
        return new ReadOnlyCollection<TradingAssistantCitation>(citations);
    }

    private static string BuildAnswer(
        TradingWorkspaceResult workspace,
        TradingAssistantIntent intent,
        IReadOnlyCollection<string> facts,
        bool french)
    {
        var prefix = intent switch
        {
            TradingAssistantIntent.Risk => french ? "Évaluation du risque" : "Risk assessment",
            TradingAssistantIntent.Plan => french ? "Plan de trading" : "Trading plan",
            TradingAssistantIntent.Decision => french ? "Décision de trading" : "Trading decision",
            TradingAssistantIntent.NextActions => french ? "Prochaines actions" : "Next actions",
            TradingAssistantIntent.Issues => french ? "Problèmes du workspace" : "Workspace issues",
            TradingAssistantIntent.Explain => french ? "Explication du workspace" : "Workspace explanation",
            _ => french ? "Résumé du workspace" : "Workspace summary"
        };

        return $"{prefix}: {workspace.Summary} {string.Join(" ", facts)}";
    }

    private static string FormatPercent(double? value) =>
        value is null ? "n/a" : $"{value.Value * 100:0.#}%";
}

public static class TradingAssistantDependencyInjection
{
    public static IServiceCollection AddTradingAssistant(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ITradingAssistantIntentClassifier, DeterministicTradingAssistantIntentClassifier>();
        services.AddScoped<ITradingAssistant, DeterministicTradingAssistant>();
        return services;
    }
}
