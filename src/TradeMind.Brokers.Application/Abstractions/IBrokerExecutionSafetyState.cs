using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Abstractions;

public enum BrokerExecutionReadiness
{
    Ready,
    Degraded,
    Blocked
}

public sealed record BrokerExecutionSafetySnapshot(
    BrokerExecutionReadiness Readiness,
    string? Code,
    string? Reason)
{
    public bool WritesAllowed => Readiness == BrokerExecutionReadiness.Ready;
}

public interface IBrokerExecutionSafetyState
{
    BrokerExecutionSafetySnapshot Snapshot { get; }
    void MarkDegraded(string code, string reason);
    void Block(string code, string reason);
    void Restore();
}

public sealed class InMemoryBrokerExecutionSafetyState : IBrokerExecutionSafetyState
{
    private readonly object sync = new();
    private BrokerExecutionSafetySnapshot snapshot = new(BrokerExecutionReadiness.Ready, null, null);

    public BrokerExecutionSafetySnapshot Snapshot
    {
        get { lock (sync) return snapshot; }
    }

    public void MarkDegraded(string code, string reason) => Set(BrokerExecutionReadiness.Degraded, code, reason);

    public void Block(string code, string reason) => Set(BrokerExecutionReadiness.Blocked, code, reason);

    public void Restore() => Set(BrokerExecutionReadiness.Ready, null, null);

    private void Set(BrokerExecutionReadiness readiness, string? code, string? reason)
    {
        lock (sync)
        {
            if (snapshot.Readiness == BrokerExecutionReadiness.Blocked && readiness != BrokerExecutionReadiness.Ready) return;
            snapshot = new(readiness, code, reason);
        }
    }
}
