namespace TradeMind.AI.Abstractions;

public sealed record EmbeddingRequest(
    IReadOnlyList<string> Texts,
    string? Model = null);

public sealed record EmbeddingResponse(
    IReadOnlyList<IReadOnlyList<float>> Vectors,
    string ProviderName,
    string Model,
    ChatUsage? Usage);

public interface IEmbeddingProvider
{
    Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken);
}
