using Microsoft.Extensions.Options;
using TradeMind.AI.Application;
using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.Knowledge;

namespace TradeMind.AI.Context.Infrastructure;

public sealed class KnowledgeContextProvider(
    IKnowledgeContextRetriever retriever,
    IOptions<ContextEngineOptions> options) : IContextProvider
{
    public static readonly ContextProviderId ProviderId = new("knowledge");
    private readonly ContextEngineOptions _options = options?.Value
        ?? throw new ArgumentNullException(nameof(options));

    public ContextProviderDescriptor Descriptor { get; } = new(
        ProviderId,
        ContextProviderCategory.Knowledge,
        ContextRequirement.Preferred,
        20,
        TimeSpan.FromSeconds(4));

    public async Task<ContextProviderResult> ProvideAsync(
        ContextProviderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await retriever.RetrieveAsync(
            new KnowledgeContextRequest(
                request.Query.KnowledgeSearchQuery,
                request.Query.SessionId,
                request.Query.CorrelationId,
                "MarketContext",
                _options.MaximumKnowledgeChunks,
                null,
                _options.MaximumKnowledgeCharacters,
                null,
                KnowledgeOrderingStrategy.RelevanceDescending,
                tenantId: request.Query.TenantId,
                userId: request.Query.UserId,
                includeSourceMetadata: false,
                includeCitations: true,
                contextLabel: "Market context knowledge"),
            cancellationToken).ConfigureAwait(false);
        if (result.Fragments.Count == 0)
        {
            return ContextProviderResult.Unavailable(
                ProviderId,
                "Knowledge retrieval completed without matching fragments.");
        }

        var chunks = result.Fragments.Select(fragment => new KnowledgeChunk(
            fragment.FragmentId.ToString("D"),
            fragment.SourceId.ToString("D"),
            fragment.Content,
            fragment.Score,
            fragment.Sequence,
            fragment.Title,
            fragment.SourceReference)).ToArray();
        var references = result.Fragments
            .Select(fragment => fragment.SourceId)
            .Distinct()
            .OrderBy(id => id)
            .Select(id => new ContextSourceReference("knowledge-source", id.ToString("D")))
            .ToArray();

        return ContextProviderResult.Succeeded(
            new KnowledgeContextData(new KnowledgeContext(
                result.Query,
                chunks,
                result.RetrievedAtUtc,
                result.Truncated,
                result.AvailableResultCount)),
            result.RetrievedAtUtc,
            chunks.Length,
            "1.0",
            references);
    }
}
