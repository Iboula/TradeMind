using TradeMind.Brokers.MetaTrader5.Connection;
using TradeMind.Brokers.MetaTrader5.Serialization;

namespace TradeMind.Brokers.MetaTrader5.Protocol;

public sealed class MT5Protocol(IMT5Serializer serializer) : IMT5Protocol
{
    public async Task<MT5Response> ExecuteAsync(IMT5Connection connection, MT5Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);
        var payload = serializer.SerializeRequest(request);
        var response = await connection.SendRawAsync(payload, cancellationToken).ConfigureAwait(false);
        return serializer.DeserializeResponse(response);
    }
}
