using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TradeMind.Brokers.MetaTrader5.Bridge;
using TradeMind.Brokers.MetaTrader5.Configuration;

namespace TradeMind.Brokers.MetaTrader5.Connection;

public sealed class MT5Connection(
    MT5Options options,
    IMT5Bridge bridge,
    TimeProvider timeProvider,
    ILogger<MT5Connection> logger) : IMT5Connection
{
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly object stateLock = new();
    private MT5ConnectionState state = MT5ConnectionState.Disconnected;
    private TimeSpan latency = TimeSpan.Zero;
    private bool heartbeat;
    private int reconnectCount;
    private DateTimeOffset? lastHeartbeat;
    private DateTimeOffset? lastReconnect;
    private bool disposed;

    public MT5ConnectionState State
    {
        get { lock (stateLock) return state; }
    }

    public MT5ConnectionSnapshot Snapshot
    {
        get
        {
            lock (stateLock)
            {
                return new MT5ConnectionSnapshot(bridge.BridgeVersion, "1.0", "simulation-terminal", latency, heartbeat, reconnectCount, state, lastHeartbeat, lastReconnect);
            }
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var current = State;
            if (current == MT5ConnectionState.Connected) return;
            if (current == MT5ConnectionState.Closed) throw new InvalidOperationException("The MT5 connection is closed.");
            if (current is MT5ConnectionState.Faulted or MT5ConnectionState.Reconnecting)
            {
                SetState(MT5ConnectionState.Reconnecting);
                lock (stateLock)
                {
                    reconnectCount++;
                    lastReconnect = timeProvider.GetUtcNow();
                }
            }

            SetState(MT5ConnectionState.Connecting);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.Timeout);
            await bridge.OpenAsync(timeout.Token).ConfigureAwait(false);
            SetState(MT5ConnectionState.Authenticating);
            await bridge.AuthenticateAsync(timeout.Token).ConfigureAwait(false);
            SetState(MT5ConnectionState.Connected);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            SetState(MT5ConnectionState.Faulted);
            throw new TimeoutException("The MT5 connection timed out.");
        }
        catch (Exception exception)
        {
            SetState(MT5ConnectionState.Faulted);
            logger.LogWarning(exception, "The MT5 connection failed while entering the connected state.");
            throw;
        }
        finally
        {
            lifecycle.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State is MT5ConnectionState.Closed or MT5ConnectionState.Disconnected) return;
            await bridge.CloseAsync(cancellationToken).ConfigureAwait(false);
            SetState(MT5ConnectionState.Disconnected);
        }
        finally
        {
            lifecycle.Release();
        }
    }

    public async Task<string> SendRawAsync(string payload, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ThrowIfDisposed();
        if (State != MT5ConnectionState.Connected) throw new InvalidOperationException("The MT5 connection is not connected.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await bridge.SendAsync(payload, timeout.Token).ConfigureAwait(false);
            lock (stateLock) latency = Stopwatch.GetElapsedTime(started);
            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            SetState(MT5ConnectionState.Faulted);
            throw new TimeoutException("The MT5 request timed out.");
        }
    }

    public void RecordHeartbeat(DateTimeOffset checkedAtUtc, TimeSpan elapsed)
    {
        if (checkedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("The heartbeat timestamp must be UTC.", nameof(checkedAtUtc));
        lock (stateLock)
        {
            heartbeat = true;
            lastHeartbeat = checkedAtUtc;
            latency = elapsed;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            await bridge.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            SetState(MT5ConnectionState.Closed);
            lifecycle.Dispose();
        }
    }

    private void SetState(MT5ConnectionState value)
    {
        lock (stateLock) state = value;
    }

    private void ThrowIfDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(MT5Connection));
    }
}

public sealed class MT5ConnectionFactory(MT5Options options, IMT5Bridge bridge, TimeProvider timeProvider, ILoggerFactory loggerFactory) : IMT5ConnectionFactory
{
    public IMT5Connection Create() => new MT5Connection(options, bridge, timeProvider, loggerFactory.CreateLogger<MT5Connection>());
}
