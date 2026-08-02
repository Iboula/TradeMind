namespace TradeMind.Brokers.MetaTrader5.Bridge;

public interface IMT5Bridge
{
    string BridgeVersion { get; }
    Task OpenAsync(CancellationToken cancellationToken);
    Task AuthenticateAsync(CancellationToken cancellationToken);
    Task CloseAsync(CancellationToken cancellationToken);
    Task<string> SendAsync(string payload, CancellationToken cancellationToken);
}
