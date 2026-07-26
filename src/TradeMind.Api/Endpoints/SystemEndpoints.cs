using Microsoft.Extensions.Options;
using TradeMind.Api.Composition;
using TradeMind.Api.Contracts.Common;

namespace TradeMind.Api.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/system/version", (
            IHostEnvironment environment,
            TimeProvider timeProvider) =>
        {
            var assembly = typeof(Program).Assembly;
            var version = assembly.GetName().Version?.ToString() ?? "unknown";
            return Results.Ok(new VersionResponse(
                "v1",
                "TradeMind.Api",
                "v1.0.0-core",
                environment.EnvironmentName,
                version,
                timeProvider.GetUtcNow()));
        })
        .WithName("GetSystemVersion")
        .WithTags("System")
        .Produces<VersionResponse>();

        return endpoints;
    }
}
