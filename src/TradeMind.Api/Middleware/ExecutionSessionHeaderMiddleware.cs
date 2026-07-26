using TradeMind.Api.Application;
using TradeMind.Api.Errors;
using TradeMind.ExecutionSessions.Application;

namespace TradeMind.Api.Middleware;

public sealed class ExecutionSessionHeaderMiddleware(
    RequestDelegate next,
    IExecutionSessionApiApplication application,
    ILogger<ExecutionSessionHeaderMiddleware> logger)
{
    public const string HeaderName = "X-Execution-Session-ID";
    public const string ItemKey = "TradeMind.ExecutionSessionId";

    public async Task InvokeAsync(HttpContext context)
    {
        var header = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (!Guid.TryParse(header, out var value) || value == Guid.Empty)
        {
            await ApiProblemDetails.WriteAsync(context, StatusCodes.Status400BadRequest, "Invalid execution session id", "The X-Execution-Session-ID header must contain a valid session id.").ConfigureAwait(false);
            return;
        }

        try
        {
            await application.GetAsync(value, context.RequestAborted).ConfigureAwait(false);
        }
        catch (ExecutionSessionNotFoundException)
        {
            await ApiProblemDetails.WriteAsync(context, StatusCodes.Status404NotFound, "Execution session not found", "The supplied execution session does not exist.").ConfigureAwait(false);
            return;
        }

        context.Items[ItemKey] = value;
        context.Response.Headers[HeaderName] = value.ToString();
        await using var buffer = new MemoryStream();
        var originalBody = context.Response.Body;
        context.Response.Body = buffer;
        try
        {
            await next(context).ConfigureAwait(false);
            var body = buffer.ToArray();
            await buffer.FlushAsync(context.RequestAborted).ConfigureAwait(false);
            context.Response.Body = originalBody;
            await originalBody.WriteAsync(body, context.RequestAborted).ConfigureAwait(false);

            if (context.Response.StatusCode is >= 200 and < 300
                && !context.Items.ContainsKey(IdempotencyMiddleware.ReplayItemKey)
                && !context.Request.Path.StartsWithSegments("/api/v1/execution-sessions"))
            {
                var stage = StageForPath(context.Request.Path);
                if (stage is not null)
                {
                    try
                    {
                        await application.LinkPipelineArtifactAsync(value, stage,
                            context.TraceIdentifier, body, context.RequestAborted).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        logger.LogWarning(exception, "Execution session artifact linking failed after pipeline response. SessionId={SessionId}, Path={Path}", value, context.Request.Path);
                    }
                }
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static string? StageForPath(PathString path)
    {
        if (path.StartsWithSegments("/api/v1/market-context")) return "MarketContext";
        if (path.StartsWithSegments("/api/v1/experts/dispatch")) return "ExpertDispatch";
        if (path.StartsWithSegments("/api/v1/experts/analyze")) return "ExpertAnalysis";
        if (path.StartsWithSegments("/api/v1/consensus")) return "Consensus";
        if (path.StartsWithSegments("/api/v1/trading-decisions")) return "TradingDecision";
        if (path.StartsWithSegments("/api/v1/risk")) return "RiskEvaluation";
        if (path.StartsWithSegments("/api/v1/trading-plans")) return "TradingPlan";
        if (path.StartsWithSegments("/api/v1/trading-workspaces")) return "TradingWorkspace";
        if (path.StartsWithSegments("/api/v1/trading-assistant")) return "TradingAssistant";
        if (path.StartsWithSegments("/api/v1/paper-trading")) return "PaperTrading";
        return null;
    }
}
