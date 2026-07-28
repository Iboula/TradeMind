using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TradeMind.Api.Health;

public sealed class ReadinessHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Healthy("Readiness checks are registered."));
}
