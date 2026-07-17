using TradeMind.Modules.Knowledge.Domain;

namespace TradeMind.Modules.Knowledge.Application;

public sealed record CreateKnowledgeDocumentCommand(string Title, string Source);
public sealed record KnowledgeDocumentResponse(Guid Id, string Title, string Source, string Status, int ChunkCount);

public interface IKnowledgeDocumentRepository
{
    Task AddAsync(KnowledgeDocument document, CancellationToken cancellationToken);
    Task<KnowledgeDocument?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public interface IKnowledgeTextChunker
{
    IReadOnlyCollection<string> Chunk(string content);
}

public interface IKnowledgeClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IKnowledgeUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
