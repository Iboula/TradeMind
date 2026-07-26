using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TradeMind.AI.Agents;

namespace TradeMind.Trading.Analytics.Tests;

public sealed class AgentAndParserTests
{
    [Fact]
    public async Task Case106_RegistersJournalAnalysisV1()
    {
        using var provider = CreateProvider();
        var agent = await provider.GetRequiredService<IAIAgentRegistry>().GetAsync(
            new AIAgentId(TradingJournalAnalyticsConstants.AgentId),
            AIAgentVersionSelection.Exact,
            new AIAgentVersion(1, 0, 0),
            CancellationToken.None);
        Assert.Equal("1.0.0", agent.Definition.Version.ToString());
        Assert.Contains("aggregateMetrics", agent.Definition.PromptPolicy.RequiredVariables);
    }

    [Fact]
    public async Task Case107_LatestStableReturnsV1()
    {
        using var provider = CreateProvider();
        var agent = await provider.GetRequiredService<IAIAgentRegistry>().GetAsync(
            new AIAgentId(TradingJournalAnalyticsConstants.AgentId),
            AIAgentVersionSelection.LatestStable,
            null,
            CancellationToken.None);
        Assert.Equal(new AIAgentVersion(1, 0, 0), agent.Definition.Version);
    }

    [Fact]
    public void Case108_StructuredOutputIsEnabled() => Assert.True(Definition().Capabilities.SupportsStructuredOutput);

    [Fact]
    public void Case109_ToolsAreDisabled()
    {
        Assert.False(Definition().Capabilities.SupportsTools);
        Assert.False(Definition().ToolPolicy.Enabled);
    }

    [Fact]
    public void Case110_StreamingIsDisabled() => Assert.False(Definition().Capabilities.SupportsStreaming);

    [Fact]
    public void Case111_AutonomousExecutionIsDisabled() => Assert.False(Definition().Capabilities.SupportsAutonomousExecution);

    [Fact]
    public void Case112_ParserAcceptsValidJson()
    {
        var value = Parser().Parse(TradingAnalyticsTestData.ValidAiJson(), "en", Guid.NewGuid());
        Assert.NotEmpty(value.Summary);
    }

    [Fact]
    public void Case113_ParserRejectsInvalidJson()
    {
        Assert.Throws<TradingJournalAnalyticsParsingException>(() => Parser().Parse("not-json", "en", Guid.NewGuid()));
    }

    [Fact]
    public void Case114_ParserRejectsMissingCriticalField()
    {
        var json = "{\"explanations\":[],\"recommendations\":[],\"nextReviewChecklist\":[],\"disclaimer\":\"educational\",\"language\":\"en\"}";
        Assert.Throws<TradingJournalAnalyticsParsingException>(() => Parser().Parse(json, "en", Guid.NewGuid()));
    }

    [Fact]
    public void Case115_ParserRejectsSignal()
    {
        var json = TradingAnalyticsTestData.ValidAiJson(summary: "Buy EURUSD now.");
        Assert.Throws<TradingJournalAnalyticsParsingException>(() => Parser().Parse(json, "en", Guid.NewGuid()));
    }

    [Fact]
    public void Case116_ParserRejectsPromise()
    {
        var json = TradingAnalyticsTestData.ValidAiJson(recommendation: "This is a guaranteed profit.");
        Assert.Throws<TradingJournalAnalyticsParsingException>(() => Parser().Parse(json, "en", Guid.NewGuid()));
    }

    [Fact]
    public void Case117_ParserDoesNotRevealRawResponse()
    {
        const string secretRawValue = "raw-provider-value-123";
        var exception = Assert.Throws<TradingJournalAnalyticsParsingException>(() =>
            Parser().Parse(secretRawValue, "en", Guid.NewGuid()));
        Assert.DoesNotContain(secretRawValue, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Case118_ProviderIsCalledOnce()
    {
        var executor = new StubAgentExecutor();
        var interpreter = new TradingJournalAIInterpreter(new TradingJournalAgentRequestFactory(), executor, Parser());
        await interpreter.InterpretAsync(
            TradingJournalAggregateMetrics.Empty,
            TradingAnalyticsTestData.DataQuality(),
            [],
            TradingAnalyticsTestData.RiskDrift(),
            [],
            [],
            TradingAnalyticsTestData.Rules(),
            TradingAnalyticsTestData.Request(),
            TradingAnalyticsTestData.Options(includeAi: true),
            Guid.NewGuid(),
            TradingAnalyticsTestData.Now,
            CancellationToken.None);
        Assert.Equal(1, executor.Calls);
        Assert.Null(executor.LastRequest!.ToolInvocation);
    }

    private static TradingJournalAnalyticsResponseParser Parser() => new(
        Options.Create(TradingAnalyticsTestData.Options()));

    private static AIAgentDefinition Definition() => new TradingJournalAnalyticsAgentV1().Definition;

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAIAgentExecutor>(new StubAgentExecutor());
        services.AddTradeMindTradingAnalytics();
        return services.BuildServiceProvider(validateScopes: true);
    }
}
