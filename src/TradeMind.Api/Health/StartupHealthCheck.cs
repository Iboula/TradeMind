using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TradeMind.Api.Health;

public sealed class StartupHealthCheck(StartupHealthCheckState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(state.IsReady
            ? HealthCheckResult.Healthy("Application startup completed.")
            : HealthCheckResult.Unhealthy("Application startup is still in progress."));
}

public sealed class StartupHealthCheckState
{
    private int _ready;
    public bool IsReady => Volatile.Read(ref _ready) == 1;
    public void MarkReady() => Interlocked.Exchange(ref _ready, 1);
}
