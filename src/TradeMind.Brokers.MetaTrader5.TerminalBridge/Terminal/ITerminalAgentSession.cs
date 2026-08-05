using TradeMind.Brokers.MetaTrader5.TerminalBridge.Protocol;

namespace TradeMind.Brokers.MetaTrader5.TerminalBridge.Terminal;

public interface ITerminalAgentSession
{
    TerminalAgentSnapshot Snapshot { get; }
    void Update(AgentPollRequest request, DateTimeOffset now);
    PendingAgentCommand? Dequeue();
    Task<AgentCommandResult> EnqueueAsync(string command, IReadOnlyDictionary<string, string> fields, TimeSpan timeout, CancellationToken cancellationToken);
    bool Complete(string commandId, AgentCommandResult result);
    void Disconnect(DateTimeOffset now);
}
