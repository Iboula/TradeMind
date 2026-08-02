using TradeMind.Brokers.MetaTrader5.Connection;

namespace TradeMind.Brokers.MetaTrader5.Health;

public interface IMT5HealthService
{
    Task<MT5HealthResult> CheckAsync(IMT5Connection connection, CancellationToken cancellationToken);
}

public interface IMT5HeartbeatService
{
    Task<MT5HeartbeatResult> CheckAsync(IMT5Connection connection, CancellationToken cancellationToken);
}

public sealed record MT5HealthResult(bool Healthy, MT5ConnectionSnapshot Snapshot, string Message, Exception? Error = null);
public sealed record MT5HeartbeatResult(bool Healthy, DateTimeOffset CheckedAtUtc, TimeSpan Latency, string Message, Exception? Error = null);
