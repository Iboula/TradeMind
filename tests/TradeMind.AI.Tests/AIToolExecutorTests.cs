using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Tests;

public sealed class AIToolExecutorTests
{
    [Fact]
    public async Task Executor_ShouldExecuteEchoTool()
    {
        var tool = new EchoAITool();
        var result = await Executor(tool, new AIToolEngineOptions { EnableDevelopmentTools = true })
            .ExecuteAsync(
                AIToolTestData.Request(
                    tool.Definition.Id,
                    new Dictionary<string, System.Text.Json.JsonElement> { ["text"] = AIToolTestData.Json("hello") }),
                AIToolTestData.Authorization(),
                CancellationToken.None);

        Assert.Equal("hello", result.Output!.Value.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Executor_ShouldExecuteAddNumbersTool()
    {
        var tool = new AddNumbersAITool();
        var result = await Executor(tool).ExecuteAsync(
            AIToolTestData.Request(
                tool.Definition.Id,
                new Dictionary<string, System.Text.Json.JsonElement>
                {
                    ["left"] = AIToolTestData.Json("1.25"),
                    ["right"] = AIToolTestData.Json("2.75")
                }),
            AIToolTestData.Authorization(),
            CancellationToken.None);

        Assert.Equal(4m, result.Output!.Value.GetProperty("sum").GetDecimal());
    }

    [Fact]
    public async Task Executor_ShouldExecuteToolOnlyOnce()
    {
        var tool = new StubAITool(AIToolTestData.Definition());
        await Executor(tool).ExecuteAsync(AIToolTestData.Request(), AIToolTestData.Authorization(), CancellationToken.None);
        Assert.Equal(1, tool.CallCount);
    }

    [Fact]
    public async Task Executor_ShouldPropagateSessionId()
    {
        var tool = await ExecuteCapturingContext(AIToolTestData.Request(sessionId: "session-42"));
        Assert.Equal("session-42", tool.LastContext!.Request.SessionId);
    }

    [Fact]
    public async Task Executor_ShouldPropagateCorrelationId()
    {
        var tool = await ExecuteCapturingContext(AIToolTestData.Request(correlationId: "correlation-42"));
        Assert.Equal("correlation-42", tool.LastContext!.Request.CorrelationId);
    }

    [Fact]
    public async Task Executor_ShouldPropagateTenantId()
    {
        var tool = await ExecuteCapturingContext(AIToolTestData.Request(tenantId: "tenant-42"));
        Assert.Equal("tenant-42", tool.LastContext!.Request.TenantId);
    }

    [Fact]
    public async Task Executor_ShouldPropagateUserId()
    {
        var tool = await ExecuteCapturingContext(AIToolTestData.Request(userId: "user-42"));
        Assert.Equal("user-42", tool.LastContext!.Request.UserId);
    }

    [Fact]
    public async Task Executor_ShouldPropagateAgentId()
    {
        var tool = await ExecuteCapturingContext(AIToolTestData.Request(agentId: "agent-42"));
        Assert.Equal("agent-42", tool.LastContext!.Request.AgentId);
    }

    [Fact]
    public async Task Executor_ShouldPropagateScenario()
    {
        var tool = await ExecuteCapturingContext(AIToolTestData.Request(scenario: "Coach"));
        Assert.Equal("Coach", tool.LastContext!.Request.Scenario);
    }

    [Fact]
    public async Task Executor_ShouldPropagateIdempotencyKey()
    {
        var result = await Executor(new StubAITool(AIToolTestData.Definition())).ExecuteAsync(
            AIToolTestData.Request(idempotencyKey: "stable-key"),
            AIToolTestData.Authorization(),
            CancellationToken.None);
        Assert.Equal("stable-key", result.IdempotencyKey);
    }

    [Fact]
    public async Task Executor_ShouldCalculateDuration()
    {
        var timeProvider = new AdvancingTimeProvider();
        var tool = new StubAITool(
            AIToolTestData.Definition(),
            (context, _) =>
            {
                var startedAt = context.TimeProvider.GetUtcNow();
                timeProvider.Advance(TimeSpan.FromMilliseconds(250));
                return Task.FromResult(AIToolExecutionResult.Succeeded(
                    context.Request.ToolId,
                    AIToolTestData.Json(new { ok = true }),
                    startedAt,
                    context.TimeProvider.GetUtcNow()));
            });

        var result = await Executor(tool, timeProvider: timeProvider).ExecuteAsync(
            AIToolTestData.Request(),
            AIToolTestData.Authorization(),
            CancellationToken.None);

        Assert.Equal(TimeSpan.FromMilliseconds(250), result.Duration);
    }

    [Fact]
    public async Task Executor_ShouldUseInjectedTimeProvider()
    {
        var timeProvider = new AdvancingTimeProvider();
        var tool = new StubAITool(AIToolTestData.Definition());
        await Executor(tool, timeProvider: timeProvider).ExecuteAsync(
            AIToolTestData.Request(),
            AIToolTestData.Authorization(),
            CancellationToken.None);
        Assert.Same(timeProvider, tool.LastContext!.TimeProvider);
    }

    [Fact]
    public async Task Executor_ShouldRespectDefaultToolTimeout()
    {
        var tool = BlockingTool(TimeSpan.FromMilliseconds(20));
        var exception = await Assert.ThrowsAsync<AIToolTimeoutException>(() => Executor(tool).ExecuteAsync(
            AIToolTestData.Request(),
            AIToolTestData.Authorization(),
            CancellationToken.None));
        Assert.Equal(TimeSpan.FromMilliseconds(20), exception.Timeout);
    }

    [Fact]
    public async Task Executor_ShouldRespectTimeoutOverride()
    {
        var tool = BlockingTool(TimeSpan.FromSeconds(1));
        var exception = await Assert.ThrowsAsync<AIToolTimeoutException>(() => Executor(tool).ExecuteAsync(
            AIToolTestData.Request(timeoutOverride: TimeSpan.FromMilliseconds(20)),
            AIToolTestData.Authorization(),
            CancellationToken.None));
        Assert.Equal(TimeSpan.FromMilliseconds(20), exception.Timeout);
    }

    [Fact]
    public async Task Executor_ShouldCapTimeoutOverride()
    {
        var options = new AIToolEngineOptions
        {
            DefaultTimeout = TimeSpan.FromMilliseconds(10),
            MaximumTimeout = TimeSpan.FromMilliseconds(25)
        };
        var tool = BlockingTool(TimeSpan.FromSeconds(1));
        var exception = await Assert.ThrowsAsync<AIToolTimeoutException>(() => Executor(tool, options).ExecuteAsync(
            AIToolTestData.Request(timeoutOverride: TimeSpan.FromSeconds(2)),
            AIToolTestData.Authorization(),
            CancellationToken.None));
        Assert.Equal(TimeSpan.FromMilliseconds(25), exception.Timeout);
    }

    [Fact]
    public async Task Executor_ShouldProduceControlledTimeout()
    {
        var exception = await Assert.ThrowsAsync<AIToolTimeoutException>(() => Executor(BlockingTool(TimeSpan.FromMilliseconds(20)))
            .ExecuteAsync(AIToolTestData.Request(), AIToolTestData.Authorization(), CancellationToken.None));

        Assert.Equal("AI_TOOL_TIMEOUT", exception.ErrorCode);
        Assert.True(exception.Metrics!.TimedOut);
        Assert.DoesNotContain("TaskCanceledException", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Executor_ShouldPropagateCallerCancellation()
    {
        var tool = new StubAITool(AIToolTestData.Definition());
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Executor(tool).ExecuteAsync(
            AIToolTestData.Request(),
            AIToolTestData.Authorization(),
            source.Token));
        Assert.Equal(0, tool.CallCount);
    }

    [Fact]
    public async Task Executor_ShouldNotWrapCallerOperationCanceledException()
    {
        var tool = BlockingTool(TimeSpan.FromSeconds(2));
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Executor(tool).ExecuteAsync(
            AIToolTestData.Request(),
            AIToolTestData.Authorization(),
            source.Token));
        Assert.IsNotType<AIToolExecutionException>(exception);
    }

    [Fact]
    public async Task Executor_ShouldPreserveInnerExceptionForInternalFailure()
    {
        var cause = new InvalidOperationException("internal failure");
        var tool = new StubAITool(AIToolTestData.Definition(), (_, _) => throw cause);
        var exception = await Assert.ThrowsAsync<AIToolExecutionException>(() => Executor(tool).ExecuteAsync(
            AIToolTestData.Request(),
            AIToolTestData.Authorization(),
            CancellationToken.None));
        Assert.Same(cause, exception.InnerException);
    }

    [Fact]
    public async Task Executor_ShouldNotLogArguments()
    {
        const string sensitiveArgument = "sensitive-argument";
        var recorder = new ToolRecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
        var definition = AIToolTestData.Definition(parameters:
        [
            new AIToolParameterDefinition("value", AIToolParameterType.String, true, "Value", isSensitive: true)
        ]);
        var tool = new StubAITool(definition);

        await Executor(tool, logger: loggerFactory.CreateLogger<AIToolExecutor>()).ExecuteAsync(
            AIToolTestData.Request(arguments: new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["value"] = AIToolTestData.Json(sensitiveArgument)
            }),
            AIToolTestData.Authorization(),
            CancellationToken.None);

        Assert.DoesNotContain(sensitiveArgument, string.Join(Environment.NewLine, recorder.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Executor_ShouldNotLogOutput()
    {
        const string sensitiveOutput = "sensitive-output";
        var recorder = new ToolRecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
        var tool = new StubAITool(
            AIToolTestData.Definition(),
            (context, _) =>
            {
                var now = context.TimeProvider.GetUtcNow();
                return Task.FromResult(AIToolExecutionResult.Succeeded(
                    context.Request.ToolId,
                    AIToolTestData.Json(new { value = sensitiveOutput }),
                    now,
                    now));
            });

        await Executor(tool, logger: loggerFactory.CreateLogger<AIToolExecutor>()).ExecuteAsync(
            AIToolTestData.Request(),
            AIToolTestData.Authorization(),
            CancellationToken.None);

        Assert.DoesNotContain(sensitiveOutput, string.Join(Environment.NewLine, recorder.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Executor_ShouldNotRetryFailedResult()
    {
        var tool = new StubAITool(
            AIToolTestData.Definition(),
            (context, _) =>
            {
                var now = context.TimeProvider.GetUtcNow();
                return Task.FromResult(AIToolExecutionResult.Failed(
                    context.Request.ToolId,
                    new AIToolError("EXPECTED_FAILURE", "Expected safe failure."),
                    now,
                    now));
            });

        var result = await Executor(tool).ExecuteAsync(
            AIToolTestData.Request(),
            AIToolTestData.Authorization(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, tool.CallCount);
    }

    [Fact]
    public async Task Executor_ShouldNotInvokeAgainAfterException()
    {
        var tool = new StubAITool(
            AIToolTestData.Definition(),
            (_, _) => throw new InvalidOperationException("failure"));

        await Assert.ThrowsAsync<AIToolExecutionException>(() => Executor(tool).ExecuteAsync(
            AIToolTestData.Request(),
            AIToolTestData.Authorization(),
            CancellationToken.None));
        Assert.Equal(1, tool.CallCount);
    }

    private static async Task<StubAITool> ExecuteCapturingContext(AIToolExecutionRequest request)
    {
        var tool = new StubAITool(AIToolTestData.Definition());
        await Executor(tool).ExecuteAsync(request, AIToolTestData.Authorization(), CancellationToken.None);
        return tool;
    }

    private static StubAITool BlockingTool(TimeSpan defaultTimeout) =>
        new(
            AIToolTestData.Definition(timeout: defaultTimeout),
            async (context, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                var now = context.TimeProvider.GetUtcNow();
                return AIToolExecutionResult.Succeeded(
                    context.Request.ToolId,
                    AIToolTestData.Json(new { completed = true }),
                    now,
                    now);
            });

    private static AIToolExecutor Executor(
        IAITool tool,
        AIToolEngineOptions? options = null,
        TimeProvider? timeProvider = null,
        ILogger<AIToolExecutor>? logger = null)
    {
        options ??= new AIToolEngineOptions();
        var configuredOptions = Options.Create(options);
        var registry = new InMemoryAIToolRegistry(
            [tool],
            configuredOptions,
            NullLogger<InMemoryAIToolRegistry>.Instance);
        return new AIToolExecutor(
            registry,
            new PermissionBasedAIToolAuthorizer(configuredOptions),
            new AIToolArgumentValidator(configuredOptions),
            configuredOptions,
            timeProvider ?? TimeProvider.System,
            logger ?? NullLogger<AIToolExecutor>.Instance);
    }
}
