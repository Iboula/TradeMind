using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Application;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Configuration;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Runtime;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Security;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOptions<MT5BridgeHostOptions>().Bind(builder.Configuration.GetSection(MT5BridgeHostOptions.SectionName)).Configure(options => MT5BridgeHostOptionsCompatibility.Apply(options, builder.Configuration)).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<MT5BridgeHostOptions>, MT5BridgeHostOptionsValidator>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<MT5BridgeHostRuntime>();
builder.Services.AddSingleton<IMT5SecretProvider, ConfigurationMT5SecretProvider>();
builder.Services.AddHttpClient("mt5-terminal", client => client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddSingleton<IMT5TerminalDiscovery, MT5TerminalDiscovery>();
builder.Services.AddSingleton<IMT5TerminalProcessController, MT5TerminalProcessController>();
builder.Services.AddSingleton<IMT5TerminalTransport, HttpMT5TerminalTransport>();
builder.Services.AddSingleton<MT5TerminalExecutionSafetyPolicy>();
builder.Services.AddSingleton<RealMT5TerminalGateway>();
builder.Services.AddSingleton<SimulatedMT5TerminalGateway>();
builder.Services.AddSingleton<IMT5TerminalGateway>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<MT5BridgeHostOptions>>().Value;
    return MT5TerminalGatewayModeParser.TryParse(options.GatewayMode, out var mode) && mode == MT5TerminalGatewayMode.Real
        ? serviceProvider.GetRequiredService<RealMT5TerminalGateway>()
        : serviceProvider.GetRequiredService<SimulatedMT5TerminalGateway>();
});
builder.Services.AddSingleton<IReplayProtector, InMemoryReplayProtector>();
builder.Services.AddSingleton<IBridgeIdempotencyStore, InMemoryBridgeIdempotencyStore>();
builder.Services.AddSingleton<BridgeSecurityValidator>();
builder.Services.AddSingleton<BridgeHostApplication>();
builder.Services.AddHostedService<BridgeHostLifecycleService>();

var app = builder.Build();
app.MapGet("/", (MT5BridgeHostRuntime runtime, IOptions<MT5BridgeHostOptions> options) => Results.Ok(new
{
    service = "TradeMind.MetaTrader5.Bridge.Host",
    status = runtime.IsReady ? "ready" : runtime.State.ToString().ToLowerInvariant(),
    mode = options.Value.GatewayMode,
    environment = options.Value.Environment
}));
app.MapGet("/health/live", (MT5BridgeHostRuntime runtime) => Results.Ok(new { status = runtime.State is MT5BridgeHostState.Stopped or MT5BridgeHostState.Faulted ? "unhealthy" : "healthy" }));
app.MapGet("/health/ready", (MT5BridgeHostRuntime runtime) => runtime.IsReady ? Results.Ok(new { status = "ready" }) : Results.Json(new { status = "not-ready" }, statusCode: StatusCodes.Status503ServiceUnavailable));
app.MapGet("/health/startup", (MT5BridgeHostRuntime runtime) => runtime.State is MT5BridgeHostState.Starting ? Results.Json(new { status = "starting" }, statusCode: StatusCodes.Status503ServiceUnavailable) : Results.Ok(new { status = "started" }));
app.MapGet("/health/terminal", (IMT5TerminalGateway gateway, IOptions<MT5BridgeHostOptions> options) =>
{
    var snapshot = gateway.Snapshot;
    var ready = gateway.IsAvailable;
    return Results.Json(new
    {
        status = ready ? "ready" : "not-ready",
        mode = options.Value.GatewayMode,
        connectionState = snapshot.ConnectionState.ToString(),
        terminalVersion = snapshot.TerminalVersion,
        terminalBuild = snapshot.TerminalBuild,
        terminalArchitecture = snapshot.TerminalArchitecture,
        protocolVersion = snapshot.ProtocolVersion,
        accountEnvironment = snapshot.AccountEnvironment,
        tradingEnabled = snapshot.TradingEnabled,
        readOnly = snapshot.ReadOnly,
        latencyMilliseconds = snapshot.Latency.TotalMilliseconds,
        lastHeartbeatUtc = snapshot.LastHeartbeatUtc,
        lastReconnectUtc = snapshot.LastReconnectUtc,
        reconnectCount = snapshot.ReconnectCount
    }, statusCode: ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});
app.MapPost("/bridge/v1/handshake", (HttpContext context, BridgeHandshakeRequest request, BridgeHostApplication host, CancellationToken cancellationToken) => host.HandshakeAsync(context, request, cancellationToken));
app.MapPost("/bridge/v1/health", (HttpContext context, BridgeHealthRequest request, BridgeHostApplication host, CancellationToken cancellationToken) => host.HealthAsync(context, request, cancellationToken));
app.MapPost("/bridge/v1/execute", (HttpContext context, BridgeTransportRequest request, BridgeHostApplication host, CancellationToken cancellationToken) => host.ExecuteAsync(context, request, cancellationToken));

app.Run();

public partial class Program;

internal sealed class BridgeHostLifecycleService(MT5BridgeHostRuntime runtime, IMT5TerminalGateway gateway, IOptions<MT5BridgeHostOptions> options, ILogger<BridgeHostLifecycleService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting MT5 bridge host with gateway mode {GatewayMode} and environment {Environment}.", options.Value.GatewayMode, options.Value.Environment);
            runtime.MarkWaitingForTerminal();
            await gateway.StartAsync(cancellationToken).ConfigureAwait(false);
            if (!gateway.IsAvailable)
            {
                runtime.MarkDegraded();
                logger.LogWarning("MT5 bridge host started without an available terminal gateway.");
                return;
            }

            runtime.MarkReady();
            logger.LogInformation("MT5 bridge host is ready with gateway mode {GatewayMode}.", options.Value.GatewayMode);
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
