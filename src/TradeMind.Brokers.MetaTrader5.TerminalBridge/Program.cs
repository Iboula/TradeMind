using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Application;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Configuration;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Protocol;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Security;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Terminal;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("https://localhost:5001");
builder.WebHost.ConfigureKestrel(server => server.Limits.MaxRequestBodySize = 64 * 1024);
builder.Services.AddOptions<TerminalBridgeOptions>()
    .Bind(builder.Configuration.GetSection(TerminalBridgeOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<TerminalBridgeOptions>, TerminalBridgeOptionsValidator>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<TerminalBridgeSecretProvider>();
builder.Services.AddSingleton<ReplayCache>();
builder.Services.AddSingleton<TerminalBridgeSecurity>();
builder.Services.AddSingleton<ITerminalAgentSession, InMemoryTerminalAgentSession>();
builder.Services.AddSingleton<TerminalBridgeApplication>();
builder.Services.AddSingleton<IStartupFilter, TerminalBridgeStartupFilter>();

var app = builder.Build();
app.UseMiddleware<TerminalBridgeSecurityMiddleware>();

app.MapGet("/", (TerminalBridgeApplication application) => Results.Ok(new
{
    service = "TradeMind.MetaTrader5.TerminalBridge",
    status = application.Health().Status,
    mode = "DemoOnly"
}));
app.MapGet("/health", (TerminalBridgeApplication application) => Results.Json(application.Health()));

app.MapPost("/terminal/v1/handshake", (TerminalHandshakeRequest request, TerminalBridgeApplication application) => Results.Ok(application.Handshake(request)));
app.MapPost("/terminal/v1/ping", (TerminalPingRequest request, TerminalBridgeApplication application) => Results.Ok(application.Ping(request)));
app.MapPost("/terminal/v1/execute", async (TerminalExecuteRequest request, TerminalBridgeApplication application, CancellationToken cancellationToken) => Results.Ok(await application.ExecuteAsync(request, cancellationToken).ConfigureAwait(false)));
app.MapPost("/terminal/v1/disconnect", (TerminalBridgeApplication application) =>
{
    application.Disconnect();
    return Results.Ok(new TerminalDisconnectResponse(true));
});
app.MapPost("/terminal/v1/agent/poll", (AgentPollRequest request, TerminalBridgeApplication application) => Results.Ok(application.Poll(request)));
app.MapPost("/terminal/v1/agent/result", (AgentResultRequest request, TerminalBridgeApplication application) => Results.Ok(application.Complete(request)));

app.Run();

public partial class Program;

internal sealed class TerminalBridgeStartupFilter(IOptions<TerminalBridgeOptions> options, ILogger<TerminalBridgeStartupFilter> logger) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return applicationBuilder =>
        {
            logger.LogInformation("MT5 terminal bridge configured for local demo mode with protocol {ProtocolVersion} and max concurrency {MaxConcurrentRequests}.", options.Value.ProtocolVersion, options.Value.MaxConcurrentRequests);
            next(applicationBuilder);
        };
    }
}
