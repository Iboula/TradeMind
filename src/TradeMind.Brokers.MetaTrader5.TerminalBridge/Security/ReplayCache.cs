namespace TradeMind.Brokers.MetaTrader5.TerminalBridge.Security;

public sealed class ReplayCache(TimeProvider timeProvider)
{
    private readonly Dictionary<string, DateTimeOffset> entries = new(StringComparer.Ordinal);
    private readonly object sync = new();

    public bool TryAccept(string nonce, DateTimeOffset expiresAt, int maximumEntries)
    {
        lock (sync)
        {
            var now = timeProvider.GetUtcNow();
            foreach (var expired in entries.Where(item => item.Value <= now).Select(item => item.Key).ToArray()) entries.Remove(expired);
            if (entries.ContainsKey(nonce)) return false;
            if (entries.Count >= maximumEntries)
            {
                var oldest = entries.OrderBy(item => item.Value).First().Key;
                entries.Remove(oldest);
            }
            entries[nonce] = expiresAt;
            return true;
        }
    }
}
