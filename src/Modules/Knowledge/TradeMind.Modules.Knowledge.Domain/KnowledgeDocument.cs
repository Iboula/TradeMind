using TradeMind.BuildingBlocks.Domain;

namespace TradeMind.Modules.Knowledge.Domain;

public sealed class KnowledgeDocument : AggregateRoot<Guid>
{
    private readonly List<KnowledgeChunk> _chunks = [];

    private KnowledgeDocument(Guid id, string title, string source, DateTimeOffset createdOnUtc)
        : base(id)
    {
        Title = title;
        Source = source;
        CreatedOnUtc = createdOnUtc;
        Status = KnowledgeDocumentStatus.Pending;
    }

    public string Title { get; private set; }
    public string Source { get; private set; }
    public KnowledgeDocumentStatus Status { get; private set; }
    public DateTimeOffset CreatedOnUtc { get; }
    public IReadOnlyCollection<KnowledgeChunk> Chunks => _chunks.AsReadOnly();

    public static KnowledgeDocument Create(string title, string source, DateTimeOffset createdOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var document = new KnowledgeDocument(Guid.NewGuid(), title.Trim(), source.Trim(), createdOnUtc);
        document.Raise(new KnowledgeDocumentCreated(document.Id, document.Title, createdOnUtc));
        return document;
    }

    public void ReplaceChunks(IEnumerable<string> chunks, DateTimeOffset indexedOnUtc)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        _chunks.Clear();
        _chunks.AddRange(chunks
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select((value, index) => new KnowledgeChunk(Guid.NewGuid(), index, value.Trim())));

        Status = KnowledgeDocumentStatus.Indexed;
        Raise(new KnowledgeDocumentIndexed(Id, _chunks.Count, indexedOnUtc));
    }
}

public sealed record KnowledgeChunk(Guid Id, int Position, string Content);

public enum KnowledgeDocumentStatus
{
    Pending = 0,
    Indexed = 1,
    Failed = 2
}

public sealed record KnowledgeDocumentCreated(Guid DocumentId, string Title, DateTimeOffset OccurredOnUtc) : IDomainEvent;
public sealed record KnowledgeDocumentIndexed(Guid DocumentId, int ChunkCount, DateTimeOffset OccurredOnUtc) : IDomainEvent;
