using System.Collections.ObjectModel;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics;

public sealed record TradingJournalAggregateMetrics(
    int TradeCount,
    int WinningTradeCount,
    int LosingTradeCount,
    int BreakEvenTradeCount,
    int DocumentedTradeCount,
    int PlannedTradeCount,
    int RuleViolationTradeCount,
    decimal? AverageRiskPercentage,
    decimal? MedianRiskPercentage,
    decimal? MaximumRiskPercentage,
    decimal? AverageResultR,
    decimal? MedianResultR,
    decimal? BestResultR,
    decimal? WorstResultR,
    decimal TotalResultR,
    TimeSpan? AverageDuration,
    decimal PlanAdherenceRate,
    decimal StopUsageRate,
    decimal JournalCompletenessAverage,
    int PositiveProcessNegativeOutcomeCount,
    int NegativeProcessPositiveOutcomeCount)
{
    public static TradingJournalAggregateMetrics Empty { get; } = new(
        0, 0, 0, 0, 0, 0, 0, null, null, null, null, null, null, null, 0, null, 0, 0, 0, 0, 0);
}

public sealed record HistoricalRDrawdown(
    decimal MaximumDrawdownR,
    decimal PeakCumulativeR,
    decimal TroughCumulativeR,
    int? StartTradeIndex,
    int? EndTradeIndex,
    int? RecoveryTradeIndex,
    bool IsRecovered)
{
    public static HistoricalRDrawdown Empty { get; } = new(0, 0, 0, null, null, null, true);
}

public sealed record TradingStreakMetrics(
    int LongestWinningStreak,
    int LongestLosingStreak,
    int CurrentWinningStreak,
    int CurrentLosingStreak,
    int MaximumConsecutiveRuleViolations,
    int MaximumConsecutiveOversizedTrades,
    int MaximumConsecutiveIncompleteJournals)
{
    public static TradingStreakMetrics Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);
}

public sealed record TradingPeriodAggregate
{
    public TradingPeriodAggregate(
        DateTimeOffset periodStartUtc,
        DateTimeOffset periodEndUtc,
        int tradeCount,
        TradingJournalAggregateMetrics aggregateMetrics,
        TradingCoachScores averageScores,
        IReadOnlyDictionary<string, int>? behaviorCounts,
        decimal dataCompleteness,
        bool isSampleSizeSufficient)
    {
        if (periodStartUtc.Offset != TimeSpan.Zero || periodEndUtc.Offset != TimeSpan.Zero || periodEndUtc <= periodStartUtc)
        {
            throw new ArgumentException("Period boundaries must be ordered UTC values.");
        }

        PeriodStartUtc = periodStartUtc;
        PeriodEndUtc = periodEndUtc;
        TradeCount = tradeCount;
        AggregateMetrics = aggregateMetrics;
        AverageScores = averageScores;
        BehaviorCounts = AnalyticsCollections.CopyDictionary(behaviorCounts);
        DataCompleteness = dataCompleteness;
        IsSampleSizeSufficient = isSampleSizeSufficient;
    }

    public DateTimeOffset PeriodStartUtc { get; }
    public DateTimeOffset PeriodEndUtc { get; }
    public int TradeCount { get; }
    public TradingJournalAggregateMetrics AggregateMetrics { get; }
    public TradingCoachScores AverageScores { get; }
    public IReadOnlyDictionary<string, int> BehaviorCounts { get; }
    public decimal DataCompleteness { get; }
    public bool IsSampleSizeSufficient { get; }
}

public sealed record TradingSetupAnalytics
{
    public TradingSetupAnalytics(
        string setupName,
        int tradeCount,
        decimal? averageResultR,
        decimal? medianResultR,
        decimal planAdherenceRate,
        decimal ruleViolationRate,
        decimal? averageRiskPercentage,
        decimal averageProcessScore,
        IReadOnlyCollection<string>? commonMistakes,
        decimal dataCompleteness,
        bool isSampleSizeSufficient,
        IReadOnlyCollection<string>? warnings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setupName);
        SetupName = setupName.Trim();
        TradeCount = tradeCount;
        AverageResultR = averageResultR;
        MedianResultR = medianResultR;
        PlanAdherenceRate = planAdherenceRate;
        RuleViolationRate = ruleViolationRate;
        AverageRiskPercentage = averageRiskPercentage;
        AverageProcessScore = averageProcessScore;
        CommonMistakes = AnalyticsCollections.CopyStrings(commonMistakes);
        DataCompleteness = dataCompleteness;
        IsSampleSizeSufficient = isSampleSizeSufficient;
        Warnings = AnalyticsCollections.CopyStrings(warnings);
    }

    public string SetupName { get; }
    public int TradeCount { get; }
    public decimal? AverageResultR { get; }
    public decimal? MedianResultR { get; }
    public decimal PlanAdherenceRate { get; }
    public decimal RuleViolationRate { get; }
    public decimal? AverageRiskPercentage { get; }
    public decimal AverageProcessScore { get; }
    public IReadOnlyList<string> CommonMistakes { get; }
    public decimal DataCompleteness { get; }
    public bool IsSampleSizeSufficient { get; }
    public IReadOnlyList<string> Warnings { get; }
}

public sealed record TradingBehaviorTrend
{
    public TradingBehaviorTrend(
        string behavior,
        int occurrenceCount,
        int affectedTradeCount,
        DateTimeOffset? firstObservedAtUtc,
        DateTimeOffset? lastObservedAtUtc,
        TradingTrendDirection trendDirection,
        decimal confidence,
        IReadOnlyCollection<int>? relatedTradeIndexes,
        IReadOnlyCollection<string>? observations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(behavior);
        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        Behavior = behavior.Trim();
        OccurrenceCount = occurrenceCount;
        AffectedTradeCount = affectedTradeCount;
        FirstObservedAtUtc = firstObservedAtUtc;
        LastObservedAtUtc = lastObservedAtUtc;
        TrendDirection = trendDirection;
        Confidence = confidence;
        RelatedTradeIndexes = AnalyticsCollections.CopyOrderedIndexes(relatedTradeIndexes);
        Observations = AnalyticsCollections.CopyStrings(observations);
    }

    public string Behavior { get; }
    public int OccurrenceCount { get; }
    public int AffectedTradeCount { get; }
    public DateTimeOffset? FirstObservedAtUtc { get; }
    public DateTimeOffset? LastObservedAtUtc { get; }
    public TradingTrendDirection TrendDirection { get; }
    public decimal Confidence { get; }
    public IReadOnlyList<int> RelatedTradeIndexes { get; }
    public IReadOnlyList<string> Observations { get; }
}

public sealed record RiskDriftAnalysis
{
    public RiskDriftAnalysis(
        bool driftDetected,
        TradingTrendDirection direction,
        TradingAnalyticsSeverity severity,
        IReadOnlyCollection<int>? supportingTradeIndexes,
        decimal? baselineRisk,
        decimal? recentRisk,
        IReadOnlyCollection<string>? observations,
        decimal confidence,
        bool isSampleSizeSufficient)
    {
        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        DriftDetected = driftDetected;
        Direction = direction;
        Severity = severity;
        SupportingTradeIndexes = AnalyticsCollections.CopyOrderedIndexes(supportingTradeIndexes);
        BaselineRisk = baselineRisk;
        RecentRisk = recentRisk;
        Observations = AnalyticsCollections.CopyStrings(observations);
        Confidence = confidence;
        IsSampleSizeSufficient = isSampleSizeSufficient;
    }

    public bool DriftDetected { get; }
    public TradingTrendDirection Direction { get; }
    public TradingAnalyticsSeverity Severity { get; }
    public IReadOnlyList<int> SupportingTradeIndexes { get; }
    public decimal? BaselineRisk { get; }
    public decimal? RecentRisk { get; }
    public IReadOnlyList<string> Observations { get; }
    public decimal Confidence { get; }
    public bool IsSampleSizeSufficient { get; }
}

public sealed record PostOutcomeBehaviorObservation
{
    public PostOutcomeBehaviorObservation(
        string outcomeContext,
        int sampleCount,
        decimal? averageNextRiskChange,
        decimal? nextRuleViolationRate,
        decimal? averageNextJournalCompleteness,
        TimeSpan? averageTimeToNextTrade,
        IReadOnlyDictionary<string, int>? followingBehaviorCounts,
        string observation,
        bool isSampleSizeSufficient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcomeContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(observation);
        OutcomeContext = outcomeContext.Trim();
        SampleCount = sampleCount;
        AverageNextRiskChange = averageNextRiskChange;
        NextRuleViolationRate = nextRuleViolationRate;
        AverageNextJournalCompleteness = averageNextJournalCompleteness;
        AverageTimeToNextTrade = averageTimeToNextTrade;
        FollowingBehaviorCounts = AnalyticsCollections.CopyDictionary(followingBehaviorCounts);
        Observation = observation.Trim();
        IsSampleSizeSufficient = isSampleSizeSufficient;
    }

    public string OutcomeContext { get; }
    public int SampleCount { get; }
    public decimal? AverageNextRiskChange { get; }
    public decimal? NextRuleViolationRate { get; }
    public decimal? AverageNextJournalCompleteness { get; }
    public TimeSpan? AverageTimeToNextTrade { get; }
    public IReadOnlyDictionary<string, int> FollowingBehaviorCounts { get; }
    public string Observation { get; }
    public bool IsSampleSizeSufficient { get; }
}

public sealed record TradingPeriodScoreValue(
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    decimal Value);

public sealed record TradingScoreEvolution
{
    public TradingScoreEvolution(
        string scoreName,
        decimal? firstAverage,
        decimal? recentAverage,
        decimal? change,
        TradingTrendDirection trendDirection,
        IReadOnlyCollection<TradingPeriodScoreValue>? periodValues,
        bool isSampleSizeSufficient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scoreName);
        ScoreName = scoreName.Trim();
        FirstAverage = firstAverage;
        RecentAverage = recentAverage;
        Change = change;
        TrendDirection = trendDirection;
        PeriodValues = Array.AsReadOnly(periodValues?.OrderBy(value => value.PeriodStartUtc).ToArray() ?? []);
        IsSampleSizeSufficient = isSampleSizeSufficient;
    }

    public string ScoreName { get; }
    public decimal? FirstAverage { get; }
    public decimal? RecentAverage { get; }
    public decimal? Change { get; }
    public TradingTrendDirection TrendDirection { get; }
    public IReadOnlyList<TradingPeriodScoreValue> PeriodValues { get; }
    public bool IsSampleSizeSufficient { get; }
}

public sealed record TradingJournalDataQuality
{
    public TradingJournalDataQuality(
        decimal overallCompleteness,
        int validTradeCount,
        int invalidTradeCount,
        int incompleteTradeCount,
        IReadOnlyDictionary<string, int>? missingFieldFrequency,
        int duplicateCount,
        TradingDateRange dateCoverage,
        TradingDataConfidenceLevel confidenceLevel,
        IReadOnlyCollection<string>? limitations)
    {
        ArgumentNullException.ThrowIfNull(dateCoverage);
        OverallCompleteness = overallCompleteness;
        ValidTradeCount = validTradeCount;
        InvalidTradeCount = invalidTradeCount;
        IncompleteTradeCount = incompleteTradeCount;
        MissingFieldFrequency = AnalyticsCollections.CopyDictionary(missingFieldFrequency);
        DuplicateCount = duplicateCount;
        DateCoverage = dateCoverage;
        ConfidenceLevel = confidenceLevel;
        Limitations = AnalyticsCollections.CopyStrings(limitations);
    }

    public decimal OverallCompleteness { get; }
    public int ValidTradeCount { get; }
    public int InvalidTradeCount { get; }
    public int IncompleteTradeCount { get; }
    public IReadOnlyDictionary<string, int> MissingFieldFrequency { get; }
    public int DuplicateCount { get; }
    public TradingDateRange DateCoverage { get; }
    public TradingDataConfidenceLevel ConfidenceLevel { get; }
    public IReadOnlyList<string> Limitations { get; }
}

public sealed record TradingJournalAnalyticsFinding
{
    public TradingJournalAnalyticsFinding(
        string code,
        string category,
        string message,
        TradingAnalyticsSeverity severity,
        IReadOnlyCollection<int>? supportingTradeIndexes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Category = category.Trim();
        Message = message.Trim();
        Severity = severity;
        SupportingTradeIndexes = AnalyticsCollections.CopyOrderedIndexes(supportingTradeIndexes);
    }

    public string Code { get; }
    public string Category { get; }
    public string Message { get; }
    public TradingAnalyticsSeverity Severity { get; }
    public IReadOnlyList<int> SupportingTradeIndexes { get; }
}

public sealed record TradingJournalAnalyticsRecommendation
{
    public TradingJournalAnalyticsRecommendation(string code, string text, IReadOnlyCollection<string>? relatedFindingCodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Code = code.Trim();
        Text = text.Trim();
        RelatedFindingCodes = AnalyticsCollections.CopyStrings(relatedFindingCodes);
    }

    public string Code { get; }
    public string Text { get; }
    public IReadOnlyList<string> RelatedFindingCodes { get; }
}

public sealed record TradingJournalRuleAnalysisResult
{
    public TradingJournalRuleAnalysisResult(
        IReadOnlyCollection<TradingJournalAnalyticsFinding>? findings,
        IReadOnlyCollection<string>? strengths,
        IReadOnlyCollection<TradingJournalAnalyticsRecommendation>? recommendations,
        IReadOnlyCollection<string>? checklist)
    {
        Findings = Array.AsReadOnly(findings?.ToArray() ?? []);
        Strengths = AnalyticsCollections.CopyStrings(strengths);
        Recommendations = Array.AsReadOnly(recommendations?.ToArray() ?? []);
        Checklist = AnalyticsCollections.CopyStrings(checklist);
    }

    public IReadOnlyList<TradingJournalAnalyticsFinding> Findings { get; }
    public IReadOnlyList<string> Strengths { get; }
    public IReadOnlyList<TradingJournalAnalyticsRecommendation> Recommendations { get; }
    public IReadOnlyList<string> Checklist { get; }
}

public sealed record TradingJournalAIInterpretation
{
    public TradingJournalAIInterpretation(
        string summary,
        IReadOnlyCollection<string>? explanations,
        IReadOnlyCollection<TradingJournalAnalyticsRecommendation>? recommendations,
        IReadOnlyCollection<string>? nextReviewChecklist,
        string disclaimer,
        string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(disclaimer);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        Summary = summary.Trim();
        Explanations = AnalyticsCollections.CopyStrings(explanations);
        Recommendations = Array.AsReadOnly(recommendations?.ToArray() ?? []);
        NextReviewChecklist = AnalyticsCollections.CopyStrings(nextReviewChecklist);
        Disclaimer = disclaimer.Trim();
        Language = language.Trim();
    }

    public string Summary { get; }
    public IReadOnlyList<string> Explanations { get; }
    public IReadOnlyList<TradingJournalAnalyticsRecommendation> Recommendations { get; }
    public IReadOnlyList<string> NextReviewChecklist { get; }
    public string Disclaimer { get; }
    public string Language { get; }
}

public sealed record TradingJournalAnalyticsReport
{
    public TradingJournalAnalyticsReport(
        Guid analysisId,
        string summary,
        IReadOnlyCollection<string>? explanations,
        TradingDateRange dateRange,
        TradingJournalDataQuality dataQuality,
        TradingJournalAggregateMetrics aggregateMetrics,
        HistoricalRDrawdown historicalDrawdown,
        TradingStreakMetrics streaks,
        IReadOnlyCollection<TradingPeriodAggregate>? periods,
        IReadOnlyCollection<TradingSetupAnalytics>? setupAnalytics,
        IReadOnlyCollection<TradingBehaviorTrend>? behaviorTrends,
        RiskDriftAnalysis riskDrift,
        IReadOnlyCollection<PostOutcomeBehaviorObservation>? postOutcomeObservations,
        IReadOnlyCollection<TradingScoreEvolution>? scoreEvolution,
        IReadOnlyCollection<string>? strengths,
        IReadOnlyCollection<TradingJournalAnalyticsFinding>? priorityIssues,
        IReadOnlyCollection<TradingJournalAnalyticsRecommendation>? recommendations,
        IReadOnlyCollection<string>? nextReviewChecklist,
        string disclaimer,
        DateTimeOffset generatedAtUtc,
        string? agentVersion,
        bool aiInterpretationUsed)
    {
        if (analysisId == Guid.Empty)
        {
            throw new ArgumentException("AnalysisId cannot be empty.", nameof(analysisId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(disclaimer);
        if (generatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The generation date must use the UTC offset.", nameof(generatedAtUtc));
        }

        AnalysisId = analysisId;
        Summary = summary.Trim();
        Explanations = AnalyticsCollections.CopyStrings(explanations);
        DateRange = dateRange;
        DataQuality = dataQuality;
        AggregateMetrics = aggregateMetrics;
        HistoricalDrawdown = historicalDrawdown;
        Streaks = streaks;
        Periods = Array.AsReadOnly(periods?.ToArray() ?? []);
        SetupAnalytics = Array.AsReadOnly(setupAnalytics?.ToArray() ?? []);
        BehaviorTrends = Array.AsReadOnly(behaviorTrends?.ToArray() ?? []);
        RiskDrift = riskDrift;
        PostOutcomeObservations = Array.AsReadOnly(postOutcomeObservations?.ToArray() ?? []);
        ScoreEvolution = Array.AsReadOnly(scoreEvolution?.ToArray() ?? []);
        Strengths = AnalyticsCollections.CopyStrings(strengths);
        PriorityIssues = Array.AsReadOnly(priorityIssues?.ToArray() ?? []);
        Recommendations = Array.AsReadOnly(recommendations?.ToArray() ?? []);
        NextReviewChecklist = AnalyticsCollections.CopyStrings(nextReviewChecklist);
        Disclaimer = disclaimer.Trim();
        GeneratedAtUtc = generatedAtUtc;
        AgentVersion = AnalyticsCollections.Normalize(agentVersion);
        AIInterpretationUsed = aiInterpretationUsed;
    }

    public Guid AnalysisId { get; }
    public string Summary { get; }
    public IReadOnlyList<string> Explanations { get; }
    public TradingDateRange DateRange { get; }
    public TradingJournalDataQuality DataQuality { get; }
    public TradingJournalAggregateMetrics AggregateMetrics { get; }
    public HistoricalRDrawdown HistoricalDrawdown { get; }
    public TradingStreakMetrics Streaks { get; }
    public IReadOnlyList<TradingPeriodAggregate> Periods { get; }
    public IReadOnlyList<TradingSetupAnalytics> SetupAnalytics { get; }
    public IReadOnlyList<TradingBehaviorTrend> BehaviorTrends { get; }
    public RiskDriftAnalysis RiskDrift { get; }
    public IReadOnlyList<PostOutcomeBehaviorObservation> PostOutcomeObservations { get; }
    public IReadOnlyList<TradingScoreEvolution> ScoreEvolution { get; }
    public IReadOnlyList<string> Strengths { get; }
    public IReadOnlyList<TradingJournalAnalyticsFinding> PriorityIssues { get; }
    public IReadOnlyList<TradingJournalAnalyticsRecommendation> Recommendations { get; }
    public IReadOnlyList<string> NextReviewChecklist { get; }
    public string Disclaimer { get; }
    public DateTimeOffset GeneratedAtUtc { get; }
    public string? AgentVersion { get; }
    public bool AIInterpretationUsed { get; }
}
