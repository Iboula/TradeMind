using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TradeMind.KnowledgeHub.Infrastructure;
using TradeMind.MarketConnectors.Infrastructure;
using TradeMind.ExecutionSessions.Infrastructure.Persistence;

namespace TradeMind.Api.Health;

public sealed class ConfiguredDependencyHealthCheck(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();
        var knowledgeConnection = configuration.GetConnectionString("KnowledgeHub");
        if (!string.IsNullOrWhiteSpace(knowledgeConnection))
        {
            await CheckDatabaseAsync<KnowledgeHubDbContext>("KnowledgeHub", failures, cancellationToken).ConfigureAwait(false);
        }

        var marketConnection = configuration.GetConnectionString("MarketConnectors");
        if (!string.IsNullOrWhiteSpace(marketConnection))
        {
            await CheckDatabaseAsync<MarketConnectorsDbContext>("MarketConnectors", failures, cancellationToken).ConfigureAwait(false);
        }

        var persistenceProvider = configuration["TradeMind:Persistence:Provider"];
        var persistenceConnectionName = configuration["TradeMind:Persistence:ConnectionStringName"] ?? "TradeMind";
        if (string.Equals(persistenceProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(configuration.GetConnectionString(persistenceConnectionName)))
        {
            await CheckDatabaseAsync<ExecutionSessionsDbContext>("ExecutionSessions", failures, cancellationToken).ConfigureAwait(false);
        }

        return failures.Count == 0
            ? HealthCheckResult.Healthy("Configured dependencies are available or not required.")
            : HealthCheckResult.Unhealthy("One or more configured dependencies are unavailable.", data: new Dictionary<string, object>
            {
                ["failures"] = failures.AsReadOnly()
            });
    }

    private async Task CheckDatabaseAsync<TContext>(
        string name,
        ICollection<string> failures,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetService<TContext>();
            if (context is null || !await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                failures.Add(name);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            failures.Add(name);
        }
    }
}
