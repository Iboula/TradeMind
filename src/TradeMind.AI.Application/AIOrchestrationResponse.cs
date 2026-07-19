using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Application;

public sealed record AIOrchestrationResponse
{
    public AIOrchestrationResponse(
        string sessionId,
        string? conversationId,
        string correlationId,
        string scenario,
        string provider,
        string model,
        string content,
        ChatUsage? usage,
        TimeSpan totalDuration,
        TimeSpan? providerDuration,
        IReadOnlyList<string> executedSteps,
        DateTimeOffset completedAtUtc,
        AIExecutionState state,
        string? responseId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        if (completedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Response completion date must be UTC.", nameof(completedAtUtc));
        }

        SessionId = sessionId;
        ConversationId = string.IsNullOrWhiteSpace(conversationId) ? null : conversationId;
        CorrelationId = correlationId;
        Scenario = scenario;
        Provider = provider;
        Model = model;
        Content = content;
        Usage = usage;
        TotalDuration = totalDuration;
        ProviderDuration = providerDuration;
        ExecutedSteps = executedSteps.ToArray();
        CompletedAtUtc = completedAtUtc;
        State = state;
        ResponseId = responseId;
    }

    public string SessionId { get; init; }

    public string? ConversationId { get; init; }

    public string CorrelationId { get; init; }

    public string Scenario { get; init; }

    public string Provider { get; init; }

    public string ProviderName => Provider;

    public string Model { get; init; }

    public string Content { get; init; }

    public ChatUsage? Usage { get; init; }

    public TimeSpan TotalDuration { get; init; }

    public TimeSpan? ProviderDuration { get; init; }

    public IReadOnlyList<string> ExecutedSteps { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public DateTimeOffset GeneratedAtUtc => CompletedAtUtc;

    public AIExecutionState State { get; init; }

    public string? ResponseId { get; init; }
}
