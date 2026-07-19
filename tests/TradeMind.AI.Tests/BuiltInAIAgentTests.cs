using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Agents;
using TradeMind.AI.Application;
using TradeMind.AI.Knowledge;
using TradeMind.AI.Memory;
using TradeMind.AI.Tools;
using TradeMind.KnowledgeHub.Application;

namespace TradeMind.AI.Tests;

public sealed class BuiltInAIAgentTests
{
    [Fact]
    public async Task GenericAssistant_IsRegistered()
    {
        using var provider = CreateProvider();
        var registry = provider.GetRequiredService<IAIAgentRegistry>();
        Assert.True(await registry.ExistsAsync(new AIAgentId("generic-assistant"), CancellationToken.None));
    }

    [Fact]
    public async Task GenericAssistant_VersionOneIsAvailable()
    {
        using var provider = CreateProvider();
        var agent = await provider.GetRequiredService<IAIAgentRegistry>().GetAsync(
            new AIAgentId("generic-assistant"),
            AIAgentVersionSelection.Exact,
            AIAgentVersion.Parse("1.0.0"),
            CancellationToken.None);
        Assert.Equal("1.0.0", agent.Definition.Version.ToString());
    }

    [Fact]
    public void GenericAssistant_UsesGenericTemplate()
    {
        Assert.Equal(BuiltInPromptTemplates.GenericChat.Id, new GenericAssistantAgent().Definition.PromptPolicy.TemplateId);
    }

    [Fact]
    public async Task GenericAssistant_WorksWithoutMemory()
    {
        using var provider = CreateProvider();
        var response = await provider.GetRequiredService<IAIAgentExecutor>().ExecuteAsync(Request(), CancellationToken.None);
        Assert.False(response.MemoryUsed);
    }

    [Fact]
    public async Task GenericAssistant_WorksWithMemory()
    {
        using var provider = CreateProvider();
        var response = await provider.GetRequiredService<IAIAgentExecutor>().ExecuteAsync(
            Request(memory: new AIAgentMemoryRequestOptions(true)),
            CancellationToken.None);
        Assert.True(response.MemoryUsed);
    }

    [Fact]
    public async Task GenericAssistant_WorksWithoutKnowledge()
    {
        using var provider = CreateProvider();
        var response = await provider.GetRequiredService<IAIAgentExecutor>().ExecuteAsync(Request(), CancellationToken.None);
        Assert.False(response.KnowledgeUsed);
    }

    [Fact]
    public async Task GenericAssistant_WorksWithFakeKnowledge()
    {
        using var provider = CreateProvider();
        var response = await provider.GetRequiredService<IAIAgentExecutor>().ExecuteAsync(
            Request(knowledge: new AIKnowledgeOptions(enabled: true)),
            CancellationToken.None);
        Assert.True(response.KnowledgeUsed);
        Assert.Single(response.Citations);
    }

    [Fact]
    public async Task GenericAssistant_WorksWithoutTool()
    {
        using var provider = CreateProvider();
        var response = await provider.GetRequiredService<IAIAgentExecutor>().ExecuteAsync(Request(), CancellationToken.None);
        Assert.False(response.ToolUsed);
    }

    [Fact]
    public async Task GenericAssistant_WorksWithAddNumbers()
    {
        using var provider = CreateProvider();
        var arguments = new Dictionary<string, JsonElement>
        {
            ["left"] = JsonSerializer.SerializeToElement(2m),
            ["right"] = JsonSerializer.SerializeToElement(3m)
        };
        var response = await provider.GetRequiredService<IAIAgentExecutor>().ExecuteAsync(
            Request(tool: new AIToolInvocationOptions(
                enabled: true,
                new AIToolId("add-numbers"),
                arguments,
                maximumAllowedSideEffectLevel: AIToolSideEffectLevel.None)),
            CancellationToken.None);
        Assert.True(response.ToolUsed);
        Assert.Equal("add-numbers", response.ToolId);
    }

    [Fact]
    public void GenericAssistant_RejectsUnauthorizedTool()
    {
        var mapper = new AIAgentRequestMapper(Options.Create(AIAgentTestData.Options(development: true)));
        Assert.Throws<AIAgentPolicyViolationException>(() => mapper.Map(
            new GenericAssistantAgent().Definition,
            Request(tool: new AIToolInvocationOptions(
                enabled: true,
                new AIToolId("other-tool"),
                maximumAllowedSideEffectLevel: AIToolSideEffectLevel.None))));
    }

    [Fact]
    public void GenericAssistant_RejectsReadOnlyWhenPolicyIsNone()
    {
        var mapper = new AIAgentRequestMapper(Options.Create(AIAgentTestData.Options(development: true)));
        Assert.Throws<AIAgentPolicyViolationException>(() => mapper.Map(
            new GenericAssistantAgent().Definition,
            Request(tool: new AIToolInvocationOptions(
                enabled: true,
                new AIToolId("add-numbers"),
                maximumAllowedSideEffectLevel: AIToolSideEffectLevel.ReadOnly))));
    }

    [Fact]
    public async Task GenericAssistant_CallsProviderOnce()
    {
        var chatProvider = new ToolFakeChatProvider();
        using var provider = CreateProvider(chatProvider);
        await provider.GetRequiredService<IAIAgentExecutor>().ExecuteAsync(Request(), CancellationToken.None);
        Assert.Equal(1, chatProvider.CallCount);
    }

    [Fact]
    public async Task GenericAssistant_MapsResponse()
    {
        using var provider = CreateProvider();
        var response = await provider.GetRequiredService<IAIAgentExecutor>().ExecuteAsync(Request(), CancellationToken.None);
        Assert.True(response.Success);
        Assert.Equal("provider-response", response.Content);
        Assert.Equal("Fake", response.Provider);
    }

    [Fact]
    public async Task TradingCoach_IsRegistered()
    {
        using var provider = CreateProvider();
        Assert.True(await provider.GetRequiredService<IAIAgentRegistry>().ExistsAsync(
            new AIAgentId("trading-coach"),
            CancellationToken.None));
    }

    [Fact]
    public async Task TradingCoach_VersionPointOneIsAvailable()
    {
        using var provider = CreateProvider();
        var agent = await provider.GetRequiredService<IAIAgentRegistry>().GetAsync(
            new AIAgentId("trading-coach"),
            AIAgentVersionSelection.Exact,
            AIAgentVersion.Parse("0.1.0"),
            CancellationToken.None);
        Assert.Equal("0.1.0", agent.Definition.Version.ToString());
    }

    [Fact]
    public void TradingCoach_AllowsNoTradingTool()
    {
        Assert.Empty(new TradingCoachAgent().Definition.ToolPolicy.AllowedToolIds);
    }

    [Fact]
    public void TradingCoach_HasNoMarketAccess()
    {
        Assert.Equal("none", new TradingCoachAgent().Definition.Metadata["market-access"]);
    }

    [Fact]
    public void TradingCoach_HasNoBuySellSignalCapability()
    {
        var definition = new TradingCoachAgent().Definition;
        Assert.False(definition.Capabilities.SupportsTools);
        Assert.False(definition.Capabilities.SupportsStructuredOutput);
    }

    [Fact]
    public void TradingCoach_HasNoTradingOrderCapability()
    {
        Assert.False(new TradingCoachAgent().Definition.ToolPolicy.Enabled);
    }

    [Fact]
    public void TradingCoach_HasNoSideEffect()
    {
        Assert.Equal(AIToolSideEffectLevel.None, new TradingCoachAgent().Definition.MaximumAllowedSideEffectLevel);
    }

    [Fact]
    public void TradingCoach_UsesControlledEducationalPrompt()
    {
        var systemMessage = BuiltInAIAgentPromptTemplates.TradingCoach.Messages.Single(message =>
            message.Role == PromptMessageRole.System);
        Assert.Contains("educational", systemMessage.ContentTemplate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not provide financial advice", systemMessage.ContentTemplate, StringComparison.Ordinal);
    }

    [Fact]
    public void TradingCoach_IsDevelopmentOnly()
    {
        Assert.Equal(AIAgentAvailability.DevelopmentOnly, new TradingCoachAgent().Definition.Availability);
    }

    private static ServiceProvider CreateProvider(ToolFakeChatProvider? chatProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChatProvider>(chatProvider ?? new ToolFakeChatProvider());
        services.AddSingleton<IAIProviderMetadata, ToolFakeProviderMetadata>();
        services.AddSingleton<IKnowledgeSearcher, FakeKnowledgeSearcher>();
        services.AddTradeMindAIOrchestration();
        services.AddTradeMindMemory();
        services.AddTradeMindKnowledgeRag();
        services.AddTradeMindAIToolOrchestration(options => options.EnableDevelopmentTools = true);
        services.AddTradeMindAIAgents(options => options.EnableDevelopmentAgents = true);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static AIAgentExecutionRequest Request(
        AIAgentMemoryRequestOptions? memory = null,
        AIKnowledgeOptions? knowledge = null,
        AIToolInvocationOptions? tool = null) =>
        new(
            new AIAgentId("generic-assistant"),
            "Explain the framework.",
            "GenericChat",
            DateTimeOffset.UtcNow,
            sessionId: "agent-session",
            conversationId: "agent-conversation",
            tenantId: "tenant",
            userId: "user",
            correlationId: "agent-correlation",
            memoryOptions: memory,
            knowledgeOptions: knowledge,
            toolInvocation: tool);

    private sealed class FakeKnowledgeSearcher : IKnowledgeSearcher
    {
        public Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
            string query,
            int limit,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<KnowledgeSearchResult> result =
            [
                new(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    "Reference",
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    0,
                    "Provider-agnostic knowledge context.",
                    0.95)
            ];
            return Task.FromResult(result);
        }
    }
}
