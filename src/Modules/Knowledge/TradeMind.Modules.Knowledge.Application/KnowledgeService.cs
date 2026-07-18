using TradeMind.Modules.Knowledge.Domain;

namespace TradeMind.Modules.Knowledge.Application;

public sealed class KnowledgeHubService(
    IKnowledgeImporter importer,
    IKnowledgeSourceRepository repository,
    IKnowledgeSearcher searcher)
{
    public async Task<KnowledgeSourceResponse> ImportAsync(
        ImportKnowledgeSourceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var source = await importer.ImportAsync(command, cancellationToken);
        return Map(source);
    }

    public async Task<KnowledgeSourceResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = await repository.GetAsync(id, cancellationToken);
        return source is null ? null : Map(source);
    }

    public Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        return searcher.SearchAsync(query.Trim(), Math.Clamp(limit, 1, 20), cancellationToken);
    }

    private static KnowledgeSourceResponse Map(KnowledgeSource source) =>
        new(source.Id, source.Name, source.MediaType, source.Status.ToString(), source.Fragments.Count, source.CreatedOnUtc);
}
