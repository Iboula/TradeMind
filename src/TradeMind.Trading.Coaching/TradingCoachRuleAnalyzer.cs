namespace TradeMind.Trading.Coaching;

public sealed class TradingCoachRuleAnalyzer : ITradingCoachRuleAnalyzer
{
    private static readonly string[] StrongEmotionTerms =
    [
        "panic", "panique", "anxious", "anxiete", "fear", "peur", "angry", "colere",
        "euphoria", "euphorie", "stress", "frustrated", "frustre"
    ];

    private readonly ITradingBehaviorPatternDetector _patternDetector;
    private readonly ITradingCoachScoreCalculator _scoreCalculator;

    public TradingCoachRuleAnalyzer(
        ITradingBehaviorPatternDetector patternDetector,
        ITradingCoachScoreCalculator scoreCalculator)
    {
        _patternDetector = patternDetector;
        _scoreCalculator = scoreCalculator;
    }

    public TradingCoachRuleAnalysisResult Analyze(
        TradingJournalNormalizationResult normalizationResult,
        TradeMetricsResult metrics,
        TradingCoachProfile profile)
    {
        ArgumentNullException.ThrowIfNull(normalizationResult);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(profile);
        var request = normalizationResult.NormalizedRequest;
        var findings = new List<TradingCoachFinding>();
        var missing = new List<string>();
        var strengths = new List<string>();

        AddRiskFindings(request, metrics, profile, findings);
        AddDocumentationFindings(request, normalizationResult, findings, missing);
        AddBehaviorFindings(request, findings);
        AddMetricConsistencyFindings(metrics, findings);

        var scores = _scoreCalculator.Calculate(normalizationResult, metrics, findings);
        AddOutcomeProcessFinding(request, scores, findings);
        scores = _scoreCalculator.Calculate(normalizationResult, metrics, findings);
        AddStrengths(request, normalizationResult, findings, strengths);

        return new TradingCoachRuleAnalysisResult(
            findings.OrderByDescending(finding => finding.Severity).ThenBy(finding => finding.Code, StringComparer.Ordinal).ToArray(),
            strengths,
            missing,
            scores);
    }

    private static void AddRiskFindings(
        TradingJournalAnalysisRequest request,
        TradeMetricsResult metrics,
        TradingCoachProfile profile,
        ICollection<TradingCoachFinding> findings)
    {
        if (metrics.ComputedMetrics.TryGetValue(TradeMetricNames.PlannedActualRiskDelta, out var delta) && delta > 0)
        {
            findings.Add(Finding(
                "ACTUAL_RISK_ABOVE_PLANNED", "risk",
                "Actual risk was above the explicitly planned risk.",
                TradingCoachFindingSeverity.Critical,
                nameof(request.ActualRiskPercentage), nameof(request.PlannedRiskPercentage)));
        }

        if (profile.MaximumRiskPerTrade is { } maximum
            && request.ActualRiskPercentage is { } actual
            && actual > maximum)
        {
            findings.Add(Finding(
                "RISK_ABOVE_PROFILE_MAXIMUM", "risk",
                "Actual risk exceeded the coaching profile maximum.",
                TradingCoachFindingSeverity.Critical,
                nameof(request.ActualRiskPercentage)));
        }
    }

    private static void AddDocumentationFindings(
        TradingJournalAnalysisRequest request,
        TradingJournalNormalizationResult normalization,
        ICollection<TradingCoachFinding> findings,
        ICollection<string> missing)
    {
        AddMissing(request.StopLoss is null, "STOP_MISSING", "risk", "A protective stop was not documented.", nameof(request.StopLoss), findings, missing);
        AddMissing(string.IsNullOrWhiteSpace(request.PlanBeforeTrade), "PLAN_MISSING", "plan", "A pre-trade plan was not documented.", nameof(request.PlanBeforeTrade), findings, missing);
        AddMissing(string.IsNullOrWhiteSpace(request.EntryReason), "ENTRY_REASON_MISSING", "execution", "The entry reason was not documented.", nameof(request.EntryReason), findings, missing);

        var hasExit = request.ExitPrice is not null || request.ClosedAtUtc is not null || request.ResultAmount is not null;
        AddMissing(hasExit && string.IsNullOrWhiteSpace(request.ExitReason), "EXIT_REASON_MISSING", "execution", "The exit was not documented.", nameof(request.ExitReason), findings, missing);

        if (request.RulesRespected.Any(IndicatesRuleViolation))
        {
            findings.Add(Finding(
                "RULES_NOT_RESPECTED", "plan", "The journal explicitly reports a rule that was not respected.",
                TradingCoachFindingSeverity.Critical, nameof(request.RulesRespected)));
        }

        if (request.Lessons.Count == 0)
        {
            findings.Add(Finding(
                "LESSON_MISSING", "journal", "No lesson was documented for the review cycle.",
                TradingCoachFindingSeverity.Warning, nameof(request.Lessons)));
            missing.Add(nameof(request.Lessons));
        }

        if (normalization.CompletenessScore < 60)
        {
            findings.Add(Finding(
                "JOURNAL_INCOMPLETE", "journal", "The journal is too incomplete for high-confidence coaching.",
                TradingCoachFindingSeverity.Warning));
        }
    }

    private void AddBehaviorFindings(
        TradingJournalAnalysisRequest request,
        ICollection<TradingCoachFinding> findings)
    {
        var patterns = _patternDetector.Detect(request);
        foreach (var pattern in patterns)
        {
            findings.Add(Finding(
                pattern,
                pattern is "OVERSIZED_POSITION" ? "risk" : "psychology",
                BehaviorMessage(pattern),
                pattern is "REVENGE_TRADING" or "MOVED_STOP" or "OVERSIZED_POSITION"
                    ? TradingCoachFindingSeverity.Critical
                    : TradingCoachFindingSeverity.Warning,
                nameof(request.ExecutionNotes), nameof(request.Mistakes), nameof(request.EmotionsDuring)));
        }

        if (ContainsStrongEmotion(request))
        {
            findings.Add(Finding(
                "STRONG_EMOTIONS", "psychology", "Strong emotions were explicitly documented.",
                TradingCoachFindingSeverity.Warning,
                nameof(request.EmotionsBefore), nameof(request.EmotionsDuring), nameof(request.EmotionsAfter)));
        }

        if (patterns.Count > 0 && request.Mistakes.Count == 0)
        {
            findings.Add(Finding(
                "MISTAKES_NOT_DOCUMENTED", "journal",
                "Behavioral evidence is present but the mistakes field is empty.",
                TradingCoachFindingSeverity.Warning,
                nameof(request.Mistakes)));
        }
    }

    private static void AddMetricConsistencyFindings(
        TradeMetricsResult metrics,
        ICollection<TradingCoachFinding> findings)
    {
        if (metrics.Warnings.Any(warning => warning.Contains("R multiple", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(Finding(
                "R_MULTIPLE_INCONSISTENT", "data-quality",
                "The explicit R multiple conflicts with the available deterministic calculation inputs.",
                TradingCoachFindingSeverity.Warning,
                nameof(TradingJournalAnalysisRequest.ResultRMultiple)));
        }
    }

    private static void AddOutcomeProcessFinding(
        TradingJournalAnalysisRequest request,
        TradingCoachScores scores,
        ICollection<TradingCoachFinding> findings)
    {
        if (request.ResultAmount > 0 && scores.OverallProcessQuality < 70)
        {
            findings.Add(Finding(
                "POSITIVE_RESULT_WEAK_PROCESS", "process",
                "A positive result does not offset the detected process weaknesses.",
                TradingCoachFindingSeverity.Warning,
                nameof(request.ResultAmount)));
        }
        else if (request.ResultAmount < 0 && scores.OverallProcessQuality >= 70)
        {
            findings.Add(Finding(
                "NEGATIVE_RESULT_GOOD_PROCESS", "process",
                "A negative result can coexist with a well-documented and disciplined process.",
                TradingCoachFindingSeverity.Information,
                nameof(request.ResultAmount)));
        }
    }

    private static void AddStrengths(
        TradingJournalAnalysisRequest request,
        TradingJournalNormalizationResult normalization,
        IReadOnlyCollection<TradingCoachFinding> findings,
        ICollection<string> strengths)
    {
        var codes = findings.Select(finding => finding.Code).ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(request.PlanBeforeTrade) && !codes.Contains("IGNORED_PLAN"))
        {
            strengths.Add("A pre-trade plan was documented.");
        }

        if (!codes.Contains("ACTUAL_RISK_ABOVE_PLANNED") && !codes.Contains("RISK_ABOVE_PROFILE_MAXIMUM") && request.ActualRiskPercentage is not null)
        {
            strengths.Add("Documented risk remained within the supplied limits.");
        }

        if (request.Lessons.Count > 0)
        {
            strengths.Add("The journal captures at least one lesson.");
        }

        if (normalization.CompletenessScore >= 70)
        {
            strengths.Add("The journal provides a useful level of process detail.");
        }
    }

    private static void AddMissing(
        bool condition,
        string code,
        string category,
        string message,
        string field,
        ICollection<TradingCoachFinding> findings,
        ICollection<string> missing)
    {
        if (!condition)
        {
            return;
        }

        findings.Add(Finding(code, category, message, TradingCoachFindingSeverity.Warning, field));
        missing.Add(field);
    }

    private static bool IndicatesRuleViolation(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized.Contains("not respected", StringComparison.Ordinal)
            || normalized.Contains("not followed", StringComparison.Ordinal)
            || normalized.Contains("ignored", StringComparison.Ordinal)
            || normalized.Contains("non respecte", StringComparison.Ordinal)
            || normalized.Contains("pas respecte", StringComparison.Ordinal)
            || normalized.Contains("ignore", StringComparison.Ordinal);
    }

    private static bool ContainsStrongEmotion(TradingJournalAnalysisRequest request)
    {
        var text = string.Join(' ', request.EmotionsBefore, request.EmotionsDuring, request.EmotionsAfter).ToLowerInvariant();
        return StrongEmotionTerms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static TradingCoachFinding Finding(
        string code,
        string category,
        string message,
        TradingCoachFindingSeverity severity,
        params string[] evidenceFields) =>
        new(code, category, message, severity, true, evidenceFields);

    private static string BehaviorMessage(string pattern) => pattern switch
    {
        "FOMO" => "The journal contains language associated with fear of missing out.",
        "REVENGE_TRADING" => "The journal contains language associated with revenge trading.",
        "OVERTRADING" => "The journal contains language associated with overtrading.",
        "HESITATION" => "The journal contains language associated with hesitation.",
        "IMPULSIVE_ENTRY" => "The journal contains language associated with an impulsive entry.",
        "MOVED_STOP" => "The journal reports that the protective stop was moved.",
        "EARLY_EXIT" => "The journal reports an early exit.",
        "IGNORED_PLAN" => "The journal reports that the plan was ignored.",
        "OVERSIZED_POSITION" => "The journal reports an oversized position.",
        _ => "The journal contains a recognized behavioral pattern."
    };
}
