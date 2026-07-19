using Microsoft.Extensions.Logging;
using System.Text.Json;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Tests;

internal static class AIToolTestData
{
    public static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value);

    public static AIToolDefinition Definition(
        string id = "test-tool",
        IReadOnlyList<AIToolParameterDefinition>? parameters = null,
        IReadOnlyCollection<string>? permissions = null,
        AIToolSideEffectLevel sideEffect = AIToolSideEffectLevel.None,
        AIToolAvailability availability = AIToolAvailability.Enabled,
        TimeSpan? timeout = null,
        IReadOnlyCollection<string>? tags = null,
        IReadOnlyCollection<string>? allowedScenarios = null,
        IReadOnlyCollection<string>? allowedTenants = null,
        IReadOnlyCollection<string>? allowedUsers = null,
        IReadOnlyCollection<string>? allowedAgents = null) =>
        new(
            new AIToolId(id),
            "Test tool",
            "A deterministic tool used by the automated test suite.",
            "1.0",
            parameters,
            permissions,
            sideEffect,
            isIdempotent: true,
            defaultTimeout: timeout,
            availability,
            tags,
            allowedScenarios: allowedScenarios,
            allowedTenantIds: allowedTenants,
            allowedUserIds: allowedUsers,
            allowedAgentIds: allowedAgents);

    public static AIToolExecutionRequest Request(
        AIToolId? toolId = null,
        IReadOnlyDictionary<string, JsonElement>? arguments = null,
        string sessionId = "session",
        string correlationId = "correlation",
        string? conversationId = "conversation",
        string? tenantId = "tenant",
        string? userId = "user",
        string? agentId = "agent",
        string scenario = "Scenario",
        TimeSpan? timeoutOverride = null,
        string? idempotencyKey = "idempotency-1") =>
        new(
            toolId ?? new AIToolId("test-tool"),
            arguments,
            sessionId,
            correlationId,
            conversationId,
            tenantId,
            userId,
            agentId,
            scenario,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            timeoutOverride,
            idempotencyKey);

    public static AIToolAuthorizationContext Authorization(
        IReadOnlyCollection<string>? permissions = null,
        AIToolSideEffectLevel maximumSideEffect = AIToolSideEffectLevel.ReadOnly,
        string? tenantId = "tenant",
        string? userId = "user",
        string? agentId = "agent",
        string scenario = "Scenario") =>
        new(tenantId, userId, agentId, permissions, scenario, maximumSideEffect, "correlation");
}

internal sealed class StubAITool : IAITool
{
    private readonly Func<AIToolExecutionContext, CancellationToken, Task<AIToolExecutionResult>> _execute;

    public StubAITool(
        AIToolDefinition definition,
        Func<AIToolExecutionContext, CancellationToken, Task<AIToolExecutionResult>>? execute = null)
    {
        Definition = definition;
        _execute = execute ?? SucceedAsync;
    }

    public AIToolDefinition Definition { get; }

    public int CallCount { get; private set; }

    public AIToolExecutionContext? LastContext { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<AIToolExecutionResult> ExecuteAsync(
        AIToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastContext = context;
        LastCancellationToken = cancellationToken;
        return _execute(context, cancellationToken);
    }

    private Task<AIToolExecutionResult> SucceedAsync(
        AIToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = context.TimeProvider.GetUtcNow();
        return Task.FromResult(AIToolExecutionResult.Succeeded(
            Definition.Id,
            AIToolTestData.Json(new { ok = true }),
            now,
            now));
    }
}

internal sealed class AdvancingTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private long _timestamp;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override long GetTimestamp() => _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public void Advance(TimeSpan duration)
    {
        _utcNow += duration;
        _timestamp += duration.Ticks;
    }
}

internal sealed class ToolFakeChatProvider : IChatProvider
{
    private readonly IList<string>? _sequence;

    public ToolFakeChatProvider(IList<string>? sequence = null)
    {
        _sequence = sequence;
    }

    public int CallCount { get; private set; }

    public ChatRequest? LastRequest { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        LastCancellationToken = cancellationToken;
        _sequence?.Add("provider");
        return Task.FromResult(new ChatResponse(
            "provider-response",
            "Fake",
            "fake-model",
            new ChatUsage(2, 3, 5),
            "response-id",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
    }
}

internal sealed class ToolFakeProviderMetadata : IAIProviderMetadata
{
    public string ProviderName => "Fake";

    public AIProviderCapabilities Capabilities { get; } = new(true, false, false, false, false);
}

internal sealed class ToolRecordingLoggerProvider : ILoggerProvider
{
    public IList<string> Messages { get; } = [];

    public ILogger CreateLogger(string categoryName) => new ToolRecordingLogger(Messages);

    public void Dispose()
    {
    }
}

internal sealed class ToolRecordingLogger(IList<string> messages) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => ToolNullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        messages.Add(formatter(state, exception));
}

internal sealed class ToolNullScope : IDisposable
{
    public static ToolNullScope Instance { get; } = new();

    public void Dispose()
    {
    }
}
