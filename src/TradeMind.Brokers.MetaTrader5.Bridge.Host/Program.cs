using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Application;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Configuration;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Runtime;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Security;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOptions<MT5BridgeHostOptions>().Bind(builder.Configuration.GetSection(MT5BridgeHostOptions.SectionName)).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<MT5BridgeHostOptions>, MT5BridgeHostOptionsValidator>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<MT5BridgeHostRuntime>();
builder.Services.AddSingleton<IMT5TerminalGateway, DeterministicSimulatedTerminalGateway>();
builder.Services.AddSingleton<IReplayProtector, InMemoryReplayProtector>();
builder.Services.AddSingleton<IBridgeIdempotencyStore, InMemoryBridgeIdempotencyStore>();
builder.Services.AddSingleton<BridgeSecurityValidator>();
builder.Services.AddSingleton<BridgeHostApplication>();
builder.Services.AddHostedService<BridgeHostLifecycleService>();

var app = builder.Build();
app.MapGet("/health/live", (MT5BridgeHostRuntime runtime) => Results.Ok(new { status = runtime.State is MT5BridgeHostState.Stopped or MT5BridgeHostState.Faulted ? "unhealthy" : "healthy" }));
app.MapGet("/health/ready", (MT5BridgeHostRuntime runtime) => runtime.IsReady ? Results.Ok(new { status = "ready" }) : Results.Json(new { status = "not-ready" }, statusCode: StatusCodes.Status503ServiceUnavailable));
app.MapGet("/health/startup", (MT5BridgeHostRuntime runtime) => runtime.State is MT5BridgeHostState.Starting ? Results.Json(new { status = "starting" }, statusCode: StatusCodes.Status503ServiceUnavailable) : Results.Ok(new { status = "started" }));
app.MapPost("/bridge/v1/handshake", (HttpContext context, BridgeHandshakeRequest request, BridgeHostApplication host, CancellationToken cancellationToken) => host.HandshakeAsync(context, request, cancellationToken));
app.MapPost("/bridge/v1/health", (HttpContext context, BridgeHealthRequest request, BridgeHostApplication host, CancellationToken cancellationToken) => host.HealthAsync(context, request, cancellationToken));
app.MapPost("/bridge/v1/execute", (HttpContext context, BridgeTransportRequest request, BridgeHostApplication host, CancellationToken cancellationToken) => host.ExecuteAsync(context, request, cancellationToken));

app.Run();

public partial class Program;

internal sealed class BridgeHostLifecycleService(MT5BridgeHostRuntime runtime, IMT5TerminalGateway gateway, ILogger<BridgeHostLifecycleService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            runtime.MarkWaitingForTerminal();
            await gateway.StartAsync(cancellationToken).ConfigureAwait(false);
            if (!gateway.IsAvailable) { runtime.MarkDegraded(); return; }
            runtime.MarkReady();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Bridge host startup failed.");
            runtime.MarkFaulted();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        runtime.MarkStopping();
        await gateway.StopAsync(cancellationToken).ConfigureAwait(false);
        runtime.MarkStopped();
    }
}
