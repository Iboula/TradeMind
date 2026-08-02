namespace TradeMind.Brokers.MetaTrader5.Retry;

public interface IMT5ReconnectPolicy
{
    bool ShouldReconnect(int attempt, Exception exception);
    TimeSpan GetDelay(int attempt);
}

public interface IMT5RetryPolicy
{
    bool ShouldRetry(int attempt, Exception exception);
    TimeSpan GetDelay(int attempt);
}
