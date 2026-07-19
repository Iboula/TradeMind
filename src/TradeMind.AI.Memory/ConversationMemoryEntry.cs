namespace TradeMind.AI.Memory;

public sealed record ConversationMemoryEntry
{
    public ConversationMemoryEntry(
        string entryId,
        ConversationMemoryRole role,
        string content,
        DateTimeOffset createdAtUtc,
        long sequenceNumber,
        string? correlationId = null,
        string? sessionId = null,
        int? tokenCount = null,
        bool isSensitive = false,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Memory entry creation date must be UTC.", nameof(createdAtUtc));
        }

        if (sequenceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber), "Sequence number must be positive.");
        }

        if (tokenCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenCount), "Token count cannot be negative.");
        }

        EntryId = entryId;
        Role = role;
        Content = content.Trim();
        CreatedAtUtc = createdAtUtc;
        SequenceNumber = sequenceNumber;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
        SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId;
        TokenCount = tokenCount;
        IsSensitive = isSensitive;
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
    }

    public string EntryId { get; }

    public ConversationMemoryRole Role { get; }

    public string Content { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public long SequenceNumber { get; }

    public string? CorrelationId { get; }

    public string? SessionId { get; }

    public int? TokenCount { get; }

    public bool IsSensitive { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }
}
