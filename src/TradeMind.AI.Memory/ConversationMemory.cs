namespace TradeMind.AI.Memory;

public sealed record ConversationMemory
{
    public ConversationMemory(
        ConversationMemoryKey key,
        IReadOnlyList<ConversationMemoryEntry> entries,
        ConversationSummary? summary,
        DateTimeOffset createdAtUtc,
        DateTimeOffset lastUpdatedAtUtc,
        DateTimeOffset? expiresAtUtc,
        long revision)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(entries);

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Memory creation date must be UTC.", nameof(createdAtUtc));
        }

        if (lastUpdatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Memory update date must be UTC.", nameof(lastUpdatedAtUtc));
        }

        if (expiresAtUtc is not null && expiresAtUtc.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Memory expiration date must be UTC.", nameof(expiresAtUtc));
        }

        if (lastUpdatedAtUtc < createdAtUtc)
        {
            throw new ArgumentException("Memory update date cannot be earlier than creation date.", nameof(lastUpdatedAtUtc));
        }

        if (revision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision), "Memory revision must be positive.");
        }

        Key = key;
        Entries = entries.OrderBy(entry => entry.SequenceNumber).ToArray();
        Summary = summary;
        CreatedAtUtc = createdAtUtc;
        LastUpdatedAtUtc = lastUpdatedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Revision = revision;
    }

    public ConversationMemoryKey Key { get; }

    public IReadOnlyList<ConversationMemoryEntry> Entries { get; }

    public ConversationSummary? Summary { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset LastUpdatedAtUtc { get; }

    public DateTimeOffset? ExpiresAtUtc { get; }

    public long Revision { get; }
}
