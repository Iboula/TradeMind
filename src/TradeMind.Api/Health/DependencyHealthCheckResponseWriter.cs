using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TradeMind.Api.Health;

public static class DependencyHealthCheckResponseWriter
{
    public static async Task WriteSafeAsync(HttpContext context, HealthReport report, string check, TimeProvider timeProvider)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = report.Status == HealthStatus.Healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
        await JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            status = report.Status.ToString(),
            check,
            checkedAtUtc = timeProvider.GetUtcNow()
        }, cancellationToken: context.RequestAborted).ConfigureAwait(false);
    }
}
