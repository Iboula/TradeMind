using TradeMind.Market.Abstractions;

namespace TradeMind.AI.Context.Domain;

public abstract record ContextData(ContextProviderCategory Category);

public sealed record MarketSnapshotContextData : ContextData
{
    public MarketSnapshotContextData(MarketSnapshot snapshot)
        : base(ContextProviderCategory.MarketSnapshot)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public MarketSnapshot Snapshot { get; }
}

public sealed record KnowledgeChunk
{
    public KnowledgeChunk(
        string fragmentId,
        string sourceId,
        string content,
        double score,
        int sequence,
        string? title = null,
        string? sourceReference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fragmentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (score is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between zero and one.");
        }

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence cannot be negative.");
        }

        FragmentId = fragmentId.Trim();
        SourceId = sourceId.Trim();
        Content = content.Trim();
        Score = score;
        Sequence = sequence;
        Title = NormalizeOptional(title);
        SourceReference = NormalizeOptional(sourceReference);
    }

    public string FragmentId { get; }
    public string SourceId { get; }
    public string Content { get; }
    public double Score { get; }
    public int Sequence { get; }
    public string? Title { get; }
    public string? SourceReference { get; }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record KnowledgeContext
{
    public KnowledgeContext(
        string query,
        IReadOnlyCollection<KnowledgeChunk> chunks,
        DateTimeOffset retrievedAtUtc,
        bool truncated,
        int availableChunkCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(chunks);
        if (availableChunkCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableChunkCount));
        }

        Query = query.Trim();
        Chunks = ContextCollections.CopyList(chunks);
        RetrievedAtUtc = retrievedAtUtc;
        Truncated = truncated;
        AvailableChunkCount = availableChunkCount;
    }

    public string Query { get; }
    public IReadOnlyList<KnowledgeChunk> Chunks { get; }
    public DateTimeOffset RetrievedAtUtc { get; }
    public bool Truncated { get; }
    public int AvailableChunkCount { get; }
}

public sealed record KnowledgeContextData : ContextData
{
    public KnowledgeContextData(KnowledgeContext context)
        : base(ContextProviderCategory.Knowledge)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public KnowledgeContext Context { get; }
}

public sealed record MemoryItem
{
    public MemoryItem(string id, string role, string content, DateTimeOffset createdAtUtc, long sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        Id = id.Trim();
        Role = role.Trim();
        Content = content.Trim();
        CreatedAtUtc = createdAtUtc;
        Sequence = sequence;
    }

    public string Id { get; }
    public string Role { get; }
    public string Content { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public long Sequence { get; }
}

public sealed record MemoryContext
{
    public MemoryContext(
        string conversationId,
        string? summary,
        IReadOnlyCollection<MemoryItem> items,
        bool truncated,
        int availableItemCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(items);
        if (availableItemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableItemCount));
        }

        ConversationId = conversationId.Trim();
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        Items = ContextCollections.CopyList(items);
        Truncated = truncated;
        AvailableItemCount = availableItemCount;
    }

    public string ConversationId { get; }
    public string? Summary { get; }
    public IReadOnlyList<MemoryItem> Items { get; }
    public bool Truncated { get; }
    public int AvailableItemCount { get; }
}

public sealed record MemoryContextData : ContextData
{
    public MemoryContextData(MemoryContext context)
        : base(ContextProviderCategory.Memory)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public MemoryContext Context { get; }
}

public sealed record TraderProfileContext
{
    public TraderProfileContext(IReadOnlyDictionary<string, string>? attributes)
    {
        Attributes = ContextCollections.CopyDictionary(attributes);
    }

    public IReadOnlyDictionary<string, string> Attributes { get; }
}

public sealed record TraderProfileContextData : ContextData
{
    public TraderProfileContextData(TraderProfileContext context)
        : base(ContextProviderCategory.TraderProfile)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public TraderProfileContext Context { get; }
}

public sealed record WorkspaceContext
{
    public WorkspaceContext(IReadOnlyDictionary<string, string>? values)
    {
        Values = ContextCollections.CopyDictionary(values);
    }

    public IReadOnlyDictionary<string, string> Values { get; }
}

public sealed record WorkspaceContextData : ContextData
{
    public WorkspaceContextData(WorkspaceContext context)
        : base(ContextProviderCategory.Workspace)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public WorkspaceContext Context { get; }
}

public sealed record NewsItem(
    string Id,
    string Headline,
    DateTimeOffset PublishedAtUtc,
    string? SourceReference = null);

public sealed record NewsContext
{
    public NewsContext(IReadOnlyCollection<NewsItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = ContextCollections.CopyList(items);
    }

    public IReadOnlyList<NewsItem> Items { get; }
}

public sealed record NewsContextData : ContextData
{
    public NewsContextData(NewsContext context)
        : base(ContextProviderCategory.News)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public NewsContext Context { get; }
}

public sealed record EconomicCalendarEvent(
    string Id,
    string Title,
    DateTimeOffset ScheduledAtUtc,
    string? Currency = null,
    string? Importance = null);

public sealed record EconomicCalendarContext
{
    public EconomicCalendarContext(IReadOnlyCollection<EconomicCalendarEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        Events = ContextCollections.CopyList(events);
    }

    public IReadOnlyList<EconomicCalendarEvent> Events { get; }
}

public sealed record EconomicCalendarContextData : ContextData
{
    public EconomicCalendarContextData(EconomicCalendarContext context)
        : base(ContextProviderCategory.EconomicCalendar)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public EconomicCalendarContext Context { get; }
}
