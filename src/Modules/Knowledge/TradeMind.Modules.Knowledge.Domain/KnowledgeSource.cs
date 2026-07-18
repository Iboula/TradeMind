using TradeMind.BuildingBlocks.Domain;

namespace TradeMind.Modules.Knowledge.Domain;

public sealed class KnowledgeSource : AggregateRoot<Guid>
{
    private readonly List<KnowledgeFragment> _fragments = [];

    private KnowledgeSource() : base(Guid.Empty) { }

    private KnowledgeSource(Guid id, string name, string mediaType, DateTimeOffset createdOnUtc)
        : base(id)
    {
        Name = name;
        MediaType = mediaType;
        CreatedOnUtc = createdOnUtc;
        Status = KnowledgeSourceStatus.Imported;
    }

    public string Name { get; private set; } = string.Empty;
    public string MediaType { get; private set; } = string.Empty;
    public KnowledgeSourceStatus Status { get; private set; }
    public DateTimeOffset CreatedOnUtc { get; private set; }
    public DateTimeOffset? IndexedOnUtc { get; private set; }
    public IReadOnlyCollection<KnowledgeFragment> Fragments => _fragments.AsReadOnly();

    public static KnowledgeSource Import(string name, string mediaType, DateTimeOffset createdOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

        var source = new KnowledgeSource(Guid.NewGuid(), name.Trim(), mediaType.Trim(), createdOnUtc);
        source.Raise(new KnowledgeSourceImported(source.Id, source.Name, createdOnUtc));
        return source;
    }

    public void ReplaceFragments(IEnumerable<string> contents, IReadOnlyList<float[]> embeddings, DateTimeOffset indexedOnUtc)
    {
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(embeddings);

        var normalized = contents.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value.Trim()).ToArray();
        if (normalized.Length != embeddings.Count)
        {
            throw new ArgumentException("Every fragment must have exactly one embedding.", nameof(embeddings));
        }

        _fragments.Clear();
        for (var index = 0; index < normalized.Length; index++)
        {
            _fragments.Add(KnowledgeFragment.Create(Id, index, normalized[index], embeddings[index]));
        }

        Status = KnowledgeSourceStatus.Indexed;
        IndexedOnUtc = indexedOnUtc;
        Raise(new KnowledgeSourceIndexed(Id, _fragments.Count, indexedOnUtc));
    }
}

public enum KnowledgeSourceStatus
{
    Imported = 0,
    Indexed = 1,
    Failed = 2
}

public sealed record KnowledgeSourceImported(Guid SourceId, string Name, DateTimeOffset OccurredOnUtc) : IDomainEvent;
public sealed record KnowledgeSourceIndexed(Guid SourceId, int FragmentCount, DateTimeOffset OccurredOnUtc) : IDomainEvent;
