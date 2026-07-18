namespace TradeMind.Modules.Knowledge.Domain;

public sealed class KnowledgeFragment
{
    private KnowledgeFragment() { }

    private KnowledgeFragment(Guid id, Guid sourceId, int position, string content, Embedding embedding)
    {
        Id = id;
        SourceId = sourceId;
        Position = position;
        Content = content;
        Embedding = embedding;
    }

    public Guid Id { get; private set; }
    public Guid SourceId { get; private set; }
    public int Position { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public Embedding Embedding { get; private set; } = null!;

    internal static KnowledgeFragment Create(Guid sourceId, int position, string content, float[] vector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentNullException.ThrowIfNull(vector);
        if (vector.Length == 0)
        {
            throw new ArgumentException("Embedding vector cannot be empty.", nameof(vector));
        }

        var id = Guid.NewGuid();
        return new KnowledgeFragment(id, sourceId, position, content.Trim(), Embedding.Create(id, vector));
    }
}

public sealed class Embedding
{
    private Embedding() { }

    private Embedding(Guid fragmentId, float[] vector)
    {
        FragmentId = fragmentId;
        Vector = vector;
    }

    public Guid FragmentId { get; private set; }
    public float[] Vector { get; private set; } = [];

    internal static Embedding Create(Guid fragmentId, float[] vector) => new(fragmentId, [.. vector]);
}

public sealed record KnowledgeRelation(Guid Id, Guid FromConceptId, Guid ToConceptId, string RelationType);
public sealed record KnowledgeConcept(Guid Id, string Name, string NormalizedName);
