using System.Collections.Concurrent;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Execution;

public sealed class InMemoryBrokerIdempotencyStore : IBrokerIdempotencyStore
{
    private sealed class Entry(string requestHash)
    {
        public string RequestHash { get; } = requestHash;
        public TaskCompletionSource<BrokerExecutionResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public async Task<BrokerIdempotencyExecutionResult> ExecuteAsync(string key, string requestHash, Func<CancellationToken, Task<BrokerExecutionResult>> operation, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        ArgumentNullException.ThrowIfNull(operation);
        var candidate = new Entry(requestHash);
        if (_entries.TryAdd(key, candidate))
        {
            try
            {
                var result = await operation(cancellationToken).ConfigureAwait(false);
                candidate.Completion.TrySetResult(result);
                return new BrokerIdempotencyExecutionResult(result, false, false, false);
            }
            catch
            {
                _entries.TryRemove(new KeyValuePair<string, Entry>(key, candidate));
                candidate.Completion.TrySetCanceled(CancellationToken.None);
                throw;
            }
        }

        if (!_entries.TryGetValue(key, out var existing))
        {
            return new BrokerIdempotencyExecutionResult(Conflict(), false, false, true);
        }
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return new BrokerIdempotencyExecutionResult(Conflict(), false, true, false);
        }

        try
        {
            var result = await existing.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new BrokerIdempotencyExecutionResult(result, true, false, false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new BrokerIdempotencyExecutionResult(Conflict(), false, false, true);
        }
    }

    private static BrokerExecutionResult Conflict() => new(new BrokerExecutionId("idempotency-conflict"), BrokerExecutionResultStatus.Conflict, null, null, new BrokerError("IDEMPOTENCY_CONFLICT", BrokerErrorCategory.Conflict, "The idempotency key is associated with a different request.", false, false, null, new BrokerTraceReference(null, null, null), DateTimeOffset.UnixEpoch), "SubmitOrder", DateTimeOffset.UnixEpoch);
}
