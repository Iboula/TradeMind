using TradeMind.BuildingBlocks.Domain;

namespace TradeMind.Modules.KnowledgeHub.Domain;

public enum KnowledgeSourceStatus { Imported, Extracted, Normalized, Fragmented, Indexed, Failed }

public sealed class KnowledgeSource : AggregateRoot<Guid>
{
    private readonly List<KnowledgeFragment> _fragments = [];

    private KnowledgeSource(Guid id, string fileName, string mediaType, DateTimeOffset createdAtUtc) : base(id)
    {
        FileName = fileName;
        MediaType = mediaType;
        CreatedAtUtc = createdAtUtc;
        Status = KnowledgeSourceStatus.Imported;
    }

    public string FileName { get; private set; }
    public string MediaType { get; private set; }
    public string? ExtractedText { get; private set; }
    public KnowledgeSourceStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public IReadOnlyCollection<KnowledgeFragment> Fragments => _fragments.AsReadOnly();

    public static KnowledgeSource Create(string fileName, string mediaType, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        return new KnowledgeSource(Guid.NewGuid(), fileName.Trim(), mediaType.Trim(), createdAtUtc);
    }

    public void SetExtractedText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ExtractedText = text.Trim();
        Status = KnowledgeSourceStatus.Extracted;
    }

    public void ReplaceFragments(IEnumerable<string> fragments)
    {
        _fragments.Clear();
        var position = 0;
        foreach (var content in fragments.Where(x => !string.IsNullOrWhiteSpace(x)))
            _fragments.Add(KnowledgeFragment.Create(Id, position++, content.Trim()));
        if (_fragments.Count == 0) throw new InvalidOperationException("A source must contain at least one fragment.");
        Status = KnowledgeSourceStatus.Fragmented;
    }

    public void MarkIndexed() => Status = KnowledgeSourceStatus.Indexed;
}

public sealed class KnowledgeFragment : Entity<Guid>
{
    private KnowledgeFragment(Guid id, Guid sourceId, int position, string content) : base(id)
    { SourceId = sourceId; Position = position; Content = content; }

    public Guid SourceId { get; }
    public int Position { get; }
    public string Content { get; }
    public Embedding? Embedding { get; private set; }

    internal static KnowledgeFragment Create(Guid sourceId, int position, string content) => new(Guid.NewGuid(), sourceId, position, content);
    public void SetEmbedding(float[] values) => Embedding = Embedding.Create(Id, values);
}

public sealed class Embedding : Entity<Guid>
{
    private Embedding(Guid id, Guid fragmentId, float[] values) : base(id) { FragmentId = fragmentId; Values = values; }
    public Guid FragmentId { get; }
    public float[] Values { get; }
    public static Embedding Create(Guid fragmentId, float[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0) throw new ArgumentException("Embedding cannot be empty.", nameof(values));
        return new Embedding(Guid.NewGuid(), fragmentId, values);
    }
}

public sealed record KnowledgeConcept(Guid Id, string Name);
public sealed record KnowledgeRelation(Guid Id, Guid FromConceptId, Guid ToConceptId, string RelationType);
