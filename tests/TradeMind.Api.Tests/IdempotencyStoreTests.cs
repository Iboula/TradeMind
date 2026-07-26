namespace TradeMind.Api.Tests;

public sealed class IdempotencyStoreTests
{
    [Fact]
    public async Task Entries_expire_without_sleeping()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var store = new InMemoryIdempotencyStore(
            Options.Create(new TradeMind.Api.Composition.ApiOptions()),
            clock);
        var calls = 0;
        Task<CapturedResponse> Operation(CancellationToken _) =>
            Task.FromResult(new CapturedResponse(++calls, "application/json", Encoding.UTF8.GetBytes(calls.ToString())));

        await store.ExecuteAsync("key", "hash", Operation, CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(16));
        await store.ExecuteAsync("key", "hash", Operation, CancellationToken.None);
        Assert.Equal(2, calls);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
