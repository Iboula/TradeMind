namespace TradeMind.KnowledgeHub.Domain;

public enum KnowledgeSourceType
{
    Text,
    Markdown,
    Pdf,
    Website,
    YouTube,
    Podcast,
    ResearchPaper,
    TradingJournal,
    BrokerRule,
    PropFirmRule,
    Conversation,
    Image,
    Csv,
    Excel,
    Json
}

public enum ProcessingStatus
{
    Pending,
    Processing,
    Ready,
    Failed
}

public sealed class KnowledgeSource
{
    private readonly List<KnowledgeFragment> _fragments = [];

    private KnowledgeSource() { }

    public KnowledgeSource(string title, KnowledgeSourceType type, string contentHash)
    {
        Id = Guid.NewGuid();
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("A title is required.", nameof(title)) : title.Trim();
        Type = type;
        ContentHash = string.IsNullOrWhiteSpace(contentHash) ? throw new ArgumentException("A content hash is required.", nameof(contentHash)) : contentHash;
        Status = ProcessingStatus.Pending;
        ImportedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public KnowledgeSourceType Type { get; private set; }
    public string ContentHash { get; private set; } = string.Empty;
    public ProcessingStatus Status { get; private set; }
    public DateTimeOffset ImportedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public IReadOnlyCollection<KnowledgeFragment> Fragments => _fragments;

    public void StartProcessing() => Status = ProcessingStatus.Processing;

    public void AddFragment(int sequence, string content, int tokenCount, float[] embedding)
    {
        if (Status != ProcessingStatus.Processing)
        {
            throw new InvalidOperationException("The source must be processing before fragments can be added.");
        }

        _fragments.Add(new KnowledgeFragment(Id, sequence, content, tokenCount, embedding));
    }

    public void MarkReady()
    {
        if (_fragments.Count == 0)
        {
            throw new InvalidOperationException("A ready source must contain at least one fragment.");
        }

        Status = ProcessingStatus.Ready;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        Status = ProcessingStatus.Failed;
        FailureReason = reason;
    }
}

public sealed class KnowledgeFragment
{
    private KnowledgeFragment() { }

    internal KnowledgeFragment(Guid sourceId, int sequence, string content, int tokenCount, float[] embedding)
    {
        Id = Guid.NewGuid();
        KnowledgeSourceId = sourceId;
        Sequence = sequence;
        Content = content;
        TokenCount = tokenCount;
        Embedding = embedding;
    }

    public Guid Id { get; private set; }
    public Guid KnowledgeSourceId { get; private set; }
    public int Sequence { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public int TokenCount { get; private set; }
    public float[] Embedding { get; private set; } = [];
}
