using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.Agents;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics.Tests;

internal static class TradingAnalyticsTestData
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-01T12:00:00Z");

    public static TradingCoachProfile Profile(decimal? maximumRisk = 2m) => new(maximumRiskPerTrade: maximumRisk);

    public static TradingJournalAnalysisRequest Trade(
        int index = 0,
        decimal resultR = 1m,
        decimal risk = 1m,
        string? id = null,
        bool includeId = true,
        string setup = "Breakout Review",
        string? behavior = null,
        bool complete = true,
        bool invalid = false,
        bool ruleViolation = false,
        DateTimeOffset? openedAtUtc = null,
        decimal? plannedRisk = null) => new(
            journalEntryId: includeId ? id ?? $"trade-{index}" : null,
            instrument: complete ? "EURUSD" : null,
            market: complete ? "forex" : null,
            direction: complete ? "long" : null,
            entryPrice: invalid ? -1 : complete ? 100 : null,
            exitPrice: complete ? 105 : null,
            stopLoss: complete ? 95 : null,
            takeProfit: complete ? 110 : null,
            positionSize: complete ? 1 : null,
            accountBalance: complete ? 10_000 : null,
            riskAmount: complete ? 100 : null,
            plannedRiskPercentage: complete ? plannedRisk ?? risk : null,
            actualRiskPercentage: risk,
            resultAmount: complete ? resultR * 100 : null,
            resultRMultiple: resultR,
            setupName: complete ? setup : null,
            timeframe: complete ? "1H" : null,
            entryReason: complete ? "Documented setup criteria." : null,
            exitReason: complete ? "Documented exit criterion." : null,
            planBeforeTrade: complete ? "Follow the predefined plan." : null,
            executionNotes: behavior ?? (complete ? "Executed according to plan." : null),
            emotionsBefore: complete ? "calm" : null,
            emotionsDuring: complete ? "focused" : null,
            emotionsAfter: complete ? "neutral" : null,
            rulesRespected: ruleViolation ? ["Rule not respected"] : complete ? ["Risk rule respected"] : [],
            mistakes: behavior is null ? complete ? ["No process mistake documented"] : [] : [behavior],
            lessons: complete ? ["Use the process checklist"] : [],
            tags: complete ? ["process"] : [],
            openedAtUtc: openedAtUtc ?? Now.AddDays(index),
            closedAtUtc: (openedAtUtc ?? Now.AddDays(index)).AddHours(1));

    public static TradingJournalAnalyticsRequest Request(
        IReadOnlyCollection<TradingJournalAnalysisRequest>? trades = null,
        TradingCoachProfile? profile = null,
        TradingAnalyticsGroupingPeriod grouping = TradingAnalyticsGroupingPeriod.Week,
        int minimumTrades = 3,
        string language = "en") => new(
            trades ?? [Trade(0), Trade(1), Trade(2)],
            profile ?? Profile(),
            groupingPeriod: grouping,
            minimumTradesForTrend: minimumTrades,
            requestedLanguage: language,
            correlationId: "correlation",
            sessionId: "session",
            tenantId: "tenant",
            userId: "user");

    public static TradingJournalAnalyticsOptions Options(
        bool includeAi = false,
        bool failOnInvalid = true,
        bool excludeInvalid = true) => new()
        {
            IncludeAIInterpretation = includeAi,
            FailOnInvalidTrade = failOnInvalid,
            ExcludeInvalidTradesFromAggregates = excludeInvalid,
            MinimumTradesForTrend = 3,
            MinimumTradesPerGroup = 2,
            MinimumDataCompleteness = 50
        };

    public static ITradingJournalValidator TradeValidator() => new TradingJournalValidator(
        Microsoft.Extensions.Options.Options.Create(new TradingJournalValidationOptions()));

    public static TradingJournalAnalyticsValidationResult Validate(
        TradingJournalAnalyticsRequest request,
        TradingJournalAnalyticsOptions? options = null) => new TradingJournalAnalyticsValidator(
            TradeValidator(),
            new TradingJournalNormalizer()).Validate(
                request,
                options ?? Options(),
                Guid.Parse("11111111-1111-1111-1111-111111111111"));

    public static TradingJournalCollectionNormalizationResult Normalize(
        TradingJournalAnalyticsRequest request,
        TradingJournalAnalyticsOptions? options = null) => new TradingJournalCollectionNormalizer(
            new TradingJournalNormalizer()).Normalize(Validate(request, options));

    public static TradingJournalTradeAnalysis Analyzed(
        TradingJournalAnalysisRequest trade,
        int originalIndex = 0,
        TradingCoachProfile? profile = null)
    {
        var normalization = new TradingJournalNormalizer().Normalize(trade);
        var metrics = new TradeMetricsCalculator().Calculate(normalization);
        var detector = new TradingBehaviorPatternDetector();
        var coaching = new TradingCoachRuleAnalyzer(detector, new TradingCoachScoreCalculator())
            .Analyze(normalization, metrics, profile ?? Profile());
        return new TradingJournalTradeAnalysis(
            new NormalizedTradingJournalTrade(originalIndex, normalization, $"test:{originalIndex}"),
            metrics,
            coaching,
            detector.Detect(normalization.NormalizedRequest));
    }

    public static IReadOnlyList<TradingJournalTradeAnalysis> Analyzed(params TradingJournalAnalysisRequest[] trades) =>
        Array.AsReadOnly(trades.Select((trade, index) => Analyzed(trade, index)).ToArray());

    public static TradingJournalDataQuality DataQuality(int validCount = 3, decimal completeness = 90) => new(
        completeness, validCount, 0, 0, new Dictionary<string, int>(), 0,
        new TradingDateRange(Now, Now.AddDays(Math.Max(1, validCount - 1))), TradingDataConfidenceLevel.High, []);

    public static RiskDriftAnalysis RiskDrift(
        bool detected = false,
        TradingTrendDirection direction = TradingTrendDirection.Stable) => new(
            detected, direction, detected ? TradingAnalyticsSeverity.Warning : TradingAnalyticsSeverity.Information,
            [], 1, 1, [], 0.8m, true);

    public static TradingJournalAnalyticsFinding Finding(string code = "FINDING") => new(
        code, "process", "Review the documented process.", TradingAnalyticsSeverity.Warning);

    public static TradingJournalRuleAnalysisResult Rules(string code = "FINDING")
    {
        var finding = Finding(code);
        return new TradingJournalRuleAnalysisResult(
            [finding], ["Documented strength."],
            [new TradingJournalAnalyticsRecommendation("RULE_ACTION", "Review the documented process.", [code])],
            ["Review the deterministic finding."]);
    }

    public static TradingJournalAnalyticsReport Report(
        TradingJournalAggregateMetrics? aggregates = null,
        HistoricalRDrawdown? drawdown = null,
        TradingStreakMetrics? streaks = null,
        IReadOnlyCollection<TradingBehaviorTrend>? behaviors = null,
        IReadOnlyCollection<TradingScoreEvolution>? scores = null,
        IReadOnlyCollection<TradingJournalAnalyticsRecommendation>? recommendations = null,
        string summary = "Deterministic historical process analysis.") => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            summary,
            ["Deterministic explanation."],
            new TradingDateRange(Now, Now.AddDays(2)),
            DataQuality(),
            aggregates ?? TradingJournalAggregateMetrics.Empty,
            drawdown ?? HistoricalRDrawdown.Empty,
            streaks ?? TradingStreakMetrics.Empty,
            [],
            [],
            behaviors ?? [],
            RiskDrift(),
            [],
            scores ?? [],
            ["Documented strength."],
            [Finding()],
            recommendations ?? [new TradingJournalAnalyticsRecommendation("RULE_ACTION", "Review the documented process.", ["FINDING"])],
            ["Review the deterministic finding."],
            TradingJournalAnalyticsConstants.Disclaimer,
            Now,
            null,
            false);

    public static TradingJournalAIInterpretation Interpretation(
        string summary = "The supplied history supports a descriptive process review.",
        string recommendation = "Review the documented process.",
        string relatedCode = "FINDING") => new(
            summary,
            ["This is an interpretation of deterministic values."],
            [new TradingJournalAnalyticsRecommendation("AI_ACTION", recommendation, [relatedCode])],
            ["Review the supplied journal fields."],
            TradingJournalAnalyticsConstants.Disclaimer,
            "en");

    public static string ValidAiJson(
        string summary = "The supplied history supports a descriptive process review.",
        string recommendation = "Review the documented process.",
        string language = "en") => JsonSerializer.Serialize(new
        {
            summary,
            explanations = new[] { "This interpretation uses deterministic historical values." },
            recommendations = new[]
            {
                new { code = "AI_ACTION", text = recommendation, relatedFindingCodes = new[] { "FINDING" } }
            },
            nextReviewChecklist = new[] { "Review the supplied journal fields." },
            disclaimer = TradingJournalAnalyticsConstants.Disclaimer,
            language
        });

    public static AIAgentExecutionResponse AgentResponse(string? content = null) => new(
        new AIAgentId(TradingJournalAnalyticsConstants.AgentId),
        AIAgentVersion.Parse(TradingJournalAnalyticsConstants.AgentVersion),
        "session",
        null,
        "correlation",
        TradingJournalAnalyticsConstants.Scenario,
        true,
        content ?? ValidAiJson(),
        AIAgentExecutionState.Completed,
        Now,
        Now.AddSeconds(1),
        "Fake",
        false,
        false,
        false,
        [],
        AIAgentExecutionMetrics.Empty);
}

internal sealed class StubAgentExecutor : IAIAgentExecutor
{
    private readonly Func<AIAgentExecutionRequest, CancellationToken, Task<AIAgentExecutionResponse>> _execute;

    public StubAgentExecutor(Func<AIAgentExecutionRequest, CancellationToken, Task<AIAgentExecutionResponse>>? execute = null)
    {
        _execute = execute ?? ((_, _) => Task.FromResult(TradingAnalyticsTestData.AgentResponse()));
    }

    public int Calls { get; private set; }
    public AIAgentExecutionRequest? LastRequest { get; private set; }

    public Task<AIAgentExecutionResponse> ExecuteAsync(AIAgentExecutionRequest request, CancellationToken cancellationToken)
    {
        Calls++;
        LastRequest = request;
        return _execute(request, cancellationToken);
    }
}

internal sealed class CountingParser(ITradingJournalAnalyticsResponseParser inner) : ITradingJournalAnalyticsResponseParser
{
    public int Calls { get; private set; }

    public TradingJournalAIInterpretation Parse(string response, string requestedLanguage, Guid analysisId, string? correlationId = null)
    {
        Calls++;
        return inner.Parse(response, requestedLanguage, analysisId, correlationId);
    }
}

internal sealed class RecordingTimeProvider : TimeProvider
{
    public int UtcNowCalls { get; private set; }
    public int TimestampCalls { get; private set; }

    public override DateTimeOffset GetUtcNow()
    {
        UtcNowCalls++;
        return TradingAnalyticsTestData.Now.AddMilliseconds(UtcNowCalls);
    }

    public override long GetTimestamp()
    {
        TimestampCalls++;
        return TimestampCalls * TimeSpan.TicksPerMillisecond;
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
}

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
}
