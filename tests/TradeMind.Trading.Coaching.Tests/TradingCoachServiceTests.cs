using TradeMind.AI.Agents;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Coaching.Tests;

public sealed class TradingCoachServiceTests
{
    [Fact]
    public async Task Case083_CallsValidatorOnce()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync();
        Assert.Equal(1, harness.Validator.Calls);
    }

    [Fact]
    public async Task Case084_CallsNormalizerOnce()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync();
        Assert.Equal(1, harness.Normalizer.Calls);
    }

    [Fact]
    public async Task Case085_CallsCalculatorOnce()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync();
        Assert.Equal(1, harness.Metrics.Calls);
    }

    [Fact]
    public async Task Case086_CallsRuleAnalyzerOnce()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync();
        Assert.Equal(1, harness.Rules.Calls);
    }

    [Fact]
    public async Task Case087_CallsAgentExecutorOnce()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync();
        Assert.Equal(1, harness.Executor.Calls);
    }

    [Fact]
    public async Task Case088_CallsParserOnce()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync();
        Assert.Equal(1, harness.Parser.Calls);
    }

    [Fact]
    public async Task Case089_CallsMergerOnce()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync();
        Assert.Equal(1, harness.Merger.Calls);
    }

    [Fact]
    public async Task Case090_CallsSafetyFilterOnce()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync();
        Assert.Equal(1, harness.Safety.Calls);
    }

    [Fact]
    public async Task Case091_DoesNotRetryAgentAfterException()
    {
        var executor = new StubAgentExecutor((_, _) => throw new InvalidOperationException("provider failed"));
        var harness = new ServiceHarness(executor);
        await Assert.ThrowsAsync<TradingCoachAnalysisException>(() => harness.AnalyzeAsync());
        Assert.Equal(1, executor.Calls);
    }

    [Fact]
    public async Task Case092_PropagatesCallerCancellation()
    {
        var executor = new StubAgentExecutor((_, token) => Task.FromCanceled<AIAgentExecutionResponse>(token));
        var harness = new ServiceHarness(executor);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.AnalyzeAsync(cancellationToken: source.Token));
    }

    [Fact]
    public async Task Case093_DistinguishesTimeout()
    {
        var executor = new StubAgentExecutor((request, _) => throw new AIAgentTimeoutException(
            request.AgentId,
            request.ExactVersion!,
            request.CorrelationId));
        var harness = new ServiceHarness(executor);
        await Assert.ThrowsAsync<TradingCoachTimeoutException>(() => harness.AnalyzeAsync());
    }

    [Fact]
    public async Task Case094_UsesTimeProvider()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync();
        Assert.True(harness.TimeProvider.UtcNowCalls >= 2);
        Assert.True(harness.TimeProvider.TimestampCalls >= 2);
    }

    [Fact]
    public async Task Case095_GeneratesAnalysisId()
    {
        var harness = new ServiceHarness();
        var result = await harness.AnalyzeAsync();
        Assert.NotEqual(Guid.Empty, result.AnalysisId);
    }

    [Fact]
    public async Task Case096_PropagatesCorrelationId()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync(new TradingCoachExecutionOptions(correlationId: "correlation-42"));
        Assert.Equal("correlation-42", harness.Executor.LastRequest!.CorrelationId);
    }

    [Fact]
    public async Task Case097_WorksWithoutMemory()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync();
        Assert.False(harness.Executor.LastRequest!.MemoryOptions!.Enabled);
    }

    [Fact]
    public async Task Case098_WorksWithOptionalMemory()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync(new TradingCoachExecutionOptions(
            includeMemory: true,
            conversationId: "conversation-1"));
        Assert.True(harness.Executor.LastRequest!.MemoryOptions!.Enabled);
    }

    [Fact]
    public async Task Case099_WorksWithoutKnowledge()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync();
        Assert.False(harness.Executor.LastRequest!.KnowledgeOptions!.Enabled);
    }

    [Fact]
    public async Task Case100_WorksWithFakeKnowledgeBoundary()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync(new TradingCoachExecutionOptions(includeKnowledge: true));
        Assert.True(harness.Executor.LastRequest!.KnowledgeOptions!.Enabled);
        Assert.Equal("educational-coaching", harness.Executor.LastRequest.KnowledgeOptions.Filters["contentPurpose"]);
    }

    [Fact]
    public async Task Case101_DoesNotRequireToolEngine()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync();
        Assert.Null(harness.Executor.LastRequest!.ToolInvocation);
    }

    [Fact]
    public async Task Case102_DoesNotLogJournalContent()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync(request: TradingCoachTestData.CompleteRequest(
            executionNotes: "PRIVATE-JOURNAL-CONTENT"));
        Assert.DoesNotContain(harness.Logger.Messages, message => message.Contains("PRIVATE-JOURNAL-CONTENT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Case103_DoesNotLogPrices()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync(request: new TradingJournalAnalysisRequest(
            entryPrice: 9876.54321m,
            planBeforeTrade: "documented",
            entryReason: "documented"));
        Assert.DoesNotContain(harness.Logger.Messages, message => message.Contains("9876.54321", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Case104_DoesNotLogProfitAndLoss()
    {
        var harness = new ServiceHarness();
        await harness.AnalyzeAsync(request: TradingCoachTestData.CompleteRequest(
            resultAmount: 7654.321m,
            resultRMultiple: 76.54321m));
        Assert.DoesNotContain(harness.Logger.Messages, message => message.Contains("7654.321", StringComparison.Ordinal));
    }
}
