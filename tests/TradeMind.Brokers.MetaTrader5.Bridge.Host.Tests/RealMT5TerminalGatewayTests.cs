using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Configuration;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Security;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Tests;

public sealed class RealMT5TerminalGatewayTests
{
    [Fact]
    public void Simulation_is_the_safe_default_and_real_configuration_is_strict()
    {
        var validator = new MT5BridgeHostOptionsValidator();
        var simulation = validator.Validate(Options.DefaultName, new MT5BridgeHostOptions());
        var real = validator.Validate(Options.DefaultName, new MT5BridgeHostOptions { GatewayMode = "Real" });

        Assert.True(simulation.Succeeded);
        Assert.False(real.Succeeded);
        Assert.Contains(real.Failures!, failure => failure.Contains("TerminalPath", StringComparison.Ordinal));
        Assert.Contains(real.Failures!, failure => failure.Contains("TerminalBridgeEndpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void Existing_bridge_user_secrets_are_mapped_to_the_sprint31_host_options()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TradeMind:Brokers:MetaTrader5:Bridge:Terminal:Mode"] = "Real",
            ["TradeMind:Brokers:MetaTrader5:Bridge:Terminal:TerminalPath"] = "C:/Terminal/terminal64.exe",
            ["TradeMind:Brokers:MetaTrader5:Bridge:Terminal:AutoStart"] = "true",
            ["TradeMind:Brokers:MetaTrader5:Bridge:Endpoint"] = "https://localhost:5001",
            ["TradeMind:Brokers:MetaTrader5:Bridge:Authentication:Token"] = "configured-at-runtime"
        }).Build();
        var options = new MT5BridgeHostOptions();

        MT5BridgeHostOptionsCompatibility.Apply(options, configuration);

        Assert.Equal("Real", options.GatewayMode);
        Assert.Equal("C:/Terminal/terminal64.exe", options.TerminalPath);
        Assert.True(options.AutoStartTerminal);
        Assert.Equal(new Uri("https://localhost:5001"), options.TerminalBridgeEndpoint);
        Assert.Equal("TradeMind:Brokers:MetaTrader5:Bridge:Authentication:Token", options.TerminalBridgeTokenConfigurationKey);
    }

    [Fact]
    public async Task Real_gateway_requires_demo_handshake_and_exposes_explicit_state()
    {
        var transport = new FakeTransport();
        var gateway = CreateGateway(transport);

        await gateway.StartAsync(CancellationToken.None);

        Assert.True(gateway.IsAvailable);
        Assert.Equal(MT5TerminalConnectionState.Connected, gateway.Snapshot.ConnectionState);
        Assert.Equal("Demo", gateway.Snapshot.AccountEnvironment);
        Assert.True(gateway.Snapshot.TradingEnabled);
        Assert.False(gateway.Snapshot.ReadOnly);
        Assert.Equal(1, transport.HandshakeCount);
        Assert.Equal(1, transport.PingCount);

        await gateway.StopAsync(CancellationToken.None);
        Assert.Equal(MT5TerminalConnectionState.Closed, gateway.Snapshot.ConnectionState);
    }

    [Fact]
    public async Task Real_gateway_rejects_trading_without_all_explicit_demo_guards()
    {
        var transport = new FakeTransport();
        var gateway = CreateGateway(transport);
        await gateway.StartAsync(CancellationToken.None);

        var result = await gateway.ExecuteAsync(new TerminalCommand("submit-order", new Dictionary<string, string> { ["mode"] = "Demo" }), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("EXECUTION_GUARDS_REQUIRED", result.Code);
        Assert.Equal(0, transport.ExecuteCount);
    }

    [Fact]
    public async Task Real_gateway_rejects_live_mode_even_when_other_guards_are_present()
    {
        var transport = new FakeTransport();
        var gateway = CreateGateway(transport);
        await gateway.StartAsync(CancellationToken.None);

        var result = await gateway.ExecuteAsync(new TerminalCommand("submit-order", ExecutionFields("Live")), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("LIVE_MODE_FORBIDDEN", result.Code);
        Assert.Equal(0, transport.ExecuteCount);
    }

    [Fact]
    public async Task Lost_heartbeat_reconnects_with_bounded_attempts_and_does_not_retry_the_order()
    {
        var transport = new FakeTransport();
        var gateway = CreateGateway(transport, reconnectAttempts: 2);
        await gateway.StartAsync(CancellationToken.None);
        transport.FailNextPing = true;

        var result = await gateway.ExecuteAsync(new TerminalCommand("submit-order", ExecutionFields("Demo")), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, gateway.Snapshot.ReconnectCount);
        Assert.Equal(2, transport.HandshakeCount);
        Assert.Equal(3, transport.PingCount);
        Assert.Equal(1, transport.ExecuteCount);
    }

    [Fact]
    public async Task External_cancellation_is_propagated_to_transport()
    {
        var transport = new FakeTransport { BlockExecution = true };
        var gateway = CreateGateway(transport);
        await gateway.StartAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.ExecuteAsync(new TerminalCommand("get-orders", new Dictionary<string, string>()), cancellation.Token));
        Assert.True(transport.ExecutionCancellationObserved);
    }

    [Fact]
    public async Task Discovery_reports_a_real_executable_without_exposing_credentials()
    {
        var processPath = Environment.ProcessPath;
        Assert.False(string.IsNullOrWhiteSpace(processPath));
        var descriptor = await new MT5TerminalDiscovery().DiscoverAsync(processPath!, CancellationToken.None);

        Assert.Equal(Path.GetFullPath(processPath!), descriptor.Path);
        Assert.NotEqual(string.Empty, descriptor.Version);
        Assert.Contains(descriptor.Architecture, new[] { "x64", "x86", "unknown" });
    }

    [Fact]
    public async Task Opt_in_real_terminal_smoke_test_never_runs_in_ci_by_default()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("MT5_REAL_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var path = Environment.GetEnvironmentVariable("MT5_TERMINAL_PATH");
        var endpoint = Environment.GetEnvironmentVariable("MT5_TERMINAL_BRIDGE_ENDPOINT");
        var token = Environment.GetEnvironmentVariable("MT5_TERMINAL_BRIDGE_TOKEN");
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(token)) return;
        var options = new MT5BridgeHostOptions { GatewayMode = "Real", TerminalPath = path, TerminalBridgeEndpoint = new Uri(endpoint), RequireTerminalBridgeAuthentication = true, SupportedTerminalArchitecture = "x64", MinimumSupportedTerminalBuild = 1, AllowInsecureDemoTransport = new Uri(endpoint).Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) };
        var gateway = new RealMT5TerminalGateway(Options.Create(options), new MT5TerminalDiscovery(), new MT5TerminalProcessController(), new HttpMT5TerminalTransport(new SingleHttpClientFactory(), Options.Create(options), new EnvironmentSecretProvider(token), TimeProvider.System), new EnvironmentSecretProvider(token), new MT5TerminalExecutionSafetyPolicy(), TimeProvider.System);
        await gateway.StartAsync(CancellationToken.None);
        Assert.True(gateway.IsAvailable);
        Assert.Equal("Demo", gateway.Snapshot.AccountEnvironment);
        await gateway.StopAsync(CancellationToken.None);
    }

    private static RealMT5TerminalGateway CreateGateway(FakeTransport transport, int reconnectAttempts = 2)
    {
        var options = new MT5BridgeHostOptions
        {
            GatewayMode = "Real",
            TerminalPath = "demo-terminal.exe",
            TerminalBridgeEndpoint = new Uri("https://127.0.0.1:17861/"),
            SupportedTerminalArchitecture = "x64",
            MinimumSupportedTerminalBuild = 3000,
            MaximumSupportedTerminalBuild = 99999,
            ReconnectMaximumAttempts = reconnectAttempts,
            ReconnectBackoffMilliseconds = 0,
            TerminalHeartbeatTimeoutSeconds = 1,
            TerminalStartupTimeoutSeconds = 1
        };
        return new RealMT5TerminalGateway(
            Options.Create(options),
            new FakeDiscovery(),
            new FakeProcessController(),
            transport,
            new FakeSecretProvider(),
            new MT5TerminalExecutionSafetyPolicy(),
            TimeProvider.System);
    }

    private static Dictionary<string, string> ExecutionFields(string mode) => new()
    {
        ["mode"] = mode,
        ["demo_confirmation"] = "true",
        ["risk_approved"] = "true",
        ["trading_plan_valid"] = "true",
        ["execution_session_valid"] = "true",
        ["permission"] = "true",
        ["capability"] = "true",
        ["heartbeat_valid"] = "true"
    };

    private sealed class FakeDiscovery : IMT5TerminalDiscovery
    {
        public Task<MT5TerminalDescriptor> DiscoverAsync(string terminalPath, CancellationToken cancellationToken) => Task.FromResult(new MT5TerminalDescriptor(terminalPath, "5.00 build 5000", 5000, "x64", true));
        public bool IsRunning(string terminalPath) => true;
    }

    private sealed class FakeProcessController : IMT5TerminalProcessController
    {
        public Task StartAsync(string terminalPath, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSecretProvider : IMT5SecretProvider
    {
        public string? GetSecret(string key) => "test-only-token";
    }

    private sealed class FakeTransport : IMT5TerminalTransport
    {
        public int HandshakeCount { get; private set; }
        public int PingCount { get; private set; }
        public int ExecuteCount { get; private set; }
        public bool FailNextPing { get; set; }
        public bool BlockExecution { get; set; }
        public bool ExecutionCancellationObserved { get; private set; }

        public Task<TerminalBridgeHandshakeResult> HandshakeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HandshakeCount++;
            return Task.FromResult(new TerminalBridgeHandshakeResult(true, "", "1.0", "terminal-5000", 5000, "x64", "Demo", true, true, false, "demo-account", ["orders", "positions"]));
        }

        public Task<TerminalBridgePingResult> PingAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PingCount++;
            if (FailNextPing)
            {
                FailNextPing = false;
                return Task.FromResult(new TerminalBridgePingResult(false, "FAILURE", "terminal-5000", null));
            }
            return Task.FromResult(new TerminalBridgePingResult(true, "", "terminal-5000", DateTimeOffset.UtcNow));
        }

        public async Task<TerminalCommandResult> ExecuteAsync(TerminalCommand command, CancellationToken cancellationToken)
        {
            ExecuteCount++;
            if (BlockExecution)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); } catch (OperationCanceledException) { ExecutionCancellationObserved = true; throw; }
            }
            return new(true, "ORDER_SUBMITTED", "accepted", new Dictionary<string, string>());
        }

        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class EnvironmentSecretProvider(string token) : IMT5SecretProvider
    {
        public string? GetSecret(string key) => token;
    }

    private sealed class SingleHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient client = new();
        public HttpClient CreateClient(string name) => client;
    }
}
