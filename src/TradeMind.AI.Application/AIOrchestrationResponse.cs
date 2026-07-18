using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Application;

public sealed record AIOrchestrationResponse(
    string Content,
    string ProviderName,
    string Model,
    ChatUsage? Usage,
    string CorrelationId,
    TimeSpan TotalDuration,
    IReadOnlyList<string> ExecutedSteps,
    DateTimeOffset GeneratedAtUtc,
    string? ResponseId = null);
