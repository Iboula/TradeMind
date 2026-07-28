using Microsoft.Extensions.Diagnostics.HealthChecks;
using TradeMind.Api.Contracts.Common;
using TradeMind.Api.Health;

namespace TradeMind.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", (TimeProvider timeProvider) =>
            Results.Ok(new HealthResponse("Healthy", "live", timeProvider.GetUtcNow())))
            .WithName("LiveHealth")
            .WithTags("Health")
            .Produces<HealthResponse>();

        endpoints.MapGet("/health/ready", async (
            HealthCheckService healthChecks,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var report = await healthChecks.CheckHealthAsync(
                check => check.Tags.Contains("ready", StringComparer.Ordinal),
                cancellationToken).ConfigureAwait(false);
            var status = report.Status.ToString();
            var result = new HealthResponse(status, "ready", timeProvider.GetUtcNow());
            return Results.Json(result, statusCode: report.Status == HealthStatus.Healthy
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable);
        })
            .WithName("ReadyHealth")
            .WithTags("Health")
            .Produces<HealthResponse>();

        endpoints.MapGet("/health/startup", async (
            HealthCheckService healthChecks,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var report = await healthChecks.CheckHealthAsync(
                check => check.Tags.Contains("startup", StringComparer.Ordinal),
                cancellationToken).ConfigureAwait(false);
            var statusCode = report.Status == HealthStatus.Healthy
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable;
            return Results.Json(new HealthResponse(report.Status.ToString(), "startup", timeProvider.GetUtcNow()), statusCode: statusCode);
        })
            .WithName("StartupHealth")
            .WithTags("Health")
            .Produces<HealthResponse>();

        return endpoints;
    }
}
