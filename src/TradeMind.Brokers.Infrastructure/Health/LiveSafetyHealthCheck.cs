using Microsoft.Extensions.Diagnostics.HealthChecks;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.LiveSafety;

namespace TradeMind.Brokers.Infrastructure.Health;

public sealed class LiveSafetyHealthCheck(
    ILiveSafetyReadiness readiness,
    IBrokerExecutionSafetyState safetyState) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = readiness.Snapshot;
        var data = new Dictionary<string, object>
        {
            ["persistence"] = snapshot.Persistence,
            ["idempotency"] = snapshot.Idempotency,
            ["execution_lock"] = snapshot.ExecutionLock,
            ["reconciliation"] = snapshot.Reconciliation,
            ["broker"] = snapshot.Broker,
            ["kill_switch"] = snapshot.KillSwitch,
            ["recovery"] = snapshot.Recovery,
            ["orphans"] = snapshot.Orphans,
            ["outbox"] = snapshot.Outbox,
            ["identity"] = snapshot.Identity,
            ["execution_readiness"] = safetyState.Snapshot.Readiness.ToString()
        };
        if (safetyState.Snapshot.Readiness == BrokerExecutionReadiness.Blocked)
            return Task.FromResult(HealthCheckResult.Unhealthy("Broker execution is blocked pending safety review.", data: data));
        if (snapshot.ExecutionBlocked)
            return Task.FromResult(HealthCheckResult.Degraded("Live execution dependencies are not all ready.", data: data));
        return Task.FromResult(HealthCheckResult.Healthy("Live safety dependencies are available; Live remains policy-controlled.", data));
    }
}
