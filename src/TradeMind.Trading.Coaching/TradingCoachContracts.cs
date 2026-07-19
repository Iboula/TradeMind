using System.Collections.ObjectModel;

namespace TradeMind.Trading.Coaching;

public static class TradingCoachConstants
{
    public const string AgentId = "trading-coach";
    public const string AgentVersion = "1.0.0";
    public const string Scenario = "TradingCoachAnalysis";
    public const string Disclaimer = "Educational process coaching only. This analysis is not financial advice, a market prediction, or an instruction to trade.";
}

public sealed record TradingJournalAnalysisRequest
{
    public TradingJournalAnalysisRequest(
        string? journalEntryId = null,
        string? instrument = null,
        string? market = null,
        string? direction = null,
        decimal? entryPrice = null,
        decimal? exitPrice = null,
        decimal? stopLoss = null,
        decimal? takeProfit = null,
        decimal? positionSize = null,
        decimal? accountBalance = null,
        decimal? riskAmount = null,
        decimal? plannedRiskPercentage = null,
        decimal? actualRiskPercentage = null,
        decimal? resultAmount = null,
        decimal? resultRMultiple = null,
        string? setupName = null,
        string? timeframe = null,
        string? entryReason = null,
        string? exitReason = null,
        string? planBeforeTrade = null,
        string? executionNotes = null,
        string? emotionsBefore = null,
        string? emotionsDuring = null,
        string? emotionsAfter = null,
        IReadOnlyCollection<string>? rulesRespected = null,
        IReadOnlyCollection<string>? mistakes = null,
        IReadOnlyCollection<string>? lessons = null,
        IReadOnlyCollection<string>? tags = null,
        DateTimeOffset? openedAtUtc = null,
        DateTimeOffset? closedAtUtc = null)
    {
        JournalEntryId = journalEntryId;
        Instrument = instrument;
        Market = market;
        Direction = direction;
        EntryPrice = entryPrice;
        ExitPrice = exitPrice;
        StopLoss = stopLoss;
        TakeProfit = takeProfit;
        PositionSize = positionSize;
        AccountBalance = accountBalance;
        RiskAmount = riskAmount;
        PlannedRiskPercentage = plannedRiskPercentage;
        ActualRiskPercentage = actualRiskPercentage;
        ResultAmount = resultAmount;
        ResultRMultiple = resultRMultiple;
        SetupName = setupName;
        Timeframe = timeframe;
        EntryReason = entryReason;
        ExitReason = exitReason;
        PlanBeforeTrade = planBeforeTrade;
        ExecutionNotes = executionNotes;
        EmotionsBefore = emotionsBefore;
        EmotionsDuring = emotionsDuring;
        EmotionsAfter = emotionsAfter;
        RulesRespected = TradingCoachCollections.CopyStrings(rulesRespected);
        Mistakes = TradingCoachCollections.CopyStrings(mistakes);
        Lessons = TradingCoachCollections.CopyStrings(lessons);
        Tags = TradingCoachCollections.CopyStrings(tags);
        OpenedAtUtc = openedAtUtc;
        ClosedAtUtc = closedAtUtc;
    }

    public string? JournalEntryId { get; }
    public string? Instrument { get; }
    public string? Market { get; }
    public string? Direction { get; }
    public decimal? EntryPrice { get; }
    public decimal? ExitPrice { get; }
    public decimal? StopLoss { get; }
    public decimal? TakeProfit { get; }
    public decimal? PositionSize { get; }
    public decimal? AccountBalance { get; }
    public decimal? RiskAmount { get; }
    public decimal? PlannedRiskPercentage { get; }
    public decimal? ActualRiskPercentage { get; }
    public decimal? ResultAmount { get; }
    public decimal? ResultRMultiple { get; }
    public string? SetupName { get; }
    public string? Timeframe { get; }
    public string? EntryReason { get; }
    public string? ExitReason { get; }
    public string? PlanBeforeTrade { get; }
    public string? ExecutionNotes { get; }
    public string? EmotionsBefore { get; }
    public string? EmotionsDuring { get; }
    public string? EmotionsAfter { get; }
    public IReadOnlyList<string> RulesRespected { get; }
    public IReadOnlyList<string> Mistakes { get; }
    public IReadOnlyList<string> Lessons { get; }
    public IReadOnlyList<string> Tags { get; }
    public DateTimeOffset? OpenedAtUtc { get; }
    public DateTimeOffset? ClosedAtUtc { get; }
}

public sealed record TradingCoachProfile
{
    public TradingCoachProfile(
        string preferredLanguage = "en",
        string experienceLevel = "unspecified",
        string? tradingStyle = null,
        decimal? maximumRiskPerTrade = null,
        decimal? dailyLossLimit = null,
        decimal? weeklyLossLimit = null,
        IReadOnlyCollection<string>? knownRules = null,
        IReadOnlyCollection<string>? focusAreas = null,
        string tone = "supportive",
        string detailLevel = "standard")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(experienceLevel);
        ArgumentException.ThrowIfNullOrWhiteSpace(tone);
        ArgumentException.ThrowIfNullOrWhiteSpace(detailLevel);
        ValidatePercentage(maximumRiskPerTrade, nameof(maximumRiskPerTrade));
        ValidatePercentage(dailyLossLimit, nameof(dailyLossLimit));
        ValidatePercentage(weeklyLossLimit, nameof(weeklyLossLimit));

        PreferredLanguage = preferredLanguage.Trim();
        ExperienceLevel = experienceLevel.Trim();
        TradingStyle = TradingCoachCollections.Normalize(tradingStyle);
        MaximumRiskPerTrade = maximumRiskPerTrade;
        DailyLossLimit = dailyLossLimit;
        WeeklyLossLimit = weeklyLossLimit;
        KnownRules = TradingCoachCollections.CopyStrings(knownRules);
        FocusAreas = TradingCoachCollections.CopyStrings(focusAreas);
        Tone = tone.Trim();
        DetailLevel = detailLevel.Trim();
    }

    public string PreferredLanguage { get; }
    public string ExperienceLevel { get; }
    public string? TradingStyle { get; }
    public decimal? MaximumRiskPerTrade { get; }
    public decimal? DailyLossLimit { get; }
    public decimal? WeeklyLossLimit { get; }
    public IReadOnlyList<string> KnownRules { get; }
    public IReadOnlyList<string> FocusAreas { get; }
    public string Tone { get; }
    public string DetailLevel { get; }

    private static void ValidatePercentage(decimal? value, string parameterName)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Profile percentages must be between 0 and 100.");
        }
    }
}

public sealed record TradingCoachExecutionOptions
{
    public TradingCoachExecutionOptions(
        string language = "en",
        bool includeMemory = false,
        bool includeKnowledge = false,
        bool includeDetailedScores = true,
        int maximumRecommendations = 5,
        int maximumChecklistItems = 5,
        bool failOnIncompleteData = false,
        TimeSpan? analysisTimeout = null,
        string? correlationId = null,
        string? sessionId = null,
        string? conversationId = null,
        string? tenantId = null,
        string? userId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        if (maximumRecommendations is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRecommendations), "Maximum recommendations must be between 1 and 20.");
        }

        if (maximumChecklistItems is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumChecklistItems), "Maximum checklist items must be between 1 and 20.");
        }

        var timeout = analysisTimeout ?? TimeSpan.FromSeconds(30);
        if (timeout < TimeSpan.FromSeconds(1) || timeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(analysisTimeout), "Analysis timeout must be between one second and two minutes.");
        }

        if (includeMemory && string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException("ConversationId is required when memory is enabled.", nameof(conversationId));
        }

        Language = language.Trim();
        IncludeMemory = includeMemory;
        IncludeKnowledge = includeKnowledge;
        IncludeDetailedScores = includeDetailedScores;
        MaximumRecommendations = maximumRecommendations;
        MaximumChecklistItems = maximumChecklistItems;
        FailOnIncompleteData = failOnIncompleteData;
        AnalysisTimeout = timeout;
        CorrelationId = TradingCoachCollections.Normalize(correlationId);
        SessionId = TradingCoachCollections.Normalize(sessionId);
        ConversationId = TradingCoachCollections.Normalize(conversationId);
        TenantId = TradingCoachCollections.Normalize(tenantId);
        UserId = TradingCoachCollections.Normalize(userId);
    }

    public string Language { get; }
    public bool IncludeMemory { get; }
    public bool IncludeKnowledge { get; }
    public bool IncludeDetailedScores { get; }
    public int MaximumRecommendations { get; }
    public int MaximumChecklistItems { get; }
    public bool FailOnIncompleteData { get; }
    public TimeSpan AnalysisTimeout { get; }
    public string? CorrelationId { get; }
    public string? SessionId { get; }
    public string? ConversationId { get; }
    public string? TenantId { get; }
    public string? UserId { get; }
}

public sealed record TradingJournalNormalizationResult
{
    public TradingJournalNormalizationResult(
        TradingJournalAnalysisRequest normalizedRequest,
        IReadOnlyDictionary<string, string>? derivedFields,
        IReadOnlyCollection<string>? warnings,
        decimal completenessScore)
    {
        ArgumentNullException.ThrowIfNull(normalizedRequest);
        if (completenessScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(completenessScore));
        }

        NormalizedRequest = normalizedRequest;
        DerivedFields = TradingCoachCollections.CopyDictionary(derivedFields);
        Warnings = TradingCoachCollections.CopyStrings(warnings);
        CompletenessScore = completenessScore;
    }

    public TradingJournalAnalysisRequest NormalizedRequest { get; }
    public IReadOnlyDictionary<string, string> DerivedFields { get; }
    public IReadOnlyList<string> Warnings { get; }
    public decimal CompletenessScore { get; }
}

public static class TradeMetricNames
{
    public const string RiskPercentage = "riskPercentage";
    public const string PlannedRewardToRisk = "plannedRewardToRisk";
    public const string RealizedRMultiple = "realizedRMultiple";
    public const string DurationMinutes = "durationMinutes";
    public const string StopDistance = "stopDistance";
    public const string TargetDistance = "targetDistance";
    public const string ResultPercentageOfBalance = "resultPercentageOfBalance";
    public const string PlannedActualRiskDelta = "plannedActualRiskDelta";
}

public sealed record TradeMetricsResult
{
    public static TradeMetricsResult Empty { get; } = new(null, null, null, null);

    public TradeMetricsResult(
        IReadOnlyDictionary<string, decimal>? computedMetrics,
        IReadOnlyCollection<string>? unavailableMetrics,
        IReadOnlyCollection<string>? warnings,
        IReadOnlyDictionary<string, string>? calculationSources)
    {
        ComputedMetrics = TradingCoachCollections.CopyDictionary(computedMetrics);
        UnavailableMetrics = TradingCoachCollections.CopyStrings(unavailableMetrics);
        Warnings = TradingCoachCollections.CopyStrings(warnings);
        CalculationSources = TradingCoachCollections.CopyDictionary(calculationSources);
    }

    public IReadOnlyDictionary<string, decimal> ComputedMetrics { get; }
    public IReadOnlyList<string> UnavailableMetrics { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyDictionary<string, string> CalculationSources { get; }
}

public enum TradingCoachFindingSeverity
{
    Information,
    Warning,
    Critical
}

public sealed record TradingCoachFinding
{
    public TradingCoachFinding(
        string code,
        string category,
        string message,
        TradingCoachFindingSeverity severity,
        bool isRuleBased,
        IReadOnlyCollection<string>? evidenceFields = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Category = category.Trim();
        Message = message.Trim();
        Severity = severity;
        IsRuleBased = isRuleBased;
        EvidenceFields = TradingCoachCollections.CopyStrings(evidenceFields);
    }

    public string Code { get; }
    public string Category { get; }
    public string Message { get; }
    public TradingCoachFindingSeverity Severity { get; }
    public bool IsRuleBased { get; }
    public IReadOnlyList<string> EvidenceFields { get; }
}

public sealed record TradingCoachScoreExplanation
{
    public TradingCoachScoreExplanation(
        string scoreName,
        int value,
        IReadOnlyCollection<string>? factors,
        decimal confidence,
        bool isRuleBased)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scoreName);
        TradingCoachScores.Validate(value, nameof(value));
        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        ScoreName = scoreName.Trim();
        Value = value;
        Factors = TradingCoachCollections.CopyStrings(factors);
        Confidence = confidence;
        IsRuleBased = isRuleBased;
    }

    public string ScoreName { get; }
    public int Value { get; }
    public IReadOnlyList<string> Factors { get; }
    public decimal Confidence { get; }
    public bool IsRuleBased { get; }
}

public sealed record TradingCoachScores
{
    public static TradingCoachScores Empty { get; } = new(0, 0, 0, 0, 0, 0, null);

    public TradingCoachScores(
        int planAdherence,
        int riskDiscipline,
        int executionQuality,
        int emotionalControl,
        int journalCompleteness,
        int overallProcessQuality,
        IReadOnlyCollection<TradingCoachScoreExplanation>? explanations)
    {
        Validate(planAdherence, nameof(planAdherence));
        Validate(riskDiscipline, nameof(riskDiscipline));
        Validate(executionQuality, nameof(executionQuality));
        Validate(emotionalControl, nameof(emotionalControl));
        Validate(journalCompleteness, nameof(journalCompleteness));
        Validate(overallProcessQuality, nameof(overallProcessQuality));
        PlanAdherence = planAdherence;
        RiskDiscipline = riskDiscipline;
        ExecutionQuality = executionQuality;
        EmotionalControl = emotionalControl;
        JournalCompleteness = journalCompleteness;
        OverallProcessQuality = overallProcessQuality;
        Explanations = Array.AsReadOnly(explanations?.ToArray() ?? []);
    }

    public int PlanAdherence { get; }
    public int RiskDiscipline { get; }
    public int ExecutionQuality { get; }
    public int EmotionalControl { get; }
    public int JournalCompleteness { get; }
    public int OverallProcessQuality { get; }
    public IReadOnlyList<TradingCoachScoreExplanation> Explanations { get; }

    internal static void Validate(int value, string parameterName)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Scores must be between 0 and 100.");
        }
    }
}

public sealed record TradingCoachRecommendedAction
{
    public TradingCoachRecommendedAction(
        string code,
        string action,
        IReadOnlyCollection<string>? relatedFindingCodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        Code = code.Trim();
        Action = action.Trim();
        RelatedFindingCodes = TradingCoachCollections.CopyStrings(relatedFindingCodes);
    }

    public string Code { get; }
    public string Action { get; }
    public IReadOnlyList<string> RelatedFindingCodes { get; }
}

public sealed record TradingCoachRuleAnalysisResult
{
    public TradingCoachRuleAnalysisResult(
        IReadOnlyCollection<TradingCoachFinding>? findings,
        IReadOnlyCollection<string>? strengths,
        IReadOnlyCollection<string>? missingInformation,
        TradingCoachScores scores)
    {
        ArgumentNullException.ThrowIfNull(scores);
        Findings = Array.AsReadOnly(findings?.ToArray() ?? []);
        Strengths = TradingCoachCollections.CopyStrings(strengths);
        MissingInformation = TradingCoachCollections.CopyStrings(missingInformation);
        Scores = scores;
    }

    public IReadOnlyList<TradingCoachFinding> Findings { get; }
    public IReadOnlyList<string> Strengths { get; }
    public IReadOnlyList<string> MissingInformation { get; }
    public TradingCoachScores Scores { get; }
}

public sealed record TradingCoachAnalysis
{
    public TradingCoachAnalysis(
        string summary,
        string dataQuality,
        IReadOnlyCollection<string>? strengths,
        IReadOnlyCollection<TradingCoachFinding>? ruleViolations,
        IReadOnlyCollection<string>? riskObservations,
        IReadOnlyCollection<string>? executionObservations,
        IReadOnlyCollection<string>? psychologyObservations,
        IReadOnlyCollection<string>? missingInformation,
        IReadOnlyCollection<TradingCoachFinding>? priorityIssues,
        IReadOnlyCollection<TradingCoachRecommendedAction>? recommendedActions,
        IReadOnlyCollection<string>? nextTradeChecklist,
        TradingCoachScores scores,
        string disclaimer,
        DateTimeOffset generatedAtUtc,
        string agentVersion,
        Guid analysisId,
        TradeMetricsResult? metrics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataQuality);
        ArgumentException.ThrowIfNullOrWhiteSpace(disclaimer);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentVersion);
        ArgumentNullException.ThrowIfNull(scores);
        if (generatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Analysis generation date must be UTC.", nameof(generatedAtUtc));
        }

        if (analysisId == Guid.Empty)
        {
            throw new ArgumentException("AnalysisId cannot be empty.", nameof(analysisId));
        }

        Summary = summary.Trim();
        DataQuality = dataQuality.Trim();
        Strengths = TradingCoachCollections.CopyStrings(strengths);
        RuleViolations = Array.AsReadOnly(ruleViolations?.ToArray() ?? []);
        RiskObservations = TradingCoachCollections.CopyStrings(riskObservations);
        ExecutionObservations = TradingCoachCollections.CopyStrings(executionObservations);
        PsychologyObservations = TradingCoachCollections.CopyStrings(psychologyObservations);
        MissingInformation = TradingCoachCollections.CopyStrings(missingInformation);
        PriorityIssues = Array.AsReadOnly(priorityIssues?.ToArray() ?? []);
        RecommendedActions = Array.AsReadOnly(recommendedActions?.ToArray() ?? []);
        NextTradeChecklist = TradingCoachCollections.CopyStrings(nextTradeChecklist);
        Scores = scores;
        Disclaimer = disclaimer.Trim();
        GeneratedAtUtc = generatedAtUtc;
        AgentVersion = agentVersion.Trim();
        AnalysisId = analysisId;
        Metrics = metrics ?? TradeMetricsResult.Empty;
    }

    public string Summary { get; }
    public string DataQuality { get; }
    public IReadOnlyList<string> Strengths { get; }
    public IReadOnlyList<TradingCoachFinding> RuleViolations { get; }
    public IReadOnlyList<string> RiskObservations { get; }
    public IReadOnlyList<string> ExecutionObservations { get; }
    public IReadOnlyList<string> PsychologyObservations { get; }
    public IReadOnlyList<string> MissingInformation { get; }
    public IReadOnlyList<TradingCoachFinding> PriorityIssues { get; }
    public IReadOnlyList<TradingCoachRecommendedAction> RecommendedActions { get; }
    public IReadOnlyList<string> NextTradeChecklist { get; }
    public TradingCoachScores Scores { get; }
    public string Disclaimer { get; }
    public DateTimeOffset GeneratedAtUtc { get; }
    public string AgentVersion { get; }
    public Guid AnalysisId { get; }
    public TradeMetricsResult Metrics { get; }
}

internal static class TradingCoachCollections
{
    public static IReadOnlyList<string> CopyStrings(IEnumerable<string>? values) =>
        Array.AsReadOnly(values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray() ?? []);

    public static IReadOnlyDictionary<string, string> CopyDictionary(IReadOnlyDictionary<string, string>? values) =>
        new ReadOnlyDictionary<string, string>(values is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(values, StringComparer.Ordinal));

    public static IReadOnlyDictionary<string, decimal> CopyDictionary(IReadOnlyDictionary<string, decimal>? values) =>
        new ReadOnlyDictionary<string, decimal>(values is null
            ? new Dictionary<string, decimal>(StringComparer.Ordinal)
            : new Dictionary<string, decimal>(values, StringComparer.Ordinal));

    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
