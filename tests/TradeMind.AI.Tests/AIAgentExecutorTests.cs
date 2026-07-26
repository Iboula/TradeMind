using TradeMind.AI.Agents;

namespace TradeMind.AI.Tests;

public sealed class AIAgentExecutorTests
{
    [Fact]
    public async Task Executor_ResolvesAgentOnce()
    {
        var agent = Agent();
        var registry = new CountingAgentRegistry(agent);
        await AIAgentTestData.Executor(agent, registry: registry).ExecuteAsync(Request(), CancellationToken.None);
        Assert.Equal(1, registry.GetCount);
    }

    [Fact]
    public async Task Executor_AuthorizesOnce()
    {
        var agent = Agent();
        var authorizer = new CountingAgentAuthorizer();
        await AIAgentTestData.Executor(agent, authorizer: authorizer).ExecuteAsync(Request(), CancellationToken.None);
        Assert.Equal(1, authorizer.Count);
    }

    [Fact]
    public async Task Executor_CallsOrchestratorOnce()
    {
        var agent = Agent();
        var orchestrator = new CountingOrchestrator();
        await AIAgentTestData.Executor(agent, orchestrator).ExecuteAsync(Request(), CancellationToken.None);
        Assert.Equal(1, orchestrator.CallCount);
    }

    [Fact]
    public async Task Executor_CallsStartingHook()
    {
        var agent = Agent();
        await AIAgentTestData.Executor(agent).ExecuteAsync(Request(), CancellationToken.None);
        Assert.Equal(1, agent.StartingCount);
    }

    [Fact]
    public async Task Executor_CallsSuccessHook()
    {
        var agent = Agent();
        await AIAgentTestData.Executor(agent).ExecuteAsync(Request(), CancellationToken.None);
        Assert.Equal(1, agent.CompletedCount);
    }

    [Fact]
    public async Task Executor_CallsFailureHook()
    {
        var agent = Agent();
        var orchestrator = ThrowingOrchestrator();
        await Assert.ThrowsAsync<AIAgentExecutionException>(() =>
            AIAgentTestData.Executor(agent, orchestrator).ExecuteAsync(Request(), CancellationToken.None));
        Assert.Equal(1, agent.FailedCount);
    }

    [Fact]
    public async Task Executor_DoesNotRecallOrchestratorAfterException()
    {
        var agent = Agent();
        var orchestrator = ThrowingOrchestrator();
        await Assert.ThrowsAsync<AIAgentExecutionException>(() =>
            AIAgentTestData.Executor(agent, orchestrator).ExecuteAsync(Request(), CancellationToken.None));
        Assert.Equal(1, orchestrator.CallCount);
    }

    [Fact]
    public async Task Executor_PropagatesCallerCancellation()
    {
        var agent = Agent();
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AIAgentTestData.Executor(agent).ExecuteAsync(Request(), source.Token));
    }

    [Fact]
    public async Task Executor_DistinguishesAgentTimeout()
    {
        var agent = Agent();
        var orchestrator = new CountingOrchestrator(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return AIAgentTestData.OrchestrationResponse();
        });
        var request = AIAgentTestData.Request(timeout: TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAsync<AIAgentTimeoutException>(() =>
            AIAgentTestData.Executor(agent, orchestrator).ExecuteAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Executor_UsesTimeProvider()
    {
        var agent = Agent();
        var timeProvider = new RecordingTimeProvider();
        var response = AIAgentTestData.OrchestrationResponse(completedAtUtc: AIAgentTestData.Now.AddMilliseconds(2));
        await AIAgentTestData.Executor(
            agent,
            new CountingOrchestrator((_, _) => Task.FromResult(response)),
            timeProvider: timeProvider).ExecuteAsync(Request(), CancellationToken.None);
        Assert.True(timeProvider.UtcNowCalls >= 2);
        Assert.True(timeProvider.TimestampCalls >= 2);
    }

    [Fact]
    public async Task Executor_ComputesMetrics()
    {
        var response = await AIAgentTestData.Executor(Agent()).ExecuteAsync(Request(), CancellationToken.None);
        Assert.True(response.Metrics.TotalDuration >= TimeSpan.Zero);
        Assert.True(response.Metrics.OrchestrationDuration >= TimeSpan.Zero);
        Assert.Equal(5, response.Metrics.EstimatedInputTokens);
    }

    [Fact]
    public async Task Response_ExposesAgentId()
    {
        var response = await AIAgentTestData.Executor(Agent()).ExecuteAsync(Request(), CancellationToken.None);
        Assert.Equal("test-agent", response.AgentId.Value);
    }

    [Fact]
    public async Task Response_ExposesAgentVersion()
    {
        var response = await AIAgentTestData.Executor(Agent()).ExecuteAsync(Request(), CancellationToken.None);
        Assert.Equal("1.0.0", response.AgentVersion.ToString());
    }

    [Fact]
    public async Task Response_ExposesProvider()
    {
        var response = await AIAgentTestData.Executor(Agent()).ExecuteAsync(Request(), CancellationToken.None);
        Assert.Equal("Fake", response.Provider);
    }

    [Fact]
    public async Task Response_ExposesMemoryUsed()
    {
        var response = await ExecuteWithResponse(AIAgentTestData.OrchestrationResponse(
            memoryUsed: true,
            completedAtUtc: DateTimeOffset.UtcNow.AddSeconds(1)));
        Assert.True(response.MemoryUsed);
    }

    [Fact]
    public async Task Response_ExposesKnowledgeUsed()
    {
        var response = await ExecuteWithResponse(AIAgentTestData.OrchestrationResponse(
            knowledgeUsed: true,
            completedAtUtc: DateTimeOffset.UtcNow.AddSeconds(1)));
        Assert.True(response.KnowledgeUsed);
    }

    [Fact]
    public async Task Response_ExposesToolUsed()
    {
        var response = await ExecuteWithResponse(AIAgentTestData.OrchestrationResponse(
            toolUsed: true,
            toolId: "add-numbers",
            completedAtUtc: DateTimeOffset.UtcNow.AddSeconds(1)));
        Assert.True(response.ToolUsed);
    }

    [Fact]
    public void Response_DoesNotExposePrompt()
    {
        Assert.DoesNotContain(typeof(AIAgentExecutionResponse).GetProperties(), property =>
            property.Name.Contains("Prompt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Response_DoesNotExposeToolArguments()
    {
        Assert.DoesNotContain(typeof(AIAgentExecutionResponse).GetProperties(), property =>
            property.Name.Contains("Argument", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Response_DoesNotExposeKnowledgeFragments()
    {
        Assert.DoesNotContain(typeof(AIAgentExecutionResponse).GetProperties(), property =>
            property.Name.Contains("Fragment", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecutionException_PreservesInnerException()
    {
        var original = new InvalidOperationException("internal failure");
        var orchestrator = new CountingOrchestrator((_, _) => Task.FromException<TradeMind.AI.Application.AIOrchestrationResponse>(original));
        var exception = await Assert.ThrowsAsync<AIAgentExecutionException>(() =>
            AIAgentTestData.Executor(Agent(), orchestrator).ExecuteAsync(Request(), CancellationToken.None));
        Assert.Same(original, exception.InnerException);
    }

    [Fact]
    public async Task Executor_DoesNotRetryAutomatically()
    {
        var agent = Agent();
        var orchestrator = ThrowingOrchestrator();
        await Assert.ThrowsAsync<AIAgentExecutionException>(() =>
            AIAgentTestData.Executor(agent, orchestrator).ExecuteAsync(Request(), CancellationToken.None));
        Assert.Equal(1, orchestrator.CallCount);
    }

    private static StubAIAgent Agent() => new(AIAgentTestData.Definition());

    private static AIAgentExecutionRequest Request() => AIAgentTestData.Request();

    private static CountingOrchestrator ThrowingOrchestrator() =>
        new((_, _) => Task.FromException<TradeMind.AI.Application.AIOrchestrationResponse>(
            new InvalidOperationException("internal failure")));

    private static Task<AIAgentExecutionResponse> ExecuteWithResponse(
        TradeMind.AI.Application.AIOrchestrationResponse response)
    {
        var agent = Agent();
        return AIAgentTestData.Executor(
            agent,
            new CountingOrchestrator((_, _) => Task.FromResult(response)))
            .ExecuteAsync(Request(), CancellationToken.None);
    }
}
