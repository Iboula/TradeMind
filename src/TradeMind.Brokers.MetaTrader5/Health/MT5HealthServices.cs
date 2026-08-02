using System.Diagnostics;
using TradeMind.Brokers.MetaTrader5.Connection;
using TradeMind.Brokers.MetaTrader5.Protocol;

namespace TradeMind.Brokers.MetaTrader5.Health;

public sealed class MT5HeartbeatService(IMT5Protocol protocol, TimeProvider timeProvider) : IMT5HeartbeatService
{
    public async Task<MT5HeartbeatResult> CheckAsync(IMT5Connection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await protocol.ExecuteAsync(connection, new MT5Request("heartbeat"), cancellationToken).ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(started);
            if (!response.Success) return new MT5HeartbeatResult(false, timeProvider.GetUtcNow(), elapsed, "The MT5 heartbeat was rejected.");
            if (connection is MT5Connection concrete) concrete.RecordHeartbeat(timeProvider.GetUtcNow(), elapsed);
            return new MT5HeartbeatResult(true, timeProvider.GetUtcNow(), elapsed, "Heartbeat acknowledged.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new MT5HeartbeatResult(false, timeProvider.GetUtcNow(), Stopwatch.GetElapsedTime(started), "The MT5 heartbeat failed.", exception);
        }
    }
}

public sealed class MT5HealthService(IMT5HeartbeatService heartbeatService) : IMT5HealthService
{
    public async Task<MT5HealthResult> CheckAsync(IMT5Connection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        try
        {
            if (connection.State is MT5ConnectionState.Disconnected or MT5ConnectionState.Faulted or MT5ConnectionState.Reconnecting)
                await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);
            var heartbeat = await heartbeatService.CheckAsync(connection, cancellationToken).ConfigureAwait(false);
            var snapshot = connection.Snapshot;
            return new MT5HealthResult(heartbeat.Healthy, snapshot, heartbeat.Message, heartbeat.Error);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new MT5HealthResult(false, connection.Snapshot, "The MT5 health check failed.", exception);
        }
    }
}
