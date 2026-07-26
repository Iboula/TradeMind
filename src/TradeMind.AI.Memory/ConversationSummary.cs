namespace TradeMind.AI.Memory;

public sealed record ConversationSummary
{
    public ConversationSummary(
        string content,
        long summarizedThroughSequence,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        int sourceEntryCount,
        int? tokenCount = null,
        string? modelName = null,
        string? providerName = null,
        long version = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        if (summarizedThroughSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(summarizedThroughSequence), "Summarized sequence must be positive.");
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Summary creation date must be UTC.", nameof(createdAtUtc));
        }

        if (updatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Summary update date must be UTC.", nameof(updatedAtUtc));
        }

        if (updatedAtUtc < createdAtUtc)
        {
            throw new ArgumentException("Summary update date cannot be earlier than creation date.", nameof(updatedAtUtc));
        }

        if (sourceEntryCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceEntryCount), "Source entry count must be positive.");
        }

        if (tokenCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenCount), "Token count cannot be negative.");
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Summary version must be positive.");
        }

        Content = content.Trim();
        SummarizedThroughSequence = summarizedThroughSequence;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        SourceEntryCount = sourceEntryCount;
        TokenCount = tokenCount;
        ModelName = string.IsNullOrWhiteSpace(modelName) ? null : modelName;
        ProviderName = string.IsNullOrWhiteSpace(providerName) ? null : providerName;
        Version = version;
    }

    public string Content { get; }

    public long SummarizedThroughSequence { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public int SourceEntryCount { get; }

    public int? TokenCount { get; }

    public string? ModelName { get; }

    public string? ProviderName { get; }

    public long Version { get; }
}
