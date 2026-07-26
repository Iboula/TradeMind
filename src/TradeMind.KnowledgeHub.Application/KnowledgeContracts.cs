using TradeMind.KnowledgeHub.Domain;

namespace TradeMind.KnowledgeHub.Application;

public interface ITextExtractor
{
    Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken);
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

public interface IKnowledgeSourceRepository
{
    Task<bool> ExistsByHashAsync(string hash, CancellationToken cancellationToken);
    Task AddAsync(KnowledgeSource source, CancellationToken cancellationToken);
    Task<KnowledgeSource?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(float[] embedding, int limit, CancellationToken cancellationToken);
}

public sealed record KnowledgeSearchResult(
    Guid SourceId,
    string SourceTitle,
    Guid FragmentId,
    int Sequence,
    string Content,
    double Score);

public sealed record IngestKnowledgeSourceRequest(string Title, string FileName, Stream Content);
