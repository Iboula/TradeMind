using Microsoft.Extensions.Options;
using TradeMind.AI.Agents;
using TradeMind.AI.Application;
using TradeMind.AI.Memory;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Tests;

public sealed class AIAgentPolicyTests
{
    private readonly AIAgentRequestMapper _mapper = new(Options.Create(AIAgentTestData.Options(development: true)));

    [Fact]
    public void Request_CannotEnableForbiddenMemory()
    {
        var request = Request(memory: new AIAgentMemoryRequestOptions(true), scenario: "TradingCoach", id: "trading-coach");
        Assert.Throws<AIAgentPolicyViolationException>(() => _mapper.Map(new TradingCoachAgent().Definition, request));
    }

    [Fact]
    public void Request_CannotDisableRequiredMemory()
    {
        var definition = MemoryDefinition(required: true);
        var request = Request(memory: new AIAgentMemoryRequestOptions(false));
        Assert.Throws<AIAgentPolicyViolationException>(() => _mapper.Map(definition, request));
    }

    [Fact]
    public void Request_CannotExceedMemoryWindow()
    {
        var request = Request(memory: new AIAgentMemoryRequestOptions(
            true,
            new MemoryWindowOptions(maxEntries: 21, maxCharacters: 8_000)));
        Assert.Throws<AIAgentPolicyViolationException>(() => _mapper.Map(new GenericAssistantAgent().Definition, request));
    }

    [Fact]
    public void Request_CannotEnableForbiddenKnowledge()
    {
        var request = Request(
            id: "trading-coach",
            scenario: "TradingCoach",
            knowledge: new AIKnowledgeOptions(enabled: true));
        Assert.Throws<AIAgentPolicyViolationException>(() => _mapper.Map(new TradingCoachAgent().Definition, request));
    }

    [Fact]
    public void Request_CannotExceedKnowledgeMaxResults()
    {
        var request = Request(knowledge: new AIKnowledgeOptions(enabled: true, maxResults: 6));
        Assert.Throws<AIAgentPolicyViolationException>(() => _mapper.Map(new GenericAssistantAgent().Definition, request));
    }

    [Fact]
    public void Request_CannotInjectForbiddenKnowledgeFilter()
    {
        var request = Request(knowledge: new AIKnowledgeOptions(
            enabled: true,
            filters: new Dictionary<string, string> { ["tenant-secret"] = "value" }));
        Assert.Throws<AIAgentPolicyViolationException>(() => _mapper.Map(new GenericAssistantAgent().Definition, request));
    }

    [Fact]
    public void Request_CannotInvokeUnauthorizedTool()
    {
        var request = Request(tool: Tool("unknown-tool"));
        Assert.Throws<AIAgentPolicyViolationException>(() => _mapper.Map(new GenericAssistantAgent().Definition, request));
    }

    [Fact]
    public void Request_CannotExceedMaximumSideEffect()
    {
        var request = Request(tool: Tool("add-numbers", AIToolSideEffectLevel.ReadOnly));
        Assert.Throws<AIAgentPolicyViolationException>(() => _mapper.Map(new GenericAssistantAgent().Definition, request));
    }

    [Fact]
    public void Request_CannotExceedToolTimeout()
    {
        var request = Request(tool: new AIToolInvocationOptions(
            enabled: true,
            new AIToolId("add-numbers"),
            maximumAllowedSideEffectLevel: AIToolSideEffectLevel.None,
            timeoutOverride: TimeSpan.FromSeconds(6)));
        Assert.Throws<AIAgentPolicyViolationException>(() => _mapper.Map(new GenericAssistantAgent().Definition, request));
    }

    [Fact]
    public void Request_CannotOverrideForbiddenTemplate()
    {
        var request = Request(promptTemplateId: new PromptTemplateId("other-template"));
        Assert.Throws<AIAgentPolicyViolationException>(() => _mapper.Map(new GenericAssistantAgent().Definition, request));
    }

    [Fact]
    public void Request_CanOverrideAllowedVariable()
    {
        var request = Request(promptVariables: new Dictionary<string, string>
        {
            ["systemInstruction"] = "A controlled override."
        });
        var result = _mapper.Map(new GenericAssistantAgent().Definition, request);
        Assert.Equal("A controlled override.", result.PromptVariables["systemInstruction"]);
    }

    [Fact]
    public void Request_CannotRemoveRequiredVariable()
    {
        var definition = AIAgentTestData.Definition(policy: new AIAgentPolicy(
            new AIAgentPromptPolicy(
                staticSystemInstruction: "System.",
                requiredVariables: ["requiredValue"])));
        Assert.Throws<AIAgentPolicyViolationException>(() => _mapper.Map(definition, Request()));
    }

    [Fact]
    public void ReducedCapabilityFailureMode_DoesNotBypassAuthorization()
    {
        var definition = AIAgentTestData.Definition(
            permissions: ["agent.execute"],
            policy: new AIAgentPolicy(
                new AIAgentPromptPolicy(staticSystemInstruction: "System."),
                failureMode: AIAgentFailureMode.ContinueWithReducedCapabilities));
        var authorizer = new PolicyBasedAIAgentAuthorizer(Options.Create(AIAgentTestData.Options()));
        var request = Request();
        Assert.Throws<AIAgentAuthorizationException>(() => authorizer.Authorize(
            definition,
            request,
            new AIAgentAuthorizationContext(
                "tenant",
                "user",
                [],
                "Scenario",
                AIToolSideEffectLevel.ReadOnly)));
    }

    [Fact]
    public void PolicyCollections_AreImmutable()
    {
        var policy = new GenericAssistantAgent().Definition.ToolPolicy;
        Assert.Throws<NotSupportedException>(() => ((ICollection<AIToolId>)policy.AllowedToolIds).Add(new AIToolId("other")));
    }

    private static AIAgentDefinition MemoryDefinition(bool required) =>
        AIAgentTestData.Definition(
            capabilities: new AIAgentCapabilities(
                supportsPromptTemplates: true,
                supportsMemory: true,
                supportsConversation: true),
            policy: new AIAgentPolicy(
                new AIAgentPromptPolicy(staticSystemInstruction: "System."),
                new AIAgentMemoryPolicy(
                    enabled: true,
                    required: required,
                    defaultWindowOptions: new MemoryWindowOptions(maxEntries: 10),
                    allowRequestOverride: true)));

    private static AIToolInvocationOptions Tool(
        string id,
        AIToolSideEffectLevel maximumSideEffect = AIToolSideEffectLevel.None) =>
        new(
            enabled: true,
            new AIToolId(id),
            maximumAllowedSideEffectLevel: maximumSideEffect);

    private static AIAgentExecutionRequest Request(
        string id = "generic-assistant",
        string scenario = "GenericChat",
        IReadOnlyDictionary<string, string>? promptVariables = null,
        AIAgentMemoryRequestOptions? memory = null,
        AIKnowledgeOptions? knowledge = null,
        AIToolInvocationOptions? tool = null,
        PromptTemplateId? promptTemplateId = null) =>
        new(
            new AIAgentId(id),
            "User message.",
            scenario,
            AIAgentTestData.Now,
            sessionId: "session",
            conversationId: "conversation",
            tenantId: "tenant",
            userId: "user",
            correlationId: "correlation",
            promptVariables: promptVariables,
            memoryOptions: memory,
            knowledgeOptions: knowledge,
            toolInvocation: tool,
            promptTemplateId: promptTemplateId);
}
