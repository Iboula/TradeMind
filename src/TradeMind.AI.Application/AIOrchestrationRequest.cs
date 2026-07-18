namespace TradeMind.AI.Application;

public sealed record AIOrchestrationRequest(
    string? SystemInstruction,
    string UserMessage,
    string Scenario,
    string? Model = null,
    float? Temperature = null,
    int? MaxOutputTokens = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? CorrelationId = null);
