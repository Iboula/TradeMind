using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TradeMind.Api.Application;
using TradeMind.Api.Contracts.Common;
using TradeMind.Api.Contracts.MarketContext;
using TradeMind.Api.Mapping;
using TradeMind.Api.Middleware;

namespace TradeMind.Api.Endpoints;

internal static class EndpointHelpers
{
    public static IResult? ValidateSchema(int schemaVersion, JsonElement payload)
    {
        if (schemaVersion != 1)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Unsupported schema version",
                "Schema version 1 is required.",
                [new ApiProblemError("UNSUPPORTED_SCHEMA_VERSION", "Schema version 1 is required.", "schemaVersion")]);
        }

        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid request",
                "A JSON payload is required.",
                [new ApiProblemError("REQUIRED", "Payload is required.", "payload")]);
        }

        return null;
    }

    public static IResult? ValidateMarketContextRequest(BuildMarketContextApiRequest? request)
    {
        if (request is null)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid request",
                "A JSON request is required.",
                [new ApiProblemError("REQUIRED", "Request body is required.", null)]);
        }

        var errors = new List<ApiProblemError>();
        if (request.SchemaVersion != 1) errors.Add(new("UNSUPPORTED_SCHEMA_VERSION", "Schema version 1 is required.", "schemaVersion"));
        if (string.IsNullOrWhiteSpace(request.UserId)) errors.Add(new("REQUIRED", "UserId is required.", "userId"));
        if (string.IsNullOrWhiteSpace(request.SessionId)) errors.Add(new("REQUIRED", "SessionId is required.", "sessionId"));
        if (string.IsNullOrWhiteSpace(request.Instrument)) errors.Add(new("REQUIRED", "Instrument is required.", "instrument"));
        if (string.IsNullOrWhiteSpace(request.Timeframe)) errors.Add(new("REQUIRED", "Timeframe is required.", "timeframe"));

        return errors.Count == 0
            ? null
            : Problem(StatusCodes.Status400BadRequest, "Invalid request", "The request contains invalid or missing fields.", errors);
    }

    public static async Task<IResult> ExecuteModuleAsync(
        HttpContext context,
        ITradeMindApiApplication application,
        ApiModule module,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var result = await application.ExecuteAsync(
            module,
            payload,
            CorrelationId(context),
            cancellationToken).ConfigureAwait(false);
        return Results.Ok(ToEnvelope(context, result, context.RequestServices.GetRequiredService<TimeProvider>()));
    }

    public static ApiResponseEnvelope ToEnvelope(
        HttpContext context,
        ApiOperationResult result,
        TimeProvider timeProvider) =>
        new(
            result.Module.ToString(),
            result.Data,
            new ApiResponseMetadata(
                context.TraceIdentifier,
                CorrelationId(context),
                timeProvider.GetUtcNow(),
                result.SchemaVersion,
                result.Warnings,
                result.Blockers,
                result.TraceReferences));

    public static string CorrelationId(HttpContext context) =>
        context.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
        ?? context.TraceIdentifier;

    public static IResult Problem(
        int statusCode,
        string title,
        string detail,
        IReadOnlyCollection<ApiProblemError>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Type = $"https://trademind.local/problems/{statusCode}",
            Title = title,
            Status = statusCode,
            Detail = detail
        };
        if (errors is not null) problem.Extensions["errors"] = errors;
        return Results.Json(problem, ApiJson.Options, contentType: "application/problem+json", statusCode: statusCode);
    }
}
