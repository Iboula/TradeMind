using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Configuration;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Protocol;

namespace TradeMind.Brokers.MetaTrader5.TerminalBridge.Terminal;

public sealed class InMemoryTerminalAgentSession(IOptions<TerminalBridgeOptions> options) : ITerminalAgentSession
{
    private readonly TerminalBridgeOptions configuration = options.Value;
    private readonly Channel<PendingAgentCommand> queue = Channel.CreateUnbounded<PendingAgentCommand>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false, AllowSynchronousContinuations = false });
    private readonly ConcurrentDictionary<string, TaskCompletionSource<AgentCommandResult>> pending = new(StringComparer.Ordinal);
    private readonly object sync = new();
    private TerminalAgentSnapshot snapshot = new(false, false, "Unknown", false, true, string.Empty, string.Empty, 0, string.Empty, string.Empty, [], null, 0);

    public TerminalAgentSnapshot Snapshot { get { lock (sync) return snapshot; } }

    public void Update(AgentPollRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (sync)
        {
            var changedConnection = !snapshot.Connected;
            snapshot = new(
                true,
                request.DemoAccount,
                request.AccountEnvironment?.Trim() ?? "Unknown",
                request.TradingEnabled,
                request.ReadOnly,
                request.ProtocolVersion?.Trim() ?? string.Empty,
                request.TerminalVersion?.Trim() ?? string.Empty,
                request.TerminalBuild,
                request.TerminalArchitecture?.Trim() ?? string.Empty,
                request.AccountId?.Trim() ?? string.Empty,
                request.Capabilities?.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() ?? [],
                now,
                changedConnection ? snapshot.ReconnectCount : snapshot.ReconnectCount);
        }
    }

    public PendingAgentCommand? Dequeue() => queue.Reader.TryRead(out var command) ? command : null;

    public async Task<AgentCommandResult> EnqueueAsync(string command, IReadOnlyDictionary<string, string> fields, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<AgentCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(id, completion)) return Failure("UNKNOWN", "The terminal command could not be queued.");

        try
        {
            await queue.Writer.WriteAsync(new PendingAgentCommand(id, command, fields), cancellationToken).ConfigureAwait(false);
            return await completion.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("TIMEOUT", "The terminal command timed out.");
        }
        catch (TimeoutException)
        {
            return Failure("TIMEOUT", "The terminal command timed out.");
        }
        catch (OperationCanceledException)
        {
            return Failure("CANCELLED", "The terminal command was cancelled.");
        }
        finally
        {
            pending.TryRemove(id, out _);
        }
    }

    public bool Complete(string commandId, AgentCommandResult result)
    {
        if (!pending.TryGetValue(commandId, out var completion)) return false;
        return completion.TrySetResult(result);
    }

    public void Disconnect(DateTimeOffset now)
    {
        lock (sync) snapshot = snapshot with { Connected = false, LastHeartbeatUtc = now };
    }

    private static AgentCommandResult Failure(string code, string message) => new(false, code, message, new Dictionary<string, string>());
}
