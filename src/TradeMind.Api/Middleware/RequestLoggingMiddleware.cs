using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TradeMind.Api.Middleware;

namespace TradeMind.Api.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger,
    TimeProvider timeProvider)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = timeProvider.GetTimestamp();
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString();
        logger.LogInformation(
            "HTTP request started. Method={Method}, Path={Path}, CorrelationId={CorrelationId}, Endpoint={Endpoint}",
            context.Request.Method,
            context.Request.Path,
            correlationId,
            context.GetEndpoint()?.DisplayName);
        try
        {
            await next(context).ConfigureAwait(false);
            logger.LogInformation(
                "HTTP request completed. Method={Method}, Path={Path}, StatusCode={StatusCode}, DurationMs={DurationMs}, CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                timeProvider.GetElapsedTime(startedAt).TotalMilliseconds,
                correlationId);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("HTTP request cancelled. Method={Method}, Path={Path}, CorrelationId={CorrelationId}", context.Request.Method, context.Request.Path, correlationId);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "HTTP request failed. Method={Method}, Path={Path}, CorrelationId={CorrelationId}", context.Request.Method, context.Request.Path, correlationId);
            throw;
        }
    }
}

public static class IdempotencyMetadata
{
    public sealed class Required;
}
