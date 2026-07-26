using Microsoft.AspNetCore.Mvc;
using TradeMind.Api.Application;
using TradeMind.Api.Contracts.Common;
using TradeMind.Api.Middleware;
using TradeMind.ExecutionSessions.Application;
using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.Api.Errors;

public sealed class ApiRequestTimeoutException(TimeSpan timeout)
    : TimeoutException($"The API request exceeded its configured timeout of {timeout.TotalSeconds:0} seconds.");

public static class ApiProblemDetails
{
    public static async Task WriteAsync(
        HttpContext context,
        int status,
        string title,
        string detail,
        IReadOnlyCollection<ApiProblemError>? errors = null,
        string? type = null)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Type = type ?? $"https://trademind.local/problems/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        problem.Extensions["correlationId"] = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString();
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        await context.Response.WriteAsJsonAsync(problem, TradeMind.Api.Mapping.ApiJson.Options).ConfigureAwait(false);
    }
}

public static class ApiExceptionHandler
{
    public static async Task WriteAsync(HttpContext context)
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        switch (exception)
        {
            case ApiRequestTimeoutException:
                await ApiProblemDetails.WriteAsync(context, StatusCodes.Status504GatewayTimeout, "Request timeout", "The server could not complete the request within the configured timeout.").ConfigureAwait(false);
                return;
            case ApiRequestValidationException validation:
                await ApiProblemDetails.WriteAsync(context, StatusCodes.Status400BadRequest, "Validation failed", "The request did not satisfy the API contract.", validation.Errors).ConfigureAwait(false);
                return;
            case ApiModuleNotConfiguredException notConfigured:
                await ApiProblemDetails.WriteAsync(context, StatusCodes.Status501NotImplemented, "Module endpoint is not configured", $"The {notConfigured.Module} application facade is not configured for this host.").ConfigureAwait(false);
                return;
            case ExecutionSessionNotConfiguredException:
                await ApiProblemDetails.WriteAsync(context, StatusCodes.Status501NotImplemented, "Execution session persistence is not configured", "Execution session endpoints require PostgreSQL persistence configuration.").ConfigureAwait(false);
                return;
            case ExecutionSessionNotFoundException:
                await ApiProblemDetails.WriteAsync(context, StatusCodes.Status404NotFound, "Execution session not found", "The requested execution session does not exist.").ConfigureAwait(false);
                return;
            case ExecutionSessionConcurrencyException concurrency:
                await ApiProblemDetails.WriteAsync(context, StatusCodes.Status409Conflict, "Execution session concurrency conflict", concurrency.Message).ConfigureAwait(false);
                return;
            case ExecutionSessionTransitionException transition:
                await ApiProblemDetails.WriteAsync(context, StatusCodes.Status422UnprocessableEntity, "Invalid execution session transition", transition.Message).ConfigureAwait(false);
                return;
            case ExecutionSessionDomainException transition:
                await ApiProblemDetails.WriteAsync(context, StatusCodes.Status422UnprocessableEntity, "Invalid execution session transition", transition.Message).ConfigureAwait(false);
                return;
            case OperationCanceledException:
                await ApiProblemDetails.WriteAsync(context, StatusCodes.Status408RequestTimeout, "Request cancelled", "The operation was cancelled before a response was completed.").ConfigureAwait(false);
                return;
            case ArgumentException argument:
                await ApiProblemDetails.WriteAsync(context, StatusCodes.Status400BadRequest, "Invalid request", argument.Message).ConfigureAwait(false);
                return;
            default:
                await ApiProblemDetails.WriteAsync(context, StatusCodes.Status500InternalServerError, "Internal server error", "The server could not complete the request.").ConfigureAwait(false);
                return;
        }
    }
}
