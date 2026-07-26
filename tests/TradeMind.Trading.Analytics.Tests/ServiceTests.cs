using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.Agents;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics.Tests;

public sealed class ServiceTests
{
    [Fact]
    public async Task Case130_CallsValidatorOnce()
    {
        var counter = new CountingAnalyticsValidator(new TradingJournalAnalyticsValidator(
            TradingAnalyticsTestData.TradeValidator(), new TradingJournalNormalizer()));
        using var provider = Provider(false, services => services.AddSingleton<ITradingJournalAnalyticsValidator>(counter));
        await Analyze(provider);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task Case131_CallsNormalizerOnce()
    {
        var counter = new CountingCollectionNormalizer(new TradingJournalCollectionNormalizer(new TradingJournalNormalizer()));
        using var provider = Provider(false, services => services.AddSingleton<ITradingJournalCollectionNormalizer>(counter));
        await Analyze(provider);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task Case132_CallsStatisticsCalculator()
    {
        var counter = new CountingStatisticsCalculator(new TradingStatisticsCalculator());
        using var provider = Provider(false, services => services.AddSingleton<ITradingStatisticsCalculator>(counter));
        await Analyze(provider);
        Assert.True(counter.AggregateCalls > 0);
        Assert.True(counter.DrawdownCalls > 0);
    }

    [Fact]
    public async Task Case133_CallsBehaviorAnalyzer()
    {
        var counter = new CountingBehaviorAnalyzer(new TradingBehaviorTrendAnalyzer());
        using var provider = Provider(false, services => services.AddSingleton<ITradingBehaviorTrendAnalyzer>(counter));
        await Analyze(provider);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task Case134_CallsRiskDriftAnalyzer()
    {
        var counter = new CountingRiskAnalyzer(new RiskDriftAnalyzer(new TradingStatisticsCalculator()));
        using var provider = Provider(false, services => services.AddSingleton<IRiskDriftAnalyzer>(counter));
        await Analyze(provider);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task Case135_CallsPostOutcomeAnalyzer()
    {
        var counter = new CountingPostOutcomeAnalyzer(new PostOutcomeBehaviorAnalyzer(new TradingStatisticsCalculator()));
        using var provider = Provider(false, services => services.AddSingleton<IPostOutcomeBehaviorAnalyzer>(counter));
        await Analyze(provider);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task Case136_CallsRuleAnalyzer()
    {
        var counter = new CountingRuleAnalyzer(new TradingJournalRuleAnalyzer());
        using var provider = Provider(false, services => services.AddSingleton<ITradingJournalRuleAnalyzer>(counter));
        await Analyze(provider);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task Case137_CallsAgentOnceWhenEnabled()
    {
        var executor = new StubAgentExecutor();
        using var provider = Provider(true, executor: executor);
        await Analyze(provider);
        Assert.Equal(1, executor.Calls);
    }

    [Fact]
    public async Task Case138_DoesNotCallAgentWhenDisabled()
    {
        var executor = new StubAgentExecutor();
        using var provider = Provider(false, executor: executor);
        await Analyze(provider);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Case139_CallsParserOnce()
    {
        var counter = new CountingParser(new TradingJournalAnalyticsResponseParser(
            Options.Create(TradingAnalyticsTestData.Options(includeAi: true))));
        using var provider = Provider(true, services => services.AddSingleton<ITradingJournalAnalyticsResponseParser>(counter));
        await Analyze(provider);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task Case140_CallsMergerOnce()
    {
        var counter = new CountingMerger(new TradingJournalAnalyticsMerger());
        using var provider = Provider(false, services => services.AddSingleton<ITradingJournalAnalyticsMerger>(counter));
        await Analyze(provider);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task Case141_CallsSafetyFilter()
    {
        var counter = new CountingSafety(new TradingJournalAnalyticsSafetyFilter(
            Options.Create(new TradingJournalAnalyticsSafetyOptions())));
        using var provider = Provider(false, services => services.AddSingleton<ITradingJournalAnalyticsSafetyFilter>(counter));
        await Analyze(provider);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task Case142_PropagatesCancellation()
    {
        using var provider = Provider(false);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetRequiredService<ITradingJournalAnalyticsService>().AnalyzeAsync(TradingAnalyticsTestData.Request(), source.Token));
    }

    [Fact]
    public async Task Case143_DistinguishesTimeout()
    {
        var executor = new StubAgentExecutor((_, _) => Task.FromException<AIAgentExecutionResponse>(
            new AIAgentTimeoutException(new AIAgentId(TradingJournalAnalyticsConstants.AgentId), new AIAgentVersion(1, 0, 0), "correlation")));
        using var provider = Provider(true, executor: executor);
        await Assert.ThrowsAsync<TradingJournalAnalyticsTimeoutException>(() => Analyze(provider));
    }

    [Fact]
    public async Task Case144_UsesTimeProvider()
    {
        var time = new RecordingTimeProvider();
        using var provider = Provider(false, services => services.AddSingleton<TimeProvider>(time));
        await Analyze(provider);
        Assert.True(time.UtcNowCalls > 0);
        Assert.True(time.TimestampCalls > 0);
    }

    [Fact]
    public async Task Case145_GeneratesAnalysisId()
    {
        using var provider = Provider(false);
        var report = await Analyze(provider);
        Assert.NotEqual(Guid.Empty, report.AnalysisId);
    }

    [Fact]
    public async Task Case146_PropagatesCorrelationId()
    {
        var executor = new StubAgentExecutor();
        using var provider = Provider(true, executor: executor);
        await Analyze(provider);
        Assert.Equal("correlation", executor.LastRequest!.CorrelationId);
    }

    [Fact]
    public async Task Case147_WorksWithoutMemory()
    {
        var executor = new StubAgentExecutor();
        using var provider = Provider(true, executor: executor);
        await Analyze(provider);
        Assert.False(executor.LastRequest!.MemoryOptions!.Enabled);
    }

    [Fact]
    public async Task Case148_WorksWithoutKnowledge()
    {
        var executor = new StubAgentExecutor();
        using var provider = Provider(true, executor: executor);
        await Analyze(provider);
        Assert.False(executor.LastRequest!.KnowledgeOptions!.Enabled);
    }

    [Fact]
    public async Task Case149_DoesNotRequireToolEngine()
    {
        var executor = new StubAgentExecutor();
        using var provider = Provider(true, executor: executor);
        await Analyze(provider);
        Assert.Null(executor.LastRequest!.ToolInvocation);
    }

    [Fact]
    public async Task Case150_DoesNotLogJournalContent()
    {
        const string sensitiveNote = "private-emotion-note-987";
        var logger = new RecordingLogger<TradingJournalAnalyticsService>();
        using var provider = Provider(false, services => services.AddSingleton<ILogger<TradingJournalAnalyticsService>>(logger));
        var request = TradingAnalyticsTestData.Request([
            TradingAnalyticsTestData.Trade(0, behavior: sensitiveNote),
            TradingAnalyticsTestData.Trade(1),
            TradingAnalyticsTestData.Trade(2)]);
        await provider.GetRequiredService<ITradingJournalAnalyticsService>().AnalyzeAsync(request, CancellationToken.None);
        Assert.DoesNotContain(logger.Messages, message => message.Contains(sensitiveNote, StringComparison.Ordinal));
    }

    private static async Task<TradingJournalAnalyticsReport> Analyze(ServiceProvider provider) =>
        await provider.GetRequiredService<ITradingJournalAnalyticsService>()
            .AnalyzeAsync(TradingAnalyticsTestData.Request(), CancellationToken.None);

    private static ServiceProvider Provider(
        bool includeAi,
        Action<IServiceCollection>? configureServices = null,
        StubAgentExecutor? executor = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAIAgentExecutor>(executor ?? new StubAgentExecutor());
        configureServices?.Invoke(services);
        services.AddTradeMindTradingAnalytics(options =>
        {
            options.IncludeAIInterpretation = includeAi;
            options.MinimumTradesForTrend = 3;
            options.MinimumTradesPerGroup = 2;
            options.MinimumDataCompleteness = 50;
        });
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class CountingAnalyticsValidator(ITradingJournalAnalyticsValidator inner) : ITradingJournalAnalyticsValidator
    {
        public int Calls { get; private set; }
        public TradingJournalAnalyticsValidationResult Validate(TradingJournalAnalyticsRequest request, TradingJournalAnalyticsOptions options, Guid analysisId)
        {
            Calls++;
            return inner.Validate(request, options, analysisId);
        }
    }

    private sealed class CountingCollectionNormalizer(ITradingJournalCollectionNormalizer inner) : ITradingJournalCollectionNormalizer
    {
        public int Calls { get; private set; }
        public TradingJournalCollectionNormalizationResult Normalize(TradingJournalAnalyticsValidationResult result)
        {
            Calls++;
            return inner.Normalize(result);
        }
    }

    private sealed class CountingStatisticsCalculator(ITradingStatisticsCalculator inner) : ITradingStatisticsCalculator
    {
        public int AggregateCalls { get; private set; }
        public int DrawdownCalls { get; private set; }
        public decimal? Mean(IEnumerable<decimal> values) => inner.Mean(values);
        public decimal? Median(IEnumerable<decimal> values) => inner.Median(values);
        public decimal? Minimum(IEnumerable<decimal> values) => inner.Minimum(values);
        public decimal? Maximum(IEnumerable<decimal> values) => inner.Maximum(values);
        public decimal Sum(IEnumerable<decimal> values) => inner.Sum(values);
        public decimal Rate(int numerator, int denominator) => inner.Rate(numerator, denominator);
        public TimeSpan? AverageDuration(IEnumerable<TimeSpan> values) => inner.AverageDuration(values);
        public TradingJournalAggregateMetrics CalculateAggregates(IReadOnlyList<TradingJournalTradeAnalysis> trades)
        {
            AggregateCalls++;
            return inner.CalculateAggregates(trades);
        }

        public HistoricalRDrawdown CalculateHistoricalDrawdown(IReadOnlyList<TradingJournalTradeAnalysis> trades)
        {
            DrawdownCalls++;
            return inner.CalculateHistoricalDrawdown(trades);
        }

        public TradingStreakMetrics CalculateStreaks(IReadOnlyList<TradingJournalTradeAnalysis> trades, TradingCoachProfile profile, decimal threshold) =>
            inner.CalculateStreaks(trades, profile, threshold);

        public IReadOnlyList<TradingScoreEvolution> CalculateScoreEvolution(
            IReadOnlyList<TradingJournalTradeAnalysis> trades,
            IReadOnlyList<TradingPeriodAggregate> periods,
            int minimum) => inner.CalculateScoreEvolution(trades, periods, minimum);
    }

    private sealed class CountingBehaviorAnalyzer(ITradingBehaviorTrendAnalyzer inner) : ITradingBehaviorTrendAnalyzer
    {
        public int Calls { get; private set; }
        public IReadOnlyList<TradingBehaviorTrend> Analyze(IReadOnlyList<TradingJournalTradeAnalysis> trades, int minimum, int maximum)
        {
            Calls++;
            return inner.Analyze(trades, minimum, maximum);
        }
    }

    private sealed class CountingRiskAnalyzer(IRiskDriftAnalyzer inner) : IRiskDriftAnalyzer
    {
        public int Calls { get; private set; }
        public RiskDriftAnalysis Analyze(IReadOnlyList<TradingJournalTradeAnalysis> trades, TradingCoachProfile profile, int minimum)
        {
            Calls++;
            return inner.Analyze(trades, profile, minimum);
        }
    }

    private sealed class CountingPostOutcomeAnalyzer(IPostOutcomeBehaviorAnalyzer inner) : IPostOutcomeBehaviorAnalyzer
    {
        public int Calls { get; private set; }
        public IReadOnlyList<PostOutcomeBehaviorObservation> Analyze(IReadOnlyList<TradingJournalTradeAnalysis> trades, int minimum, string language)
        {
            Calls++;
            return inner.Analyze(trades, minimum, language);
        }
    }

    private sealed class CountingRuleAnalyzer(ITradingJournalRuleAnalyzer inner) : ITradingJournalRuleAnalyzer
    {
        public int Calls { get; private set; }
        public TradingJournalRuleAnalysisResult Analyze(
            TradingJournalAggregateMetrics aggregates,
            TradingStreakMetrics streaks,
            IReadOnlyList<TradingPeriodAggregate> periods,
            IReadOnlyList<TradingSetupAnalytics> setups,
            IReadOnlyList<TradingBehaviorTrend> behaviors,
            RiskDriftAnalysis riskDrift,
            IReadOnlyList<TradingScoreEvolution> scores,
            TradingJournalDataQuality quality,
            TradingCoachProfile profile)
        {
            Calls++;
            return inner.Analyze(aggregates, streaks, periods, setups, behaviors, riskDrift, scores, quality, profile);
        }
    }

    private sealed class CountingMerger(ITradingJournalAnalyticsMerger inner) : ITradingJournalAnalyticsMerger
    {
        public int Calls { get; private set; }
        public TradingJournalAnalyticsReport Merge(
            TradingJournalAnalyticsReport report,
            TradingJournalRuleAnalysisResult rules,
            TradingJournalAIInterpretation? interpretation,
            TradingJournalAnalyticsOptions options)
        {
            Calls++;
            return inner.Merge(report, rules, interpretation, options);
        }
    }

    private sealed class CountingSafety(ITradingJournalAnalyticsSafetyFilter inner) : ITradingJournalAnalyticsSafetyFilter
    {
        public int Calls { get; private set; }
        public TradingJournalAnalyticsReport Validate(TradingJournalAnalyticsReport report, string? correlationId = null)
        {
            Calls++;
            return inner.Validate(report, correlationId);
        }
    }
}
