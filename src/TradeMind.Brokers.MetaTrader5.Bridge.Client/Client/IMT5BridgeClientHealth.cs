namespace TradeMind.Brokers.MetaTrader5.Bridge.Client.Client;

public interface IMT5BridgeClientHealth
{
    MT5BridgeClientState State { get; }
    Task<MT5BridgeClientHealth> GetHealthAsync(CancellationToken cancellationToken);
    Task HeartbeatAsync(CancellationToken cancellationToken);
}
