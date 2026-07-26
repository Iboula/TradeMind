using TradeMind.AI.Abstractions;
using TradeMind.KnowledgeHub.Application;

namespace TradeMind.AI.Knowledge;

public interface IKnowledgeSearcher
{
    Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);
}

public interface IKnowledgeContextRetriever
{
    Task<KnowledgeContextResult> RetrieveAsync(
        KnowledgeContextRequest request,
        CancellationToken cancellationToken);
}

public interface IKnowledgeContextComposer
{
    IReadOnlyList<ChatMessage> Compose(KnowledgeContextResult result, string contextLabel);
}

public interface IKnowledgeTokenEstimator
{
    int EstimateTokens(string content);
}
