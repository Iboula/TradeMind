using Microsoft.Extensions.Options;
using TradeMind.AI.Agents;
using TradeMind.AI.Application;
using TradeMind.AI.Memory;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Tests;

public sealed class AIAgentRequestMapperTests
{
    private readonly AIAgentRequestMapper _mapper = new(Options.Create(AIAgentTestData.Options(development: true)));
    private readonly AIAgentDefinition _definition = new GenericAssistantAgent().Definition;

    [Fact]
    public void Mapper_MapsUserMessage()
    {
        Assert.Equal("Mapped message.", _mapper.Map(_definition, Request(userMessage: "Mapped message.")).UserMessage);
    }

    [Fact]
    public void Mapper_MapsSessionId()
    {
        Assert.Equal("mapped-session", _mapper.Map(_definition, Request(sessionId: "mapped-session")).SessionId);
    }

    [Fact]
    public void Mapper_MapsConversationId()
    {
        Assert.Equal("mapped-conversation", _mapper.Map(_definition, Request(conversationId: "mapped-conversation")).ConversationId);
    }

    [Fact]
    public void Mapper_MapsTenantId()
    {
        Assert.Equal("mapped-tenant", _mapper.Map(_definition, Request(tenantId: "mapped-tenant")).TenantId);
    }

    [Fact]
    public void Mapper_MapsUserId()
    {
        Assert.Equal("mapped-user", _mapper.Map(_definition, Request(userId: "mapped-user")).UserId);
    }

    [Fact]
    public void Mapper_MapsCorrelationId()
    {
        Assert.Equal("mapped-correlation", _mapper.Map(_definition, Request(correlationId: "mapped-correlation")).CorrelationId);
    }

    [Fact]
    public void Mapper_MapsScenario()
    {
        Assert.Equal("GenericChat", _mapper.Map(_definition, Request()).Scenario);
    }

    [Fact]
    public void Mapper_AppliesAgentTemplate()
    {
        var mapped = _mapper.Map(_definition, Request());
        Assert.Equal(BuiltInPromptTemplates.GenericChat.Id, mapped.PromptTemplateId);
        Assert.Equal(BuiltInPromptTemplates.GenericChat.Version, mapped.PromptTemplateVersion);
    }

    [Fact]
    public void Mapper_AppliesDefaultVariables()
    {
        var definition = AIAgentTestData.Definition(policy: new AIAgentPolicy(
            new AIAgentPromptPolicy(
                staticSystemInstruction: "System.",
                defaultVariables: new Dictionary<string, string> { ["tone"] = "concise" })));
        Assert.Equal("concise", _mapper.Map(definition, Request(id: "test-agent", scenario: "Scenario")).PromptVariables["tone"]);
    }

    [Fact]
    public void Mapper_AppliesEffectiveMemory()
    {
        var mapped = _mapper.Map(_definition, Request(memory: new AIAgentMemoryRequestOptions(true)));
        Assert.True(mapped.Memory.Enabled);
        Assert.True(mapped.UseMemory);
    }

    [Fact]
    public void Mapper_AppliesEffectiveKnowledge()
    {
        var mapped = _mapper.Map(_definition, Request(knowledge: new AIKnowledgeOptions(enabled: true)));
        Assert.True(mapped.Knowledge.Enabled);
        Assert.Equal(5, mapped.Knowledge.MaxResults);
    }

    [Fact]
    public void Mapper_AppliesEffectiveTool()
    {
        var mapped = _mapper.Map(_definition, Request(tool: Tool()));
        Assert.True(mapped.Tool.Enabled);
        Assert.Equal("add-numbers", mapped.Tool.ToolId?.Value);
    }

    [Fact]
    public void Mapper_AppliesFailureModes()
    {
        var mapped = _mapper.Map(_definition, Request(
            memory: new AIAgentMemoryRequestOptions(true),
            knowledge: new AIKnowledgeOptions(enabled: true),
            tool: new AIToolInvocationOptions(
                enabled: true,
                new AIToolId("add-numbers"),
                maximumAllowedSideEffectLevel: AIToolSideEffectLevel.None,
                failureMode: AIToolFailureMode.ContinueWithoutTool)));
        Assert.Equal(AIMemoryFailureMode.ContinueWithoutMemory, mapped.Memory.FailureMode);
        Assert.Equal(KnowledgeFailureMode.ContinueWithoutKnowledge, mapped.Knowledge.FailureMode);
        Assert.Equal(AIToolFailureMode.ContinueWithoutTool, mapped.Tool.FailureMode);
    }

    [Fact]
    public void Mapper_PreservesAgentCeilings()
    {
        var mapped = _mapper.Map(_definition, Request(memory: new AIAgentMemoryRequestOptions(
            true,
            new MemoryWindowOptions(maxEntries: 10, maxCharacters: 2_000))));
        Assert.Equal(10, mapped.Memory.Window.MaxEntries);
        Assert.True(mapped.Memory.Window.MaxEntries <= _definition.MemoryPolicy.DefaultWindowOptions.MaxEntries);
    }

    [Fact]
    public void Mapper_DoesNotMutateDefinition()
    {
        var before = _definition.PromptPolicy.DefaultVariables.ToArray();
        _ = _mapper.Map(_definition, Request(promptVariables: new Dictionary<string, string> { ["tone"] = "brief" }));
        Assert.Equal(before, _definition.PromptPolicy.DefaultVariables.ToArray());
    }

    [Fact]
    public void Mapper_IsDeterministic()
    {
        var request = Request(memory: new AIAgentMemoryRequestOptions(true));
        var first = _mapper.Map(_definition, request);
        var second = _mapper.Map(_definition, request);
        Assert.Equal(first.UserMessage, second.UserMessage);
        Assert.Equal(first.PromptTemplateId, second.PromptTemplateId);
        Assert.Equal(first.PromptVariables, second.PromptVariables);
        Assert.Equal(first.Memory, second.Memory);
    }

    [Fact]
    public void Mapper_DoesNotDuplicateUserMessage()
    {
        var mapped = _mapper.Map(_definition, Request(userMessage: "Only once."));
        Assert.Equal("Only once.", mapped.UserMessage);
        Assert.DoesNotContain("userMessage", mapped.PromptVariables.Keys);
    }

    private static AIToolInvocationOptions Tool() =>
        new(
            enabled: true,
            new AIToolId("add-numbers"),
            maximumAllowedSideEffectLevel: AIToolSideEffectLevel.None);

    private static AIAgentExecutionRequest Request(
        string id = "generic-assistant",
        string userMessage = "User message.",
        string scenario = "GenericChat",
        string? sessionId = "session",
        string? conversationId = "conversation",
        string? tenantId = "tenant",
        string? userId = "user",
        string? correlationId = "correlation",
        IReadOnlyDictionary<string, string>? promptVariables = null,
        AIAgentMemoryRequestOptions? memory = null,
        AIKnowledgeOptions? knowledge = null,
        AIToolInvocationOptions? tool = null) =>
        new(
            new AIAgentId(id),
            userMessage,
            scenario,
            AIAgentTestData.Now,
            sessionId: sessionId,
            conversationId: conversationId,
            tenantId: tenantId,
            userId: userId,
            correlationId: correlationId,
            promptVariables: promptVariables,
            memoryOptions: memory,
            knowledgeOptions: knowledge,
            toolInvocation: tool);
}
