using System.Text.Json;
using TradeMind.Brokers.MetaTrader5.Protocol;

namespace TradeMind.Brokers.MetaTrader5.Serialization;

public sealed class MT5JsonSerializer : IMT5Serializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string SerializeRequest(MT5Request request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return JsonSerializer.Serialize(new WireRequest(request.Command, request.Fields), Options);
    }

    public MT5Request DeserializeRequest(string payload)
    {
        var wire = JsonSerializer.Deserialize<WireRequest>(payload, Options) ?? throw new FormatException("The MT5 request payload is empty.");
        return new MT5Request(wire.Command, wire.Fields);
    }

    public string SerializeResponse(MT5Response response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return JsonSerializer.Serialize(new WireResponse(response.Success, response.Code, response.Message, response.Fields), Options);
    }

    public MT5Response DeserializeResponse(string payload)
    {
        var wire = JsonSerializer.Deserialize<WireResponse>(payload, Options) ?? throw new FormatException("The MT5 response payload is empty.");
        return new MT5Response(wire.Success, wire.Code, wire.Message, wire.Fields);
    }

    internal static string SerializePayload<T>(T payload) => JsonSerializer.Serialize(payload, Options);

    internal static T DeserializePayload<T>(string payload) => JsonSerializer.Deserialize<T>(payload, Options)
        ?? throw new FormatException($"The MT5 payload could not be deserialized as {typeof(T).Name}.");

    internal static IReadOnlyList<T> DeserializePayloads<T>(string payload) => DeserializePayload<List<T>>(payload);

    private sealed record WireRequest(string Command, IReadOnlyDictionary<string, string> Fields);
    private sealed record WireResponse(bool Success, string Code, string Message, IReadOnlyDictionary<string, string> Fields);
}
