using TradeMind.Brokers.MetaTrader5.Configuration;

namespace TradeMind.Brokers.MetaTrader5.Retry;

public sealed class MT5ReconnectPolicy(MT5Options options) : IMT5ReconnectPolicy
{
    public bool ShouldReconnect(int attempt, Exception exception) => attempt < options.ReconnectAttempts && exception is not OperationCanceledException;
    public TimeSpan GetDelay(int attempt) => TimeSpan.FromMilliseconds(Math.Min(1000, 50 * Math.Pow(2, Math.Max(0, attempt))));
}

public sealed class MT5RetryPolicy(MT5Options options) : IMT5RetryPolicy
{
    public bool ShouldRetry(int attempt, Exception exception) => attempt < options.ReconnectAttempts && exception is TimeoutException;
    public TimeSpan GetDelay(int attempt) => TimeSpan.FromMilliseconds(Math.Min(1000, 50 * Math.Pow(2, Math.Max(0, attempt))));
}
