using TradeMind.Modules.KnowledgeHub.Domain;

namespace TradeMind.Modules.KnowledgeHub.Application;

public interface IKnowledgeImporter { Task<ImportedKnowledge> ImportAsync(Stream stream, string fileName, string mediaType, CancellationToken ct); }
public interface ITextExtractor { Task<string> ExtractAsync(ImportedKnowledge knowledge, CancellationToken ct); }
public interface IFragmenter { IReadOnlyList<string> Fragment(string text); }
public interface IEmbeddingGenerator { Task<float[]> GenerateAsync(string text, CancellationToken ct); }
public interface IKnowledgeIndexer { Task IndexAsync(KnowledgeSource source, CancellationToken ct); }
public interface IKnowledgeSearcher { Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(string query, int limit, CancellationToken ct); }
public interface IKnowledgeSourceRepository
{
    Task AddAsync(KnowledgeSource source, CancellationToken ct);
    Task<KnowledgeSourceDto?> GetAsync(Guid id, CancellationToken ct);
}
public interface IKnowledgeHubUnitOfWork { Task SaveChangesAsync(CancellationToken ct); }

public sealed record ImportedKnowledge(byte[] Content, string FileName, string MediaType);
public sealed record KnowledgeSearchResult(Guid SourceId, Guid FragmentId, string FileName, string Content, double Score);
public sealed record KnowledgeSourceDto(Guid Id, string FileName, string MediaType, string Status, int FragmentCount, DateTimeOffset CreatedAtUtc);
