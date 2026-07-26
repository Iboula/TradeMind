using TradeMind.AI.Abstractions;
using TradeMind.KnowledgeHub.Application;

namespace TradeMind.KnowledgeHub.Infrastructure;

public sealed class AIEmbeddingGeneratorAdapter(IEmbeddingProvider embeddingProvider) : IEmbeddingGenerator
{
    public int Dimensions => 0;

    public async Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var response = await embeddingProvider.GenerateAsync(
            new EmbeddingRequest([text]),
            cancellationToken);
        var vector = response.Vectors.SingleOrDefault()
            ?? throw new InvalidOperationException("The embedding provider returned no vector.");

        return vector.ToArray();
    }
}
