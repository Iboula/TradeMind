namespace TradeMind.AI.Memory;

public sealed class MemoryReader : IMemoryReader
{
    private readonly IMemoryStore _store;
    private readonly ITokenEstimator _tokenEstimator;

    public MemoryReader(
        IMemoryStore store,
        ITokenEstimator tokenEstimator)
    {
        _store = store;
        _tokenEstimator = tokenEstimator;
    }

    public async Task<MemoryReadResult> ReadAsync(
        MemoryReadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var memory = await _store.GetAsync(request.Key, cancellationToken).ConfigureAwait(false);
        if (memory is null)
        {
            return new MemoryReadResult(request.Key, null, [], 0, false, null, null, 0);
        }

        var summary = request.Options.IncludeSummary ? memory.Summary : null;
        var entries = memory.Entries
            .Where(entry => request.Options.IncludeSystemMessages || entry.Role != ConversationMemoryRole.System)
            .Where(entry => summary is null || entry.SequenceNumber > summary.SummarizedThroughSequence)
            .OrderBy(entry => entry.SequenceNumber)
            .ToArray();

        var selected = SelectWindow(entries, request.Options);
        var estimatedTokens = selected.Sum(GetTokenCount);
        if (summary is not null)
        {
            estimatedTokens += summary.TokenCount ?? _tokenEstimator.EstimateTokens(summary.Content);
        }

        var truncated = selected.Count < entries.Length;

        return new MemoryReadResult(
            request.Key,
            summary,
            selected,
            memory.Entries.Count,
            truncated,
            selected.Count == 0 ? null : selected.Min(entry => entry.SequenceNumber),
            selected.Count == 0 ? null : selected.Max(entry => entry.SequenceNumber),
            estimatedTokens);
    }

    private IReadOnlyList<ConversationMemoryEntry> SelectWindow(
        IReadOnlyList<ConversationMemoryEntry> entries,
        MemoryWindowOptions options)
    {
        var selected = new List<ConversationMemoryEntry>();
        var selectedUser = 0;
        var selectedAssistant = 0;
        var characters = 0;
        var tokens = 0;

        foreach (var entry in entries.Reverse())
        {
            var entryTokens = GetTokenCount(entry);
            var mustKeep = IsMinimumRequired(entry, selectedUser, selectedAssistant, options);
            var wouldFit = selected.Count < options.MaxEntries
                && (options.MaxCharacters is null || characters + entry.Content.Length <= options.MaxCharacters.Value)
                && (options.MaxEstimatedTokens is null || tokens + entryTokens <= options.MaxEstimatedTokens.Value);

            if (!wouldFit && !mustKeep)
            {
                continue;
            }

            selected.Add(entry);
            characters += entry.Content.Length;
            tokens += entryTokens;

            if (entry.Role == ConversationMemoryRole.User)
            {
                selectedUser++;
            }
            else if (entry.Role == ConversationMemoryRole.Assistant)
            {
                selectedAssistant++;
            }
        }

        return selected
            .OrderBy(entry => entry.SequenceNumber)
            .TakeLast(options.MaxEntries)
            .ToArray();
    }

    private static bool IsMinimumRequired(
        ConversationMemoryEntry entry,
        int selectedUser,
        int selectedAssistant,
        MemoryWindowOptions options)
    {
        return entry.Role switch
        {
            ConversationMemoryRole.User => selectedUser < options.RecentUserMessagesMinimum,
            ConversationMemoryRole.Assistant => selectedAssistant < options.RecentAssistantMessagesMinimum,
            _ => false
        };
    }

    private int GetTokenCount(ConversationMemoryEntry entry)
    {
        return entry.TokenCount ?? _tokenEstimator.EstimateTokens(entry.Content);
    }
}
