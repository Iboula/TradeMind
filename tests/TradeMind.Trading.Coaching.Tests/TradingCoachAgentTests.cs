using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Agents;
using TradeMind.AI.Application;
using TradeMind.AI.Tools;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Coaching.Tests;

public sealed class TradingCoachAgentTests
{
    [Fact]
    public async Task Case105_TradingCoachVersionOneIsRegistered()
    {
        using var provider = CreateRegistryProvider();
        var agent = await Exact(provider, "1.0.0");
        Assert.IsType<TradingCoachAgentV1>(agent);
    }

    [Fact]
    public async Task Case106_TradingCoachSkeletonRemainsResolvable()
    {
        using var provider = CreateRegistryProvider();
        Assert.IsType<TradingCoachAgent>(await Exact(provider, "0.1.0"));
    }

    [Fact]
    public async Task Case107_LatestStableReturnsVersionOne()
    {
        using var provider = CreateRegistryProvider();
        var agent = await provider.GetRequiredService<IAIAgentRegistry>().GetAsync(
            new AIAgentId(TradingCoachConstants.AgentId),
            AIAgentVersionSelection.LatestStable,
            null,
            CancellationToken.None);
        Assert.Equal("1.0.0", agent.Definition.Version.ToString());
    }

    [Fact]
    public void Case108_StructuredOutputIsEnabled()
    {
        Assert.True(Definition.Capabilities.SupportsStructuredOutput);
    }

    [Fact]
    public void Case109_StreamingIsDisabled()
    {
        Assert.False(Definition.Capabilities.SupportsStreaming);
    }

    [Fact]
    public void Case110_ToolCallingIsDisabled()
    {
        Assert.False(Definition.Capabilities.SupportsToolCalling);
    }

    [Fact]
    public void Case111_AutonomousExecutionIsDisabled()
    {
        Assert.False(Definition.Capabilities.SupportsAutonomousExecution);
    }

    [Fact]
    public void Case112_ToolsAreDisabledByDefault()
    {
        Assert.False(Definition.ToolPolicy.Enabled);
        Assert.Empty(Definition.ToolPolicy.AllowedToolIds);
    }

    [Fact]
    public void Case113_MemoryIsOptional()
    {
        Assert.True(Definition.MemoryPolicy.Enabled);
        Assert.False(Definition.MemoryPolicy.Required);
        Assert.False(Definition.MemoryPolicy.SaveUserMessage);
    }

    [Fact]
    public void Case114_KnowledgeIsOptional()
    {
        Assert.True(Definition.KnowledgePolicy.Enabled);
        Assert.False(Definition.KnowledgePolicy.Required);
    }

    [Fact]
    public void Case115_RejectsTradingTool()
    {
        var mapper = new AIAgentRequestMapper(Options.Create(new AIAgentFrameworkOptions()));
        var request = Request(tool: new AIToolInvocationOptions(
            enabled: true,
            new AIToolId("market-order"),
            maximumAllowedSideEffectLevel: AIToolSideEffectLevel.IrreversibleWrite));
        Assert.Throws<AIAgentPolicyViolationException>(() => mapper.Map(Definition, request));
    }

    [Fact]
    public void Case116_RejectsSideEffects()
    {
        Assert.Equal(AIToolSideEffectLevel.None, Definition.MaximumAllowedSideEffectLevel);
        Assert.Equal(AIToolSideEffectLevel.None, Definition.ToolPolicy.MaximumAllowedSideEffectLevel);
    }

    [Fact]
    public void Case117_UsesTradingCoachAnalysisTemplate()
    {
        Assert.Equal("trading-coach-analysis", Definition.PromptPolicy.TemplateId!.Value);
        Assert.Equal("1.0", Definition.PromptPolicy.TemplateVersion!.ToString());
    }

    [Fact]
    public void Case118_AppliesRequiredDisclaimer()
    {
        Assert.Contains(TradingCoachConstants.Disclaimer, TradingCoachPromptTemplates.SafetyRules, StringComparison.Ordinal);
    }

    [Fact]
    public void Case119_ContainsNoCodedTradingSignal()
    {
        Assert.Equal("none", Definition.Metadata["trading-actions"]);
        Assert.Equal("none", Definition.Metadata["market-access"]);
        Assert.Equal("none", Definition.Metadata["broker-access"]);
    }

    [Fact]
    public async Task Case120_CallsProviderOnce()
    {
        var chat = new CountingChatProvider();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChatProvider>(chat);
        services.AddSingleton<IAIProviderMetadata, FakeProviderMetadata>();
        services.AddTradeMindAIOrchestration();
        services.AddTradeMindTradingCoaching();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        await provider.GetRequiredService<IAIAgentExecutor>().ExecuteAsync(Request(), CancellationToken.None);
        Assert.Equal(1, chat.Calls);
    }

    private static AIAgentDefinition Definition => new TradingCoachAgentV1().Definition;

    private static ServiceProvider CreateRegistryProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTradeMindTradingCoaching();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static Task<IAIAgent> Exact(ServiceProvider provider, string version) =>
        provider.GetRequiredService<IAIAgentRegistry>().GetAsync(
            new AIAgentId(TradingCoachConstants.AgentId),
            AIAgentVersionSelection.Exact,
            AIAgentVersion.Parse(version),
            CancellationToken.None);

    private static AIAgentExecutionRequest Request(AIToolInvocationOptions? tool = null) => new(
        new AIAgentId(TradingCoachConstants.AgentId),
        "Analyze structured data.",
        TradingCoachConstants.Scenario,
        TradingCoachTestData.Now,
        AIAgentVersionSelection.Exact,
        AIAgentVersion.Parse(TradingCoachConstants.AgentVersion),
        sessionId: "session",
        conversationId: "conversation",
        correlationId: "correlation",
        promptVariables: new Dictionary<string, string>
        {
            ["normalizedJournal"] = "{}",
            ["computedMetrics"] = "{}",
            ["ruleBasedFindings"] = "{}",
            ["coachProfile"] = "{}",
            ["requestedLanguage"] = "en",
            ["requiredOutputSchema"] = TradingCoachPromptTemplates.RequiredOutputSchema,
            ["safetyRules"] = TradingCoachPromptTemplates.SafetyRules
        },
        toolInvocation: tool,
        requestedCapabilities: new AIAgentCapabilities(
            supportsPromptTemplates: true,
            supportsConversation: true,
            supportsStructuredOutput: true));

    private sealed class CountingChatProvider : IChatProvider
    {
        public int Calls { get; private set; }

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new ChatResponse(
                TradingCoachTestData.ValidResponseJson(),
                "Fake",
                "fake-model",
                new ChatUsage(10, 10, 20),
                "response-1",
                TradingCoachTestData.Now));
        }
    }

    private sealed class FakeProviderMetadata : IAIProviderMetadata
    {
        public string ProviderName => "Fake";
        public AIProviderCapabilities Capabilities { get; } = new(true, false, false, false, false);
    }
}
