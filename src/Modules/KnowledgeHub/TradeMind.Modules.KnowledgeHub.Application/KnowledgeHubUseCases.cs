using MediatR;
using TradeMind.Modules.KnowledgeHub.Domain;

namespace TradeMind.Modules.KnowledgeHub.Application;

public sealed record ImportKnowledgeSourceCommand(Stream Content, string FileName, string MediaType) : IRequest<KnowledgeSourceDto>;
public sealed record GetKnowledgeSourceQuery(Guid Id) : IRequest<KnowledgeSourceDto?>;
public sealed record SearchKnowledgeQuery(string Query, int Limit = 5) : IRequest<IReadOnlyList<KnowledgeSearchResult>>;

public sealed class ImportKnowledgeSourceHandler(
    IKnowledgeImporter importer,
    ITextExtractor extractor,
    IFragmenter fragmenter,
    IEmbeddingGenerator embeddings,
    IKnowledgeIndexer indexer,
    IKnowledgeSourceRepository repository,
    IKnowledgeHubUnitOfWork unitOfWork) : IRequestHandler<ImportKnowledgeSourceCommand, KnowledgeSourceDto>
{
    public async Task<KnowledgeSourceDto> Handle(ImportKnowledgeSourceCommand request, CancellationToken ct)
    {
        var imported = await importer.ImportAsync(request.Content, request.FileName, request.MediaType, ct);
        var source = KnowledgeSource.Create(imported.FileName, imported.MediaType, DateTimeOffset.UtcNow);
        source.SetExtractedText(await extractor.ExtractAsync(imported, ct));
        source.ReplaceFragments(fragmenter.Fragment(source.ExtractedText!));
        foreach (var fragment in source.Fragments)
            fragment.SetEmbedding(await embeddings.GenerateAsync(fragment.Content, ct));
        await repository.AddAsync(source, ct);
        await indexer.IndexAsync(source, ct);
        source.MarkIndexed();
        await unitOfWork.SaveChangesAsync(ct);
        return Map(source);
    }

    internal static KnowledgeSourceDto Map(KnowledgeSource source) =>
        new(source.Id, source.FileName, source.MediaType, source.Status.ToString(), source.Fragments.Count, source.CreatedAtUtc);
}

public sealed class GetKnowledgeSourceHandler(IKnowledgeSourceRepository repository) : IRequestHandler<GetKnowledgeSourceQuery, KnowledgeSourceDto?>
{
    public async Task<KnowledgeSourceDto?> Handle(GetKnowledgeSourceQuery request, CancellationToken ct)
    {
        var source = await repository.GetAsync(request.Id, ct);
        return source is null ? null : ImportKnowledgeSourceHandler.Map(source);
    }
}

public sealed class SearchKnowledgeHandler(IKnowledgeSearcher searcher) : IRequestHandler<SearchKnowledgeQuery, IReadOnlyList<KnowledgeSearchResult>>
{
    public Task<IReadOnlyList<KnowledgeSearchResult>> Handle(SearchKnowledgeQuery request, CancellationToken ct) =>
        searcher.SearchAsync(request.Query, Math.Clamp(request.Limit, 1, 5), ct);
}
