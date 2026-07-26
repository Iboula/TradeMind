using System.Collections.Concurrent;

namespace TradeMind.AI.Memory;

public sealed class InMemoryMemoryStore : IMemoryStore
{
    private readonly ConcurrentDictionary<ConversationMemoryKey, MemoryState> _memories = [];
    private readonly ConcurrentDictionary<ConversationMemoryKey, SemaphoreSlim> _locks = [];
    private readonly MemoryRetentionOptions _retentionOptions;
    private readonly TimeProvider _timeProvider;

    public InMemoryMemoryStore(
        MemoryRetentionOptions retentionOptions,
        TimeProvider timeProvider)
    {
        _retentionOptions = retentionOptions;
        _timeProvider = timeProvider;
    }

    public async Task<ConversationMemory?> GetAsync(
        ConversationMemoryKey key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        var gate = GetLock(key);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_memories.TryGetValue(key, out var state))
            {
                CleanupLockIfUnused(key);
                return null;
            }

            if (IsExpired(state))
            {
                if (_retentionOptions.RemoveExpiredOnAccess)
                {
                    _memories.TryRemove(key, out _);
                    CleanupLockIfUnused(key);
                }

                return null;
            }

            if (_retentionOptions.SlidingExpiration && _retentionOptions.DefaultTimeToLive is not null)
            {
                state.ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_retentionOptions.DefaultTimeToLive.Value);
                state.Revision++;
            }

            return Snapshot(state);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ConversationMemoryEntry> AppendAsync(
        MemoryWriteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entries = await AppendCoreAsync(request.Key, [request], cancellationToken).ConfigureAwait(false);
        return entries.Single();
    }

    public Task<IReadOnlyList<ConversationMemoryEntry>> AppendRangeAsync(
        ConversationMemoryKey key,
        IReadOnlyList<MemoryWriteRequest> requests,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<ConversationMemoryEntry>>([]);
        }

        if (requests.Any(request => request.Key != key))
        {
            throw new MemoryValidationException("All memory write requests must target the same key.", key);
        }

        return AppendCoreAsync(key, requests, cancellationToken);
    }

    public async Task SaveSummaryAsync(
        ConversationMemoryKey key,
        ConversationSummary summary,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(summary);
        cancellationToken.ThrowIfCancellationRequested();

        var gate = GetLock(key);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _timeProvider.GetUtcNow();
            var state = _memories.GetOrAdd(key, _ => CreateState(key, now));
            state.Summary = summary;
            state.LastUpdatedAtUtc = now;
            state.Revision++;
            RefreshExpiration(state, now);
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<bool> DeleteAsync(
        ConversationMemoryKey key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        var removed = _memories.TryRemove(key, out _);
        CleanupLockIfUnused(key);
        return Task.FromResult(removed);
    }

    public async Task<bool> ExistsAsync(
        ConversationMemoryKey key,
        CancellationToken cancellationToken)
    {
        return await GetAsync(key, cancellationToken).ConfigureAwait(false) is not null;
    }

    private async Task<IReadOnlyList<ConversationMemoryEntry>> AppendCoreAsync(
        ConversationMemoryKey key,
        IReadOnlyList<MemoryWriteRequest> requests,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var gate = GetLock(key);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _timeProvider.GetUtcNow();
            var state = _memories.GetOrAdd(key, _ => CreateState(key, now));
            if (IsExpired(state))
            {
                state = CreateState(key, now);
                _memories[key] = state;
            }

            var appended = new List<ConversationMemoryEntry>(requests.Count);
            foreach (var request in requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sequence = state.NextSequence++;
                var entry = new ConversationMemoryEntry(
                    Guid.NewGuid().ToString("N"),
                    request.Role,
                    request.Content,
                    now,
                    sequence,
                    request.CorrelationId,
                    request.SessionId,
                    request.TokenCount,
                    request.IsSensitive,
                    request.Metadata);
                state.Entries.Add(entry);
                appended.Add(entry);
            }

            state.LastUpdatedAtUtc = now;
            state.Revision++;
            RefreshExpiration(state, now);
            return appended.ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    private SemaphoreSlim GetLock(ConversationMemoryKey key)
    {
        return _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    private void CleanupLockIfUnused(ConversationMemoryKey key)
    {
        if (!_memories.ContainsKey(key))
        {
            _locks.TryRemove(key, out _);
        }
    }

    private bool IsExpired(MemoryState state)
    {
        return state.ExpiresAtUtc is not null && state.ExpiresAtUtc <= _timeProvider.GetUtcNow();
    }

    private MemoryState CreateState(ConversationMemoryKey key, DateTimeOffset now)
    {
        return new MemoryState
        {
            Key = key,
            CreatedAtUtc = now,
            LastUpdatedAtUtc = now,
            ExpiresAtUtc = _retentionOptions.DefaultTimeToLive is null
                ? null
                : now.Add(_retentionOptions.DefaultTimeToLive.Value)
        };
    }

    private void RefreshExpiration(MemoryState state, DateTimeOffset now)
    {
        if (_retentionOptions.DefaultTimeToLive is not null)
        {
            state.ExpiresAtUtc = now.Add(_retentionOptions.DefaultTimeToLive.Value);
        }
    }

    private static ConversationMemory Snapshot(MemoryState state)
    {
        return new ConversationMemory(
            state.Key,
            state.Entries.ToArray(),
            state.Summary,
            state.CreatedAtUtc,
            state.LastUpdatedAtUtc,
            state.ExpiresAtUtc,
            state.Revision);
    }

    private sealed class MemoryState
    {
        public required ConversationMemoryKey Key { get; init; }

        public List<ConversationMemoryEntry> Entries { get; } = [];

        public ConversationSummary? Summary { get; set; }

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset LastUpdatedAtUtc { get; set; }

        public DateTimeOffset? ExpiresAtUtc { get; set; }

        public long Revision { get; set; } = 1;

        public long NextSequence { get; set; } = 1;
    }
}
