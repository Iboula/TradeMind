namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;

internal interface IMT5TerminalGateway
{
    string TerminalVersion { get; }
    bool IsAvailable { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task<TerminalCommandResult> ExecuteAsync(TerminalCommand command, CancellationToken cancellationToken);
}

internal sealed record TerminalCommand(string Command, IReadOnlyDictionary<string, string> Fields);

internal sealed record TerminalCommandResult(bool Success, string Code, string Message, IReadOnlyDictionary<string, string> Fields);
