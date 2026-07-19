using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Agents;
using TradeMind.AI.Application;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Tests;

internal static class AIAgentTestData
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-19T12:00:00Z");

    public static AIAgentDefinition Definition(
        string id = "test-agent",
        string version = "1.0.0",
        AIAgentAvailability availability = AIAgentAvailability.Enabled,
        AIAgentCapabilities? capabilities = null,
        AIAgentPolicy? policy = null,
        IReadOnlyCollection<string>? permissions = null,
        IReadOnlyCollection<string>? scenarios = null,
        IReadOnlyCollection<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        IReadOnlyCollection<string>? tenants = null,
        IReadOnlyCollection<string>? users = null) =>
        new(
            new AIAgentId(id),
            "Test Agent",
            "A deterministic agent used by the automated test suite.",
            AIAgentVersion.Parse(version),
            availability,
            capabilities ?? new AIAgentCapabilities(supportsPromptTemplates: true, supportsConversation: true),
            policy ?? new AIAgentPolicy(
                new AIAgentPromptPolicy(staticSystemInstruction: "System instruction."),
                maximumExecutionDuration: TimeSpan.FromSeconds(10)),
            permissions,
            scenarios ?? ["Scenario"],
            tags,
            metadata,
            tenants,
            users);

    public static AIAgentExecutionRequest Request(
        string id = "test-agent",
        string scenario = "Scenario",
        AIAgentVersionSelection selection = AIAgentVersionSelection.LatestStable,
        AIAgentVersion? exactVersion = null,
        IReadOnlyCollection<string>? permissions = null,
        AIAgentCapabilities? requestedCapabilities = null,
        TimeSpan? timeout = null) =>
        new(
            new AIAgentId(id),
            "User message.",
            scenario,
            Now,
            selection,
            exactVersion,
            sessionId: "session",
            conversationId: "conversation",
            tenantId: "tenant",
            userId: "user",
            correlationId: "correlation",
            permissions: permissions,
            timeoutOverride: timeout,
            requestedCapabilities: requestedCapabilities);

    public static AIAgentFrameworkOptions Options(bool development = false) => new()
    {
        EnableDevelopmentAgents = development,
        DefaultExecutionTimeout = TimeSpan.FromSeconds(10),
        MaximumExecutionTimeout = TimeSpan.FromMinutes(1),
        MaximumAllowedSideEffectLevel = AIToolSideEffectLevel.ReadOnly
    };

    public static InMemoryAIAgentRegistry Registry(
        IEnumerable<IAIAgent> agents,
        bool development = false) =>
        new(agents, Microsoft.Extensions.Options.Options.Create(Options(development)));

    public static AIOrchestrationResponse OrchestrationResponse(
        bool memoryUsed = false,
        bool knowledgeUsed = false,
        bool toolUsed = false,
        string? toolId = null,
        string content = "Agent response.",
        DateTimeOffset? completedAtUtc = null) =>
        new(
            "session",
            "conversation",
            "correlation",
            "Scenario",
            "Fake",
            "fake-model",
            content,
            new ChatUsage(5, 3, 8),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(10),
            ["ProviderExecution"],
            completedAtUtc ?? Now.AddMilliseconds(20),
            AIExecutionState.Completed,
            knowledgeUsed: knowledgeUsed,
            knowledgeCitationIds: knowledgeUsed ? ["citation-1"] : [],
            toolUsed: toolUsed,
            toolId: toolId,
            toolSuccess: toolUsed ? true : null,
            memoryUsed: memoryUsed);

    public static AIAgentExecutor Executor(
        IAIAgent agent,
        CountingOrchestrator? orchestrator = null,
        IAIAgentRegistry? registry = null,
        IAIAgentAuthorizer? authorizer = null,
        IAIAgentRequestMapper? mapper = null,
        IAIAgentResponseMapper? responseMapper = null,
        TimeProvider? timeProvider = null,
        AIAgentFrameworkOptions? options = null) =>
        new(
            registry ?? Registry([agent], development: true),
            authorizer ?? new PolicyBasedAIAgentAuthorizer(Microsoft.Extensions.Options.Options.Create(options ?? Options(development: true))),
            mapper ?? new AIAgentRequestMapper(Microsoft.Extensions.Options.Options.Create(options ?? Options(development: true))),
            responseMapper ?? new AIAgentResponseMapper(),
            orchestrator ?? new CountingOrchestrator(),
            timeProvider ?? TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(options ?? Options(development: true)),
            NullLogger<AIAgentExecutor>.Instance);
}

internal sealed class StubAIAgent : IAIAgent
{
    private readonly Func<AIAgentExecutionContext, CancellationToken, ValueTask>? _starting;
    private readonly Func<AIAgentExecutionContext, AIAgentExecutionResponse, CancellationToken, ValueTask>? _completed;
    private readonly Func<AIAgentExecutionContext, Exception, CancellationToken, ValueTask>? _failed;

    public StubAIAgent(
        AIAgentDefinition definition,
        Func<AIAgentExecutionContext, CancellationToken, ValueTask>? starting = null,
        Func<AIAgentExecutionContext, AIAgentExecutionResponse, CancellationToken, ValueTask>? completed = null,
        Func<AIAgentExecutionContext, Exception, CancellationToken, ValueTask>? failed = null)
    {
        Definition = definition;
        _starting = starting;
        _completed = completed;
        _failed = failed;
    }

    public AIAgentDefinition Definition { get; }

    public int StartingCount { get; private set; }

    public int CompletedCount { get; private set; }

    public int FailedCount { get; private set; }

    public ValueTask OnExecutionStartingAsync(AIAgentExecutionContext context, CancellationToken cancellationToken)
    {
        StartingCount++;
        return _starting?.Invoke(context, cancellationToken) ?? ValueTask.CompletedTask;
    }

    public ValueTask OnExecutionCompletedAsync(
        AIAgentExecutionContext context,
        AIAgentExecutionResponse response,
        CancellationToken cancellationToken)
    {
        CompletedCount++;
        return _completed?.Invoke(context, response, cancellationToken) ?? ValueTask.CompletedTask;
    }

    public ValueTask OnExecutionFailedAsync(
        AIAgentExecutionContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        FailedCount++;
        return _failed?.Invoke(context, exception, cancellationToken) ?? ValueTask.CompletedTask;
    }
}

internal sealed class CountingOrchestrator : IAIOrchestrator
{
    private readonly Func<AIOrchestrationRequest, CancellationToken, Task<AIOrchestrationResponse>> _execute;

    public CountingOrchestrator(
        Func<AIOrchestrationRequest, CancellationToken, Task<AIOrchestrationResponse>>? execute = null)
    {
        _execute = execute ?? ((_, _) => Task.FromResult(AIAgentTestData.OrchestrationResponse(
            completedAtUtc: DateTimeOffset.UtcNow)));
    }

    public int CallCount { get; private set; }

    public AIOrchestrationRequest? LastRequest { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<AIOrchestrationResponse> ExecuteAsync(
        AIOrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        LastCancellationToken = cancellationToken;
        return _execute(request, cancellationToken);
    }
}

internal sealed class CountingAgentRegistry(IAIAgent agent) : IAIAgentRegistry
{
    public int GetCount { get; private set; }

    public Task<IAIAgent> GetAsync(AIAgentId agentId, CancellationToken cancellationToken) =>
        GetAsync(agentId, AIAgentVersionSelection.LatestStable, null, cancellationToken);

    public Task<IAIAgent> GetAsync(
        AIAgentId agentId,
        AIAgentVersionSelection versionSelection,
        AIAgentVersion? exactVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GetCount++;
        return Task.FromResult(agent);
    }

    public Task<bool> ExistsAsync(AIAgentId agentId, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<IReadOnlyList<AIAgentDefinition>> GetAvailableAsync(
        AIAgentDiscoveryContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AIAgentDefinition>>([agent.Definition]);
}

internal sealed class CountingAgentAuthorizer(Exception? exception = null) : IAIAgentAuthorizer
{
    public int Count { get; private set; }

    public void Authorize(
        AIAgentDefinition definition,
        AIAgentExecutionRequest request,
        AIAgentAuthorizationContext context)
    {
        Count++;
        if (exception is not null)
        {
            throw exception;
        }
    }
}

internal sealed class CountingAgentRequestMapper : IAIAgentRequestMapper
{
    public int Count { get; private set; }

    public AIOrchestrationRequest Map(AIAgentDefinition definition, AIAgentExecutionRequest request)
    {
        Count++;
        return new AIOrchestrationRequest("System.", request.UserMessage, request.Scenario, correlationId: request.CorrelationId)
        {
            SessionId = request.SessionId,
            ConversationId = request.ConversationId,
            Identity = new AIIdentityContext(request.TenantId, request.UserId, definition.Id.Value)
        };
    }
}

internal sealed class RecordingTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now = AIAgentTestData.Now;

    public int UtcNowCalls { get; private set; }

    public int TimestampCalls { get; private set; }

    public override DateTimeOffset GetUtcNow()
    {
        UtcNowCalls++;
        return _now.AddMilliseconds(UtcNowCalls);
    }

    public override long GetTimestamp()
    {
        TimestampCalls++;
        return TimestampCalls * TimeSpan.TicksPerMillisecond;
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
}
