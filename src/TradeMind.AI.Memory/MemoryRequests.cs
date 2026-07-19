namespace TradeMind.AI.Memory;

public sealed record MemoryReadRequest(
    ConversationMemoryKey Key,
    MemoryWindowOptions Options);

public sealed record MemoryReadResult
{
    public MemoryReadResult(
        ConversationMemoryKey key,
        ConversationSummary? summary,
        IReadOnlyList<ConversationMemoryEntry> selectedEntries,
        int totalAvailableEntries,
        bool truncated,
        long? oldestIncludedSequence,
        long? newestIncludedSequence,
        int? estimatedTokenCount)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(selectedEntries);

        if (totalAvailableEntries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAvailableEntries), "Total available entries cannot be negative.");
        }

        if (estimatedTokenCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedTokenCount), "Estimated token count cannot be negative.");
        }

        Key = key;
        Summary = summary;
        SelectedEntries = selectedEntries.OrderBy(entry => entry.SequenceNumber).ToArray();
        TotalAvailableEntries = totalAvailableEntries;
        SelectedEntryCount = SelectedEntries.Count;
        Truncated = truncated;
        OldestIncludedSequence = SelectedEntries.Count == 0 ? oldestIncludedSequence : SelectedEntries.Min(entry => entry.SequenceNumber);
        NewestIncludedSequence = SelectedEntries.Count == 0 ? newestIncludedSequence : SelectedEntries.Max(entry => entry.SequenceNumber);
        EstimatedTokenCount = estimatedTokenCount;
    }

    public ConversationMemoryKey Key { get; }

    public ConversationSummary? Summary { get; }

    public IReadOnlyList<ConversationMemoryEntry> SelectedEntries { get; }

    public int TotalAvailableEntries { get; }

    public int SelectedEntryCount { get; }

    public bool Truncated { get; }

    public long? OldestIncludedSequence { get; }

    public long? NewestIncludedSequence { get; }

    public int? EstimatedTokenCount { get; }
}

public sealed record MemoryWriteRequest
{
    public MemoryWriteRequest(
        ConversationMemoryKey key,
        ConversationMemoryRole role,
        string content,
        string? correlationId = null,
        string? sessionId = null,
        int? tokenCount = null,
        bool isSensitive = false,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        if (tokenCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenCount), "Token count cannot be negative.");
        }

        Key = key;
        Role = role;
        Content = content;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
        SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId;
        TokenCount = tokenCount;
        IsSensitive = isSensitive;
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
    }

    public ConversationMemoryKey Key { get; init; }

    public ConversationMemoryRole Role { get; init; }

    public string Content { get; init; }

    public string? CorrelationId { get; init; }

    public string? SessionId { get; init; }

    public int? TokenCount { get; init; }

    public bool IsSensitive { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; }
}

public sealed record ConversationSummaryRequest(
    ConversationMemoryKey Key,
    ConversationSummary? PreviousSummary,
    IReadOnlyList<ConversationMemoryEntry> Entries,
    int MaxCharacters);
