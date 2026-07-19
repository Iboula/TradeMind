using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Application;
using TradeMind.AI.Knowledge;
using TradeMind.AI.Memory;
using TradeMind.AI.Tools;
using TradeMind.KnowledgeHub.Application;

namespace TradeMind.AI.Tests;

public sealed class AIToolOrchestrationTests
{
    [Fact]
    public async Task Orchestrator_ShouldNotAccessToolEngineWhenDisabled()
    {
        var executor = new CountingAIToolExecutor();
        var chat = new ToolFakeChatProvider();
        using var provider = Provider(chat, executor: executor);

        await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(
            new AIOrchestrationRequest("system", "user", "Scenario"),
            CancellationToken.None);

        Assert.Equal(0, executor.CallCount);
    }

    [Fact]
    public async Task Orchestrator_ShouldRejectEnabledToolWithoutToolId()
    {
        var executor = new CountingAIToolExecutor();
        var chat = new ToolFakeChatProvider();
        using var provider = Provider(chat, executor: executor);
        var request = new AIOrchestrationRequest("system", "user", "Scenario")
        {
            Tool = new AIToolInvocationOptions(enabled: true)
        };

        await Assert.ThrowsAsync<AIOrchestrationValidationException>(() =>
            provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(request, CancellationToken.None));
        Assert.Equal(0, executor.CallCount);
        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public async Task Orchestrator_ShouldExecuteToolBeforeProvider()
    {
        var sequence = new List<string>();
        var tool = SuccessfulTool((_, _) => sequence.Add("tool"));
        var chat = new ToolFakeChatProvider(sequence);
        using var provider = Provider(chat, tool);

        await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(["tool", "provider"], sequence);
    }

    [Fact]
    public async Task Orchestrator_ShouldGiveComposedToolResultToProvider()
    {
        var chat = new ToolFakeChatProvider();
        using var provider = Provider(chat, SuccessfulTool());

        await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(Request(), CancellationToken.None);

        Assert.Contains(chat.LastRequest!.Messages, message =>
            message.Content.Contains("Tool: test-tool", StringComparison.Ordinal)
            && message.Content.Contains("untrusted external data", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Orchestrator_ShouldCallProviderOnce()
    {
        var chat = new ToolFakeChatProvider();
        using var provider = Provider(chat, SuccessfulTool());
        await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(Request(), CancellationToken.None);
        Assert.Equal(1, chat.CallCount);
    }

    [Fact]
    public async Task Orchestrator_ShouldCallToolOnce()
    {
        var tool = SuccessfulTool();
        using var provider = Provider(new ToolFakeChatProvider(), tool);
        await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(Request(), CancellationToken.None);
        Assert.Equal(1, tool.CallCount);
    }

    [Fact]
    public async Task Orchestrator_ShouldPlaceToolResultBeforeCurrentUserMessage()
    {
        var chat = new ToolFakeChatProvider();
        using var provider = Provider(chat, SuccessfulTool());
        await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(Request(), CancellationToken.None);

        var messages = chat.LastRequest!.Messages;
        var toolIndex = messages.ToList().FindIndex(message => message.Content.Contains("Tool: test-tool", StringComparison.Ordinal));
        var userIndex = messages.ToList().FindLastIndex(message => message.Role == ChatRole.User);
        Assert.True(toolIndex >= 0 && toolIndex < userIndex);
    }

    [Fact]
    public async Task Orchestrator_ShouldCombineMemoryAndToolContext()
    {
        var chat = new ToolFakeChatProvider();
        using var provider = Provider(chat, SuccessfulTool(), includeMemory: true);
        await provider.GetRequiredService<IMemoryStore>().AppendAsync(
            new MemoryWriteRequest(
                new ConversationMemoryKey("conversation", "tenant", "user"),
                ConversationMemoryRole.User,
                "memory-message"),
            CancellationToken.None);
        var request = Request() with { UseMemory = true };

        await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(request, CancellationToken.None);

        Assert.Contains(chat.LastRequest!.Messages, message => message.Content == "memory-message");
        Assert.Contains(chat.LastRequest.Messages, message => message.Content.Contains("Tool: test-tool", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Orchestrator_ShouldCombineKnowledgeAndToolContext()
    {
        var chat = new ToolFakeChatProvider();
        using var provider = Provider(chat, SuccessfulTool(), includeKnowledge: true);
        var request = Request() with
        {
            Knowledge = new AIKnowledgeOptions(enabled: true, query: "query")
        };

        await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(request, CancellationToken.None);

        Assert.Contains(chat.LastRequest!.Messages, message => message.Content.Contains("knowledge-fragment", StringComparison.Ordinal));
        Assert.Contains(chat.LastRequest.Messages, message => message.Content.Contains("Tool: test-tool", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Orchestrator_ShouldCombinePromptEngineAndToolContext()
    {
        var chat = new ToolFakeChatProvider();
        using var provider = Provider(chat, SuccessfulTool());
        var request = Request() with
        {
            PromptTemplateId = new PromptTemplateId("generic-chat"),
            PromptTemplateVersion = PromptTemplateVersion.Parse("1.0"),
            PromptVariables = new Dictionary<string, string>
            {
                ["systemInstruction"] = "template-system",
                ["userMessage"] = "template-user"
            }
        };

        await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(request, CancellationToken.None);

        Assert.Contains(chat.LastRequest!.Messages, message => message.Content == "template-system");
        Assert.Contains(chat.LastRequest.Messages, message => message.Content == "template-user");
        Assert.Contains(chat.LastRequest.Messages, message => message.Content.Contains("Tool: test-tool", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Orchestrator_FailClosed_ShouldPreventProviderCall()
    {
        var chat = new ToolFakeChatProvider();
        using var provider = Provider(chat, ThrowingTool());

        await Assert.ThrowsAsync<AIOrchestrationException>(() =>
            provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(Request(), CancellationToken.None));
        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public async Task Orchestrator_ContinueWithoutTool_ShouldCallProvider()
    {
        var chat = new ToolFakeChatProvider();
        using var provider = Provider(chat, ThrowingTool());
        var response = await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(
            Request(AIToolFailureMode.ContinueWithoutTool),
            CancellationToken.None);

        Assert.Equal(1, chat.CallCount);
        Assert.True(response.ToolUsed);
        Assert.False(response.ToolSuccess);
        Assert.Equal("AI_TOOL_EXECUTION_FAILED", response.ToolErrorCode);
    }

    [Fact]
    public async Task Orchestrator_ShouldNeverFailOpenAuthorizationError()
    {
        var chat = new ToolFakeChatProvider();
        var tool = new StubAITool(AIToolTestData.Definition(permissions: ["tool.use"]));
        using var provider = Provider(chat, tool);

        await Assert.ThrowsAsync<AIOrchestrationException>(() =>
            provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(
                Request(AIToolFailureMode.ContinueWithoutTool),
                CancellationToken.None));
        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public async Task Orchestrator_ShouldPropagateToolCancellation()
    {
        var tool = new StubAITool(
            AIToolTestData.Definition(timeout: TimeSpan.FromSeconds(2)),
            async (context, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                var now = context.TimeProvider.GetUtcNow();
                return AIToolExecutionResult.Succeeded(
                    context.Request.ToolId,
                    AIToolTestData.Json(new { ok = true }),
                    now,
                    now);
            });
        var chat = new ToolFakeChatProvider();
        using var provider = Provider(chat, tool);
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(Request(), source.Token));
        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public void Orchestrator_ShouldRegisterToolSteps()
    {
        using var provider = Provider(new ToolFakeChatProvider(), SuccessfulTool());
        var steps = provider.GetServices<IAIOrchestrationStep>().Select(step => step.Name).ToArray();
        Assert.Contains(AIOrchestrationStepNames.ToolExecution, steps);
        Assert.Contains(AIOrchestrationStepNames.ToolResultComposition, steps);
    }

    [Fact]
    public async Task Orchestrator_ShouldExposeToolDuration()
    {
        using var provider = Provider(new ToolFakeChatProvider(), SuccessfulTool());
        var response = await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(Request(), CancellationToken.None);
        Assert.NotNull(response.ToolDuration);
        Assert.True(response.ToolDuration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task Orchestrator_ShouldNotExposeCompleteToolOutput()
    {
        const string privateOutput = "private-tool-output";
        var tool = new StubAITool(
            AIToolTestData.Definition(),
            (context, _) =>
            {
                var now = context.TimeProvider.GetUtcNow();
                return Task.FromResult(AIToolExecutionResult.Succeeded(
                    context.Request.ToolId,
                    AIToolTestData.Json(new { value = privateOutput }),
                    now,
                    now));
            });
        using var provider = Provider(new ToolFakeChatProvider(), tool);

        var response = await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(Request(), CancellationToken.None);

        Assert.DoesNotContain(privateOutput, System.Text.Json.JsonSerializer.Serialize(response), StringComparison.Ordinal);
        Assert.DoesNotContain(
            typeof(AIOrchestrationResponse).GetProperties(),
            property => property.Name.Contains("Output", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Orchestrator_ShouldKeepDisabledToolPathBehavior()
    {
        var chat = new ToolFakeChatProvider();
        var executor = new CountingAIToolExecutor();
        using var provider = Provider(chat, executor: executor);

        var response = await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(
            new AIOrchestrationRequest("system", "user", "Scenario"),
            CancellationToken.None);

        Assert.Equal("provider-response", response.Content);
        Assert.False(response.ToolUsed);
        Assert.Null(response.ToolSuccess);
        Assert.Equal([ChatRole.System, ChatRole.User], chat.LastRequest!.Messages.Select(message => message.Role));
    }

    [Fact]
    public async Task Orchestrator_ShouldKeepExistingMetricsCoherent()
    {
        using var provider = Provider(new ToolFakeChatProvider(), SuccessfulTool());
        var response = await provider.GetRequiredService<IAIOrchestrator>().ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(5, response.Usage!.TotalTokens);
        Assert.NotNull(response.ProviderDuration);
        Assert.True(response.TotalDuration >= response.ProviderDuration);
    }

    private static AIOrchestrationRequest Request(
        AIToolFailureMode failureMode = AIToolFailureMode.FailClosed) =>
        new AIOrchestrationRequest("system", "current-user", "Scenario")
        {
            SessionId = "session",
            ConversationId = "conversation",
            CorrelationId = "correlation",
            Identity = new AIIdentityContext("tenant", "user", "agent"),
            Tool = new AIToolInvocationOptions(
                enabled: true,
                toolId: new AIToolId("test-tool"),
                failureMode: failureMode)
        };

    private static StubAITool SuccessfulTool(Action<AIToolExecutionContext, CancellationToken>? action = null) =>
        new(
            AIToolTestData.Definition(),
            (context, cancellationToken) =>
            {
                action?.Invoke(context, cancellationToken);
                var now = context.TimeProvider.GetUtcNow();
                return Task.FromResult(AIToolExecutionResult.Succeeded(
                    context.Request.ToolId,
                    AIToolTestData.Json(new { value = 42 }),
                    now,
                    now));
            });

    private static StubAITool ThrowingTool() =>
        new(AIToolTestData.Definition(), (_, _) => throw new InvalidOperationException("internal tool failure"));

    private static ServiceProvider Provider(
        ToolFakeChatProvider chatProvider,
        IAITool? tool = null,
        IAIToolExecutor? executor = null,
        bool includeMemory = false,
        bool includeKnowledge = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChatProvider>(chatProvider);
        services.AddSingleton<IAIProviderMetadata>(new ToolFakeProviderMetadata());
        if (tool is not null)
        {
            services.AddSingleton(tool);
        }

        if (executor is not null)
        {
            services.AddSingleton(executor);
        }

        services.AddTradeMindAIOrchestration();
        services.AddTradeMindAIToolOrchestration(options => options.EnableDevelopmentTools = true);

        if (includeMemory)
        {
            services.AddTradeMindMemory(compactionOptions: new MemoryCompactionOptions(enabled: false));
        }

        if (includeKnowledge)
        {
            services.AddSingleton<IKnowledgeSearcher>(new ToolKnowledgeSearcher());
            services.AddTradeMindKnowledgeRag();
        }

        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class CountingAIToolExecutor : IAIToolExecutor
    {
        public int CallCount { get; private set; }

        public Task<AIToolExecutionResult> ExecuteAsync(
            AIToolExecutionRequest request,
            AIToolAuthorizationContext authorizationContext,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
            return Task.FromResult(AIToolExecutionResult.Succeeded(
                request.ToolId,
                AIToolTestData.Json(new { ok = true }),
                now,
                now));
        }
    }

    private sealed class ToolKnowledgeSearcher : IKnowledgeSearcher
    {
        public Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
            string query,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<KnowledgeSearchResult>>(
            [
                new KnowledgeSearchResult(
                    Guid.NewGuid(),
                    "Source",
                    Guid.NewGuid(),
                    0,
                    "knowledge-fragment",
                    0.9)
            ]);
    }
}
