using TradeMind.Brokers.MetaTrader5.Connection;

namespace TradeMind.Brokers.MetaTrader5.Protocol;

public sealed record MT5Request
{
    public MT5Request(string command, IReadOnlyDictionary<string, string>? fields = null)
    {
        Command = ValidateCommand(command);
        Fields = new Dictionary<string, string>(fields ?? new Dictionary<string, string>(), StringComparer.Ordinal);
    }

    public string Command { get; }
    public IReadOnlyDictionary<string, string> Fields { get; }

    private static string ValidateCommand(string command) => string.IsNullOrWhiteSpace(command)
        ? throw new ArgumentException("A command is required.", nameof(command))
        : command.Trim();
}

public sealed record MT5Response
{
    public MT5Response(bool success, string code, string message, IReadOnlyDictionary<string, string>? fields = null)
    {
        Success = success;
        Code = string.IsNullOrWhiteSpace(code) ? throw new ArgumentException("A response code is required.", nameof(code)) : code.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? throw new ArgumentException("A response message is required.", nameof(message)) : message.Trim();
        Fields = new Dictionary<string, string>(fields ?? new Dictionary<string, string>(), StringComparer.Ordinal);
    }

    public bool Success { get; }
    public string Code { get; }
    public string Message { get; }
    public IReadOnlyDictionary<string, string> Fields { get; }
}

public interface IMT5Protocol
{
    Task<MT5Response> ExecuteAsync(IMT5Connection connection, MT5Request request, CancellationToken cancellationToken);
}

public interface IMT5Serializer
{
    string SerializeRequest(MT5Request request);
    MT5Request DeserializeRequest(string payload);
    string SerializeResponse(MT5Response response);
    MT5Response DeserializeResponse(string payload);
}
