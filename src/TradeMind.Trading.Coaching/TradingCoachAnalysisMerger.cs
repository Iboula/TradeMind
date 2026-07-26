namespace TradeMind.Trading.Coaching;

public sealed class TradingCoachAnalysisMerger : ITradingCoachAnalysisMerger
{
    public TradingCoachAnalysis Merge(
        TradingCoachAnalysis aiAnalysis,
        TradingCoachRuleAnalysisResult ruleAnalysis,
        TradingJournalNormalizationResult normalizationResult,
        TradeMetricsResult metrics,
        TradingCoachExecutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(aiAnalysis);
        ArgumentNullException.ThrowIfNull(ruleAnalysis);
        ArgumentNullException.ThrowIfNull(normalizationResult);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);

        var violations = MergeFindings(ruleAnalysis.Findings, aiAnalysis.RuleViolations);
        var priority = MergeFindings(
            ruleAnalysis.Findings.Where(finding => finding.Severity != TradingCoachFindingSeverity.Information),
            aiAnalysis.PriorityIssues);
        var findingCodes = violations.Concat(priority).Select(finding => finding.Code).ToHashSet(StringComparer.Ordinal);
        var actions = MergeActions(ruleAnalysis.Findings, aiAnalysis.RecommendedActions, findingCodes, options.MaximumRecommendations);
        var checklist = actions.Select(action => action.Action)
            .Concat(aiAnalysis.NextTradeChecklist.Where(value => !TradingCoachSafetyPolicy.ContainsProhibitedContent(value)))
            .Distinct(StringComparer.Ordinal)
            .Take(options.MaximumChecklistItems)
            .ToArray();
        var scores = options.IncludeDetailedScores
            ? ruleAnalysis.Scores
            : new TradingCoachScores(
                ruleAnalysis.Scores.PlanAdherence,
                ruleAnalysis.Scores.RiskDiscipline,
                ruleAnalysis.Scores.ExecutionQuality,
                ruleAnalysis.Scores.EmotionalControl,
                ruleAnalysis.Scores.JournalCompleteness,
                ruleAnalysis.Scores.OverallProcessQuality,
                null);

        var aiDuplicateExplanations = aiAnalysis.RuleViolations
            .Where(ai => ruleAnalysis.Findings.Any(rule => string.Equals(rule.Code, ai.Code, StringComparison.Ordinal)))
            .Select(ai => ai.Message);

        return new TradingCoachAnalysis(
            aiAnalysis.Summary,
            DataQuality(normalizationResult.CompletenessScore),
            ruleAnalysis.Strengths.Concat(aiAnalysis.Strengths).Distinct(StringComparer.Ordinal).ToArray(),
            violations,
            aiAnalysis.RiskObservations,
            aiAnalysis.ExecutionObservations.Concat(aiDuplicateExplanations).Distinct(StringComparer.Ordinal).ToArray(),
            aiAnalysis.PsychologyObservations,
            ruleAnalysis.MissingInformation.Concat(aiAnalysis.MissingInformation).Distinct(StringComparer.Ordinal).ToArray(),
            priority,
            actions,
            checklist,
            scores,
            TradingCoachConstants.Disclaimer,
            aiAnalysis.GeneratedAtUtc,
            TradingCoachConstants.AgentVersion,
            aiAnalysis.AnalysisId,
            metrics);
    }

    private static IReadOnlyCollection<TradingCoachFinding> MergeFindings(
        IEnumerable<TradingCoachFinding> deterministic,
        IEnumerable<TradingCoachFinding> ai)
    {
        var output = new List<TradingCoachFinding>();
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var finding in deterministic.Concat(ai))
        {
            if (codes.Add(finding.Code))
            {
                output.Add(finding);
            }
        }

        return output;
    }

    private static IReadOnlyCollection<TradingCoachRecommendedAction> MergeActions(
        IEnumerable<TradingCoachFinding> deterministicFindings,
        IEnumerable<TradingCoachRecommendedAction> aiActions,
        IReadOnlySet<string> findingCodes,
        int maximum)
    {
        var deterministic = deterministicFindings
            .Where(finding => finding.Severity != TradingCoachFindingSeverity.Information)
            .Select(finding => new TradingCoachRecommendedAction(
                $"ACTION_{finding.Code}",
                ActionFor(finding.Code),
                [finding.Code]));
        var safeAi = aiActions.Where(action =>
            action.RelatedFindingCodes.Any(findingCodes.Contains)
            && !TradingCoachSafetyPolicy.ContainsProhibitedContent(action.Action));

        return deterministic.Concat(safeAi)
            .DistinctBy(action => action.Code, StringComparer.Ordinal)
            .Take(maximum)
            .ToArray();
    }

    private static string ActionFor(string code) => code switch
    {
        "ACTUAL_RISK_ABOVE_PLANNED" => "Record planned and actual risk before reviewing the outcome.",
        "RISK_ABOVE_PROFILE_MAXIMUM" => "Add a pre-execution check against the personal risk limit.",
        "STOP_MISSING" => "Document the protective stop in the pre-trade plan.",
        "PLAN_MISSING" => "Write a concise plan before the next journaled execution.",
        "ENTRY_REASON_MISSING" => "Document the entry rationale and its plan criteria.",
        "EXIT_REASON_MISSING" => "Document the exit rationale independently of the result.",
        "RULES_NOT_RESPECTED" or "IGNORED_PLAN" => "Review the breached rule and define one observable prevention step.",
        "LESSON_MISSING" => "Record one process lesson that can be checked in the next review.",
        "JOURNAL_INCOMPLETE" => "Complete the missing journal fields before drawing broad conclusions.",
        "MOVED_STOP" => "Add stop changes and their stated rationale to the review checklist.",
        "FOMO" or "REVENGE_TRADING" or "OVERTRADING" or "STRONG_EMOTIONS" => "Use a brief pause-and-label routine before the next planned decision.",
        _ => "Review this process finding and define one observable corrective action."
    };

    private static string DataQuality(decimal completenessScore) => completenessScore switch
    {
        >= 80 => "high",
        >= 60 => "moderate",
        _ => "limited"
    };
}
