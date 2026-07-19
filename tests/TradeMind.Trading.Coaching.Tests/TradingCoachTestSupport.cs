using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Agents;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Coaching.Tests;

internal static class TradingCoachTestData
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-19T12:00:00Z");

    public static TradingJournalAnalysisRequest CompleteRequest(
        string? executionNotes = "Executed according to the documented plan.",
        decimal? resultAmount = 200,
        decimal? resultRMultiple = 2,
        decimal? plannedRiskPercentage = 1,
        decimal? actualRiskPercentage = 1,
        IReadOnlyCollection<string>? mistakes = null) => new(
            journalEntryId: "journal-1",
            instrument: "EURUSD",
            market: "forex",
            direction: "long",
            entryPrice: 100,
            exitPrice: 110,
            stopLoss: 95,
            takeProfit: 110,
            positionSize: 1,
            accountBalance: 10_000,
            riskAmount: 100,
            plannedRiskPercentage: plannedRiskPercentage,
            actualRiskPercentage: actualRiskPercentage,
            resultAmount: resultAmount,
            resultRMultiple: resultRMultiple,
            setupName: "Breakout review",
            timeframe: "1H",
            entryReason: "The documented setup criteria were present.",
            exitReason: "The documented exit criterion was reached.",
            planBeforeTrade: "Follow the setup and the predefined risk limit.",
            executionNotes: executionNotes,
            emotionsBefore: "calm",
            emotionsDuring: "focused",
            emotionsAfter: "neutral",
            rulesRespected: ["Risk rule respected"],
            mistakes: mistakes ?? ["No process mistake documented"],
            lessons: ["Keep using the pre-trade checklist"],
            tags: ["Process", "Review"],
            openedAtUtc: Now,
            closedAtUtc: Now.AddHours(2));

    public static TradingJournalNormalizationResult Normalize(TradingJournalAnalysisRequest? request = null) =>
        new TradingJournalNormalizer().Normalize(request ?? CompleteRequest());

    public static TradeMetricsResult Metrics(TradingJournalAnalysisRequest? request = null)
    {
        var normalized = Normalize(request);
        return new TradeMetricsCalculator().Calculate(normalized);
    }

    public static TradingCoachRuleAnalysisResult Rules(TradingJournalAnalysisRequest? request = null)
    {
        var normalization = Normalize(request);
        var metrics = new TradeMetricsCalculator().Calculate(normalization);
        return new TradingCoachRuleAnalyzer(
            new TradingBehaviorPatternDetector(),
            new TradingCoachScoreCalculator()).Analyze(normalization, metrics, new TradingCoachProfile());
    }

    public static TradingCoachAnalysis Analysis(
        string summary = "The review identifies a documented process with focused improvements.",
        IReadOnlyCollection<TradingCoachFinding>? violations = null,
        IReadOnlyCollection<TradingCoachFinding>? priority = null,
        IReadOnlyCollection<TradingCoachRecommendedAction>? actions = null,
        IReadOnlyCollection<string>? missing = null,
        IReadOnlyCollection<string>? checklist = null,
        TradingCoachScores? scores = null,
        TradeMetricsResult? metrics = null) => new(
            summary,
            "high",
            ["The plan was documented."],
            violations ?? [],
            ["Review documented risk consistency."],
            ["Review entry and exit documentation."],
            ["Continue labeling emotions."],
            missing ?? [],
            priority ?? [],
            actions ?? [],
            checklist ?? ["Complete the journal before reviewing the result."],
            scores ?? Scores(isRuleBased: false),
            TradingCoachConstants.Disclaimer,
            Now,
            TradingCoachConstants.AgentVersion,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            metrics);

    public static TradingCoachScores Scores(bool isRuleBased = false) => new(
        80, 80, 80, 80, 80, 80,
        [new TradingCoachScoreExplanation("PlanAdherence", 80, ["Documented plan"], 0.8m, isRuleBased)]);

    public static string ValidResponseJson(
        string summary = "The supplied process is documented and can be reviewed educationally.",
        int score = 80,
        string? recommendation = null,
        string relatedFindingCode = "AI_PROCESS_NOTE") => JsonSerializer.Serialize(new
        {
            summary,
            dataQuality = "high",
            strengths = new[] { "The plan is documented." },
            ruleViolations = new[]
            {
                new { code = "AI_PROCESS_NOTE", category = "process", message = "Review consistency across journal entries.", severity = "Information" }
            },
            riskObservations = new[] { "Risk values should be interpreted from supplied data only." },
            executionObservations = new[] { "The execution notes provide reviewable context." },
            psychologyObservations = new[] { "The emotional notes are concise." },
            missingInformation = Array.Empty<string>(),
            priorityIssues = Array.Empty<object>(),
            recommendedActions = recommendation is null
                ? Array.Empty<object>()
                : new object[] { new { code = "AI_ACTION", action = recommendation, relatedFindingCodes = new[] { relatedFindingCode } } },
            nextTradeChecklist = new[] { "Complete the journal before reviewing the outcome." },
            scores = new
            {
                planAdherence = score,
                riskDiscipline = score,
                executionQuality = score,
                emotionalControl = score,
                journalCompleteness = score,
                overallProcessQuality = score,
                explanations = new[]
                {
                    new { scoreName = "PlanAdherence", value = score, factors = new[] { "Documented plan" }, confidence = 0.8m }
                }
            },
            disclaimer = TradingCoachConstants.Disclaimer
        });

    public static AIAgentExecutionResponse AgentResponse(string? content = null) => new(
        new AIAgentId(TradingCoachConstants.AgentId),
        AIAgentVersion.Parse(TradingCoachConstants.AgentVersion),
        "session",
        "conversation",
        "correlation",
        TradingCoachConstants.Scenario,
        true,
        content ?? ValidResponseJson(),
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
        _execute = execute ?? ((_, _) => Task.FromResult(TradingCoachTestData.AgentResponse()));
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

internal sealed class RecordingTimeProvider : TimeProvider
{
    public int UtcNowCalls { get; private set; }
    public int TimestampCalls { get; private set; }

    public override DateTimeOffset GetUtcNow()
    {
        UtcNowCalls++;
        return TradingCoachTestData.Now.AddMilliseconds(UtcNowCalls);
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

internal sealed class CountingValidator(ITradingJournalValidator inner) : ITradingJournalValidator
{
    public int Calls { get; private set; }
    public void Validate(TradingJournalAnalysisRequest request, Guid analysisId, string? correlationId = null)
    {
        Calls++;
        inner.Validate(request, analysisId, correlationId);
    }
}

internal sealed class CountingNormalizer(ITradingJournalNormalizer inner) : ITradingJournalNormalizer
{
    public int Calls { get; private set; }
    public TradingJournalNormalizationResult Normalize(TradingJournalAnalysisRequest request)
    {
        Calls++;
        return inner.Normalize(request);
    }
}

internal sealed class CountingMetricsCalculator(ITradeMetricsCalculator inner) : ITradeMetricsCalculator
{
    public int Calls { get; private set; }
    public TradeMetricsResult Calculate(TradingJournalNormalizationResult result)
    {
        Calls++;
        return inner.Calculate(result);
    }
}

internal sealed class CountingRuleAnalyzer(ITradingCoachRuleAnalyzer inner) : ITradingCoachRuleAnalyzer
{
    public int Calls { get; private set; }
    public TradingCoachRuleAnalysisResult Analyze(
        TradingJournalNormalizationResult normalizationResult,
        TradeMetricsResult metrics,
        TradingCoachProfile profile)
    {
        Calls++;
        return inner.Analyze(normalizationResult, metrics, profile);
    }
}

internal sealed class CountingParser(ITradingCoachResponseParser inner) : ITradingCoachResponseParser
{
    public int Calls { get; private set; }
    public TradingCoachAnalysis Parse(string response, Guid analysisId, DateTimeOffset generatedAtUtc, string agentVersion, string? correlationId = null)
    {
        Calls++;
        return inner.Parse(response, analysisId, generatedAtUtc, agentVersion, correlationId);
    }
}

internal sealed class CountingMerger(ITradingCoachAnalysisMerger inner) : ITradingCoachAnalysisMerger
{
    public int Calls { get; private set; }
    public TradingCoachAnalysis Merge(
        TradingCoachAnalysis aiAnalysis,
        TradingCoachRuleAnalysisResult ruleAnalysis,
        TradingJournalNormalizationResult normalizationResult,
        TradeMetricsResult metrics,
        TradingCoachExecutionOptions options)
    {
        Calls++;
        return inner.Merge(aiAnalysis, ruleAnalysis, normalizationResult, metrics, options);
    }
}

internal sealed class CountingSafetyFilter(ITradingCoachSafetyFilter inner) : ITradingCoachSafetyFilter
{
    public int Calls { get; private set; }
    public TradingCoachAnalysis Validate(TradingCoachAnalysis analysis, string? correlationId = null)
    {
        Calls++;
        return inner.Validate(analysis, correlationId);
    }
}

internal sealed class ServiceHarness
{
    public ServiceHarness(StubAgentExecutor? executor = null)
    {
        var validationOptions = Options.Create(new TradingJournalValidationOptions());
        Validator = new CountingValidator(new TradingJournalValidator(validationOptions));
        Normalizer = new CountingNormalizer(new TradingJournalNormalizer());
        Metrics = new CountingMetricsCalculator(new TradeMetricsCalculator());
        Rules = new CountingRuleAnalyzer(new TradingCoachRuleAnalyzer(
            new TradingBehaviorPatternDetector(),
            new TradingCoachScoreCalculator()));
        Executor = executor ?? new StubAgentExecutor();
        Parser = new CountingParser(new TradingCoachResponseParser());
        Merger = new CountingMerger(new TradingCoachAnalysisMerger());
        Safety = new CountingSafetyFilter(new TradingCoachSafetyFilter(Options.Create(new TradingCoachSafetyOptions())));
        TimeProvider = new RecordingTimeProvider();
        Logger = new RecordingLogger<TradingCoachService>();
        Service = new TradingCoachService(
            Validator,
            Normalizer,
            Metrics,
            Rules,
            Executor,
            Parser,
            Merger,
            Safety,
            validationOptions,
            TimeProvider,
            Logger);
    }

    public CountingValidator Validator { get; }
    public CountingNormalizer Normalizer { get; }
    public CountingMetricsCalculator Metrics { get; }
    public CountingRuleAnalyzer Rules { get; }
    public StubAgentExecutor Executor { get; }
    public CountingParser Parser { get; }
    public CountingMerger Merger { get; }
    public CountingSafetyFilter Safety { get; }
    public RecordingTimeProvider TimeProvider { get; }
    public RecordingLogger<TradingCoachService> Logger { get; }
    public TradingCoachService Service { get; }

    public Task<TradingCoachAnalysis> AnalyzeAsync(
        TradingCoachExecutionOptions? options = null,
        TradingJournalAnalysisRequest? request = null,
        CancellationToken cancellationToken = default) => Service.AnalyzeAsync(
            request ?? TradingCoachTestData.CompleteRequest(),
            new TradingCoachProfile(),
            options ?? new TradingCoachExecutionOptions(),
            cancellationToken);
}
