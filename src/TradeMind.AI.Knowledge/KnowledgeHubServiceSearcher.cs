using TradeMind.KnowledgeHub.Application;

namespace TradeMind.AI.Knowledge;

public sealed class KnowledgeHubServiceSearcher : IKnowledgeSearcher
{
    private readonly KnowledgeHubService _knowledgeHubService;

    public KnowledgeHubServiceSearcher(KnowledgeHubService knowledgeHubService)
    {
        _knowledgeHubService = knowledgeHubService;
    }

    public Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        return _knowledgeHubService.SearchAsync(query, limit, cancellationToken);
    }
}
