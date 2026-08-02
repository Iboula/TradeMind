namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Runtime;

public enum MT5BridgeHostState
{
    Starting,
    WaitingForTerminal,
    Ready,
    Degraded,
    Stopping,
    Stopped,
    Faulted
}

public sealed class MT5BridgeHostRuntime
{
    private readonly object sync = new();
    private MT5BridgeHostState state = MT5BridgeHostState.Starting;

    public MT5BridgeHostState State { get { lock (sync) return state; } }
    public bool IsReady => State == MT5BridgeHostState.Ready;

    public void MarkWaitingForTerminal() { lock (sync) { Ensure(MT5BridgeHostState.Starting); state = MT5BridgeHostState.WaitingForTerminal; } }
    public void MarkReady() { lock (sync) { if (state is not (MT5BridgeHostState.WaitingForTerminal or MT5BridgeHostState.Degraded)) throw new InvalidOperationException("The host cannot become ready from its current state."); state = MT5BridgeHostState.Ready; } }
    public void MarkDegraded() { lock (sync) { if (state is MT5BridgeHostState.Stopped or MT5BridgeHostState.Stopping) return; state = MT5BridgeHostState.Degraded; } }
    public void MarkStopping() { lock (sync) { state = MT5BridgeHostState.Stopping; } }
    public void MarkStopped() { lock (sync) { state = MT5BridgeHostState.Stopped; } }
    public void MarkFaulted() { lock (sync) { state = MT5BridgeHostState.Faulted; } }

    private void Ensure(MT5BridgeHostState expected)
    {
        if (state != expected) throw new InvalidOperationException($"Host state must be {expected}.");
    }
}
