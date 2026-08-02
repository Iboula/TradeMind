namespace TradeMind.Brokers.MetaTrader5.Connection;

public interface IMT5Connection : IAsyncDisposable
{
    MT5ConnectionState State { get; }
    MT5ConnectionSnapshot Snapshot { get; }
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
    Task<string> SendRawAsync(string payload, CancellationToken cancellationToken);
}

public interface IMT5ConnectionFactory
{
    IMT5Connection Create();
}
