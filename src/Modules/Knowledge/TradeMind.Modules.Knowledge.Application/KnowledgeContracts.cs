using TradeMind.Modules.Knowledge.Domain;

namespace TradeMind.Modules.Knowledge.Application;

public sealed record ImportKnowledgeSourceCommand(string Name, string MediaType, Stream Content);
public sealed record KnowledgeSourceResponse(Guid Id, string Name, string MediaType, string Status, int FragmentCount, DateTimeOffset CreatedOnUtc);
public sealed record KnowledgeSearchResult(Guid SourceId, Guid FragmentId, string SourceName, string Content, double Score);

public interface IKnowledgeImporter
{
    Task<KnowledgeSource> ImportAsync(ImportKnowledgeSourceCommand command, CancellationToken cancellationToken);
}

public interface ITextExtractor
{
    bool Supports(string mediaType);
    Task<string> ExtractAsync(Stream content, CancellationToken cancellationToken);
}

public interface IFragmenter
{
    IReadOnlyList<string> Fragment(string text);
}

public interface IEmbeddingGenerator
{
    int Dimensions { get; }
    Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken);
}

public interface IKnowledgeIndexer
{
    Task IndexAsync(KnowledgeSource source, CancellationToken cancellationToken);
}

public interface IKnowledgeSearcher
{
    Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken);
}

public interface IKnowledgeSourceRepository
{
    Task AddAsync(KnowledgeSource source, CancellationToken cancellationToken);
    Task<KnowledgeSource?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public interface IKnowledgeUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IKnowledgeClock
{
    DateTimeOffset UtcNow { get; }
}
