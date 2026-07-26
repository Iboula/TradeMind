using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Infrastructure;

public sealed class OpenAIEmbeddingProvider(
    EmbeddingClient embeddingClient,
    IOptions<AIOptions> options,
    ILogger<OpenAIEmbeddingProvider> logger)
    : IEmbeddingProvider
{
    public async Task<EmbeddingResponse> GenerateAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Texts.Count == 0)
        {
            throw new ArgumentException("At least one text is required.", nameof(request));
        }

        var model = ResolveModel(request.Model, options.Value.OpenAI.EmbeddingModel);
        logger.LogInformation(
            "AI embedding request started using provider {Provider} and model {Model} for {TextCount} texts",
            OpenAIProviderNames.OpenAI,
            model,
            request.Texts.Count);

        try
        {
            var result = await embeddingClient.GenerateEmbeddingsAsync(
                request.Texts,
                options: null,
                cancellationToken);
            var embeddings = result.Value;
            var vectors = embeddings.Select(embedding => embedding.ToFloats().ToArray()).ToArray();
            var usage = embeddings.Usage is null
                ? null
                : new ChatUsage(
                    embeddings.Usage.InputTokenCount,
                    0,
                    embeddings.Usage.InputTokenCount);

            logger.LogInformation(
                "AI embedding request completed using provider {Provider} and model {Model} for {VectorCount} vectors",
                OpenAIProviderNames.OpenAI,
                model,
                vectors.Length);

            return new EmbeddingResponse(
                vectors,
                OpenAIProviderNames.OpenAI,
                model,
                usage);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "AI embedding request failed using provider {Provider} and model {Model}",
                OpenAIProviderNames.OpenAI,
                model);
            throw new AIProviderException("The OpenAI embedding request failed.", exception);
        }
    }

    private static string ResolveModel(string? requestedModel, string configuredModel) =>
        string.IsNullOrWhiteSpace(requestedModel) ? configuredModel : requestedModel.Trim();
}
