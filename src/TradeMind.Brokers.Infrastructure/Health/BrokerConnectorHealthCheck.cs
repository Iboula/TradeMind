using Microsoft.Extensions.Diagnostics.HealthChecks;
using TradeMind.Brokers.Application.Abstractions;

namespace TradeMind.Brokers.Infrastructure.Health;

public sealed class BrokerConnectorHealthCheck(
    IBrokerConnectorRegistry registry,
    IBrokerOrphanPositionDetector? orphanPositionDetector = null,
    IBrokerExecutionSafetyState? safetyState = null) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var descriptors = registry.Descriptors;
        if (descriptors.Count == 0) return HealthCheckResult.Healthy("No broker connectors are configured.");
        var results = new List<HealthCheckResult>();
        foreach (var descriptor in descriptors)
        {
            if (!registry.TryGet(descriptor.ConnectorId, out var connector) || connector is null)
            {
                results.Add(HealthCheckResult.Unhealthy($"Connector '{descriptor.ConnectorId}' is unavailable."));
                continue;
            }

            var health = await connector.GetHealthAsync(new TradeMind.Brokers.Domain.BrokerExecutionContext(false, null, null, null, null, [], null, null), cancellationToken).ConfigureAwait(false);
            results.Add(health.Health.Status switch
            {
                TradeMind.Brokers.Domain.BrokerHealthStatus.Healthy => HealthCheckResult.Healthy(health.Health.Message),
                TradeMind.Brokers.Domain.BrokerHealthStatus.Degraded => HealthCheckResult.Degraded(health.Health.Message),
                _ => HealthCheckResult.Unhealthy(health.Health.Message)
            });

            if (orphanPositionDetector is not null && safetyState is not null)
            {
                try
                {
                    var readContext = new TradeMind.Brokers.Domain.BrokerExecutionContext(false, null, null, null, null, [], null, null);
                    var accounts = await connector.GetAccountsAsync(readContext, cancellationToken).ConfigureAwait(false);
                    foreach (var account in accounts)
                    {
                        var report = await orphanPositionDetector.DetectAsync(readContext, descriptor.ConnectorId, account.AccountId, [], cancellationToken).ConfigureAwait(false);
                        if (report.HasUnsafePositions)
                        {
                            safetyState.Block("BROKER_POSITION_REVIEW_REQUIRED", "Broker positions require explicit reconciliation before new writes.");
                            results.Add(HealthCheckResult.Unhealthy("Broker positions require explicit operator review."));
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    safetyState.MarkDegraded("BROKER_POSITION_CHECK_FAILED", "Broker position readiness could not be verified.");
                    results.Add(HealthCheckResult.Degraded("Broker position readiness could not be verified."));
                }
            }
        }

        if (results.Any(item => item.Status == HealthStatus.Unhealthy)) return HealthCheckResult.Unhealthy("A configured broker connector is unhealthy.");
        if (results.Any(item => item.Status == HealthStatus.Degraded)) return HealthCheckResult.Degraded("A broker connector is degraded.");
        return HealthCheckResult.Healthy("Configured broker connectors are healthy.");
    }
}
