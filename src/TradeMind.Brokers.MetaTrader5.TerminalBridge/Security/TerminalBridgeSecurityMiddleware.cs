using System.Text;
using System.Text.Json;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Protocol;

namespace TradeMind.Brokers.MetaTrader5.TerminalBridge.Security;

public sealed class TerminalBridgeSecurityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TerminalBridgeSecurity security)
    {
        if (!context.Request.Path.StartsWithSegments("/terminal/v1", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/terminal/v1/agent", StringComparison.OrdinalIgnoreCase))
        {
            var agentResult = security.ValidateAgent(context);
            if (!agentResult.Accepted)
            {
                await WriteFailureAsync(context, agentResult).ConfigureAwait(false);
                return;
            }
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        context.Request.Body.Position = 0;
        var result = security.ValidateBridge(context, body);
        if (!result.Accepted)
        {
            await WriteFailureAsync(context, result).ConfigureAwait(false);
            return;
        }
        await next(context).ConfigureAwait(false);
    }

    private static async Task WriteFailureAsync(HttpContext context, SecurityValidationResult result)
    {
        context.Response.StatusCode = result.StatusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new TerminalExecuteResponse(false, result.Code, result.Message, new Dictionary<string, string>())), context.RequestAborted).ConfigureAwait(false);
    }
}
