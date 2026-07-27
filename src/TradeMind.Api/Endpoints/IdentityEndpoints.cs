using TradeMind.Api.Application;
using TradeMind.Api.Authorization;
using TradeMind.Api.Contracts.Common;
using TradeMind.Api.Contracts.Identity;
using TradeMind.Api.Middleware;
using TradeMind.Identity.Application;
using TradeMind.Identity.Application.DTOs;
using TradeMind.Identity.Application.Abstractions;

namespace TradeMind.Api.Endpoints;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/identity/me", (ICurrentActor actor, HttpContext context) =>
            Results.Ok(new IdentityMeResponse(IdentityDtoMapper.ToDto(actor.Identity), Metadata(context))))
            .WithName("GetCurrentIdentity").WithTags("Identity");

        var keys = endpoints.MapGroup("/api/v1/identity/api-keys").WithTags("API Keys");
        keys.MapPost("/", async (CreateApiKeyRequest? request, HttpContext context, IIdentityApplicationService service, IHostEnvironment environment, CancellationToken token) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            var result = await service.CreateApiKeyAsync(request, environment.EnvironmentName, token).ConfigureAwait(false);
            return Results.Created("/api/v1/identity/api-keys/" + result.Key.Id, new IdentityApiKeyResponse(result, Metadata(context)));
        }).RequireAuthorization(IdentityPolicies.ApiKeysCreate);

        keys.MapGet("/", async (HttpContext context, IIdentityApplicationService service, CancellationToken token) =>
            Results.Ok(new IdentityApiKeyListResponse(await service.SearchApiKeysAsync(token).ConfigureAwait(false), Metadata(context))))
            .RequireAuthorization(IdentityPolicies.ApiKeysRead);

        keys.MapGet("/{id:guid}", async (Guid id, HttpContext context, IIdentityApplicationService service, CancellationToken token) =>
            Results.Ok(new { data = await service.GetApiKeyAsync(id, token).ConfigureAwait(false), metadata = Metadata(context) }))
            .RequireAuthorization(IdentityPolicies.ApiKeysRead);

        keys.MapPost("/{id:guid}/rotate", async (Guid id, RotateApiKeyRequest? request, HttpContext context, IIdentityApplicationService service, IHostEnvironment environment, CancellationToken token) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            var result = await service.RotateApiKeyAsync(id, request, environment.EnvironmentName, token).ConfigureAwait(false);
            return Results.Ok(new IdentityApiKeyResponse(result, Metadata(context)));
        }).RequireAuthorization(IdentityPolicies.ApiKeysRotate);

        keys.MapPost("/{id:guid}/revoke", async (Guid id, RevokeApiKeyRequest? request, HttpContext context, IIdentityApplicationService service, CancellationToken token) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            return Results.Ok(new { data = await service.RevokeApiKeyAsync(id, request, token).ConfigureAwait(false), metadata = Metadata(context) });
        }).RequireAuthorization(IdentityPolicies.ApiKeysRevoke);

        keys.MapPost("/{id:guid}/disable", async (Guid id, ApiKeyMutationRequest? request, HttpContext context, IIdentityApplicationService service, CancellationToken token) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            return Results.Ok(new { data = await service.DisableApiKeyAsync(id, request, token).ConfigureAwait(false), metadata = Metadata(context) });
        }).RequireAuthorization(IdentityPolicies.ApiKeysRevoke);

        keys.MapPost("/{id:guid}/enable", async (Guid id, ApiKeyMutationRequest? request, HttpContext context, IIdentityApplicationService service, CancellationToken token) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            return Results.Ok(new { data = await service.EnableApiKeyAsync(id, request, token).ConfigureAwait(false), metadata = Metadata(context) });
        }).RequireAuthorization(IdentityPolicies.ApiKeysRevoke);
        return endpoints;
    }

    private static ApiResponseMetadata Metadata(HttpContext context) => new(
        context.TraceIdentifier, EndpointHelpers.CorrelationId(context), context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(), "1.0", [], [], []);
}
