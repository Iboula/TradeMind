using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Client.Client;

public sealed class MT5BridgeException(BridgeError error) : Exception(error?.Message)
{
    public BridgeError Error { get; } = error ?? throw new ArgumentNullException(nameof(error));
}
