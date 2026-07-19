using Microsoft.Extensions.Logging;

namespace TradeMind.AI.Memory;

public sealed class MemoryWriter : IMemoryWriter
{
    private readonly IMemoryStore _store;
    private readonly IConversationSummarizer _summarizer;
    private readonly MemoryCompactionOptions _compactionOptions;
    private readonly ILogger<MemoryWriter> _logger;

    public MemoryWriter(
        IMemoryStore store,
        IConversationSummarizer summarizer,
        MemoryCompactionOptions compactionOptions,
        ILogger<MemoryWriter> logger)
    {
        _store = store;
        _summarizer = summarizer;
        _compactionOptions = compactionOptions;
        _logger = logger;
    }

    public Task<ConversationMemoryEntry> WriteUserMessageAsync(
        MemoryWriteRequest request,
        CancellationToken cancellationToken)
    {
        return WriteAsync(request with { Role = ConversationMemoryRole.User }, cancellationToken);
    }

    public Task<ConversationMemoryEntry> WriteAssistantMessageAsync(
        MemoryWriteRequest request,
        CancellationToken cancellationToken)
    {
        return WriteAsync(request with { Role = ConversationMemoryRole.Assistant }, cancellationToken);
    }

    private async Task<ConversationMemoryEntry> WriteAsync(
        MemoryWriteRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Memory write started for conversation {ConversationId}, tenant {TenantId}, user {UserId}, session {SessionId}, correlation {CorrelationId}, and role {Role}",
            request.Key.ConversationId,
            request.Key.TenantId,
            request.Key.UserId,
            request.SessionId,
            request.CorrelationId,
            request.Role);

        var entry = await _store.AppendAsync(request, cancellationToken).ConfigureAwait(false);
        await CompactIfNeededAsync(request.Key, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Memory write completed for conversation {ConversationId}, tenant {TenantId}, user {UserId}, sequence {SequenceNumber}, and role {Role}",
            request.Key.ConversationId,
            request.Key.TenantId,
            request.Key.UserId,
            entry.SequenceNumber,
            request.Role);

        return entry;
    }

    private async Task CompactIfNeededAsync(
        ConversationMemoryKey key,
        CancellationToken cancellationToken)
    {
        if (!_compactionOptions.Enabled)
        {
            return;
        }

        var memory = await _store.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (memory is null || memory.Entries.Count < _compactionOptions.SummarizeAfterEntryCount)
        {
            return;
        }

        var summarizable = memory.Entries
            .OrderBy(entry => entry.SequenceNumber)
            .Take(Math.Max(1, memory.Entries.Count - _compactionOptions.EntriesToKeepAfterSummary))
            .Where(entry => memory.Summary is null || entry.SequenceNumber > memory.Summary.SummarizedThroughSequence)
            .ToArray();

        if (summarizable.Length == 0)
        {
            return;
        }

        var summary = await _summarizer.SummarizeAsync(
            new ConversationSummaryRequest(
                key,
                memory.Summary,
                summarizable,
                _compactionOptions.SummaryMaxCharacters),
            cancellationToken).ConfigureAwait(false);

        await _store.SaveSummaryAsync(key, summary, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Memory summary saved for conversation {ConversationId}, tenant {TenantId}, user {UserId}, summarized through sequence {SequenceNumber}, and version {Version}",
            key.ConversationId,
            key.TenantId,
            key.UserId,
            summary.SummarizedThroughSequence,
            summary.Version);
    }
}
