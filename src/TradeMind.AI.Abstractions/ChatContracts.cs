namespace TradeMind.AI.Abstractions;

public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool
}

public sealed record ChatMessage(
    ChatRole Role,
    string Content);

public sealed record ChatRequest(
    IReadOnlyList<ChatMessage> Messages,
    string? Model = null,
    float? Temperature = null,
    int? MaxOutputTokens = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record ChatResponse(
    string Content,
    string ProviderName,
    string Model,
    ChatUsage? Usage,
    string? ResponseId,
    DateTimeOffset GeneratedAtUtc);

public sealed record ChatUsage(
    int InputTokens,
    int OutputTokens,
    int TotalTokens);

public interface IChatProvider
{
    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken);
}
