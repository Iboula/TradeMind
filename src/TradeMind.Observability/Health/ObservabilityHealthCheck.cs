using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TradeMind.Observability.OpenTelemetry;

namespace TradeMind.Observability.Health;

public sealed class ObservabilityHealthCheck(IOptions<OpenTelemetryOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var value = options.Value;
        var details = new Dictionary<string, object>
        {
            ["enabled"] = value.Enabled,
            ["tracing"] = value.Enabled && value.Tracing.Enabled,
            ["metrics"] = value.Enabled && value.Metrics.Enabled
        };
        return Task.FromResult(value.Enabled
            ? HealthCheckResult.Healthy("Observability configuration is valid.", details)
            : HealthCheckResult.Healthy("Observability is disabled.", details));
    }
}
