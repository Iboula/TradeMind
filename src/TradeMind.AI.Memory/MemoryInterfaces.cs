namespace TradeMind.AI.Memory;

public interface IMemoryStore
{
    Task<ConversationMemory?> GetAsync(
        ConversationMemoryKey key,
        CancellationToken cancellationToken);

    Task<ConversationMemoryEntry> AppendAsync(
        MemoryWriteRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConversationMemoryEntry>> AppendRangeAsync(
        ConversationMemoryKey key,
        IReadOnlyList<MemoryWriteRequest> requests,
        CancellationToken cancellationToken);

    Task SaveSummaryAsync(
        ConversationMemoryKey key,
        ConversationSummary summary,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        ConversationMemoryKey key,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        ConversationMemoryKey key,
        CancellationToken cancellationToken);
}

public interface IMemoryReader
{
    Task<MemoryReadResult> ReadAsync(
        MemoryReadRequest request,
        CancellationToken cancellationToken);
}

public interface IMemoryWriter
{
    Task<ConversationMemoryEntry> WriteUserMessageAsync(
        MemoryWriteRequest request,
        CancellationToken cancellationToken);

    Task<ConversationMemoryEntry> WriteAssistantMessageAsync(
        MemoryWriteRequest request,
        CancellationToken cancellationToken);
}

public interface IConversationSummarizer
{
    Task<ConversationSummary> SummarizeAsync(
        ConversationSummaryRequest request,
        CancellationToken cancellationToken);
}

public interface ITokenEstimator
{
    int EstimateTokens(string content);
}
