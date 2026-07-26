using Microsoft.AspNetCore.Mvc;
using TradeMind.Api.Application;
using TradeMind.Api.Contracts.Common;
using TradeMind.Api.Contracts.ExecutionSessions;
using TradeMind.Api.Middleware;
using TradeMind.ExecutionSessions.Application.DTOs;

namespace TradeMind.Api.Endpoints;

public static class ExecutionSessionEndpoints
{
    public static void MapExecutionSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/execution-sessions").WithTags("Execution Sessions");

        group.MapPost("/", async (CreateExecutionSessionApiRequest? request, HttpContext context, IExecutionSessionApiApplication application, CancellationToken cancellationToken) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            try
            {
                var result = await application.StartAsync(request with
                {
                    CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? EndpointHelpers.CorrelationId(context) : request.CorrelationId
                }, cancellationToken).ConfigureAwait(false);
                return Results.Created($"/api/v1/execution-sessions/{result.Id}", Response(context, result));
            }
            catch (Exception exception) when (exception is ApiRequestValidationException or ArgumentException)
            {
                return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", exception.Message);
            }
        }).WithMetadata(new IdempotencyMetadata.Required());

        group.MapGet("/{sessionId:guid}", async (Guid sessionId, HttpContext context, IExecutionSessionApiApplication application, CancellationToken cancellationToken) =>
        {
            var result = await application.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(Response(context, result));
        });

        group.MapGet("/{sessionId:guid}/timeline", async (Guid sessionId, HttpContext context, IExecutionSessionApiApplication application, CancellationToken cancellationToken) =>
        {
            var timeline = await application.GetTimelineAsync(sessionId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { items = timeline, metadata = Metadata(context) });
        });

        group.MapGet("/{sessionId:guid}/replay-manifest", async (Guid sessionId, HttpContext context, IExecutionSessionApiApplication application, CancellationToken cancellationToken) =>
        {
            var manifest = await application.GetReplayManifestAsync(sessionId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(manifest with { Metadata = Metadata(context) });
        });

        group.MapGet("/", async ([AsParameters] ExecutionSessionSearchApiQuery query, HttpContext context, IExecutionSessionApiApplication application, CancellationToken cancellationToken) =>
        {
            var result = await application.SearchAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new
            {
                items = result.Items.Select(MapSearchItem).ToArray(),
                page = result.Page,
                pageSize = result.PageSize,
                totalCount = result.TotalCount,
                metadata = Metadata(context)
            });
        });

        group.MapPost("/{sessionId:guid}/artifacts", async (Guid sessionId, LinkExecutionArtifactApiRequest? request, IExecutionSessionApiApplication application, CancellationToken cancellationToken) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            var result = await application.LinkArtifactAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        });

        group.MapPost("/{sessionId:guid}/stages", async (Guid sessionId, AdvanceExecutionStageApiRequest? request, IExecutionSessionApiApplication application, CancellationToken cancellationToken) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            var result = await application.AdvanceStageAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        });

        group.MapPost("/{sessionId:guid}/complete", async (Guid sessionId, ExecutionSessionMutationApiRequest? request, HttpContext context, IExecutionSessionApiApplication application, CancellationToken cancellationToken) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            var result = await application.CompleteAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
            return Results.Ok(Response(context, result));
        });

        group.MapPost("/{sessionId:guid}/fail", async (Guid sessionId, FailExecutionSessionApiRequest? request, IExecutionSessionApiApplication application, CancellationToken cancellationToken) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            var result = await application.FailAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        });

        group.MapPost("/{sessionId:guid}/cancel", async (Guid sessionId, ExecutionSessionMutationApiRequest? request, HttpContext context, IExecutionSessionApiApplication application, CancellationToken cancellationToken) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            var result = await application.CancelAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
            return Results.Ok(Response(context, result));
        }).WithMetadata(new IdempotencyMetadata.Required());
    }

    private static ExecutionSessionApiResponse Response(HttpContext context, ExecutionSessionApiResource resource) =>
        new(resource, Metadata(context));

    private static ApiResponseMetadata Metadata(HttpContext context) => new(
        context.TraceIdentifier,
        EndpointHelpers.CorrelationId(context),
        context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(),
        "1.0",
        [],
        [],
        []);

    private static ExecutionSessionSearchApiItem MapSearchItem(ExecutionSessionSearchItem item) => new(
        item.Id, item.CorrelationId, item.Instrument, item.Timeframe, item.Status, item.CurrentStage, item.TriggerType,
        item.StartedAtUtc, item.UpdatedAtUtc, item.ArtifactCount);
}
