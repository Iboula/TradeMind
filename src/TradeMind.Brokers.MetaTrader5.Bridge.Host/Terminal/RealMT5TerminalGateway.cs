using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Configuration;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Security;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Telemetry;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;

internal sealed class RealMT5TerminalGateway(
    IOptions<MT5BridgeHostOptions> options,
    IMT5TerminalDiscovery discovery,
    IMT5TerminalProcessController processController,
    IMT5TerminalTransport transport,
    IMT5SecretProvider secrets,
    MT5TerminalExecutionSafetyPolicy safetyPolicy,
    TimeProvider timeProvider,
    ITradeMindMetrics? bridgeMetrics = null,
    ILogger<RealMT5TerminalGateway>? logger = null) : IMT5TerminalGateway
{
    private readonly MT5BridgeHostOptions configuration = options.Value;
    private readonly ITradeMindMetrics metrics = bridgeMetrics ?? NullBridgeMetrics.Instance;
    private readonly ILogger<RealMT5TerminalGateway> log = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RealMT5TerminalGateway>.Instance;
    private readonly object sync = new();
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private MT5TerminalGatewaySnapshot snapshot = new(MT5TerminalConnectionState.Disconnected, "unknown", 0, "unknown", "", "", false, false, TimeSpan.Zero, null, null, 0);
    private bool ownsTerminalProcess;

    public string TerminalVersion => Snapshot.TerminalVersion;
    public bool IsAvailable => Snapshot.ConnectionState == MT5TerminalConnectionState.Connected;
    public MT5TerminalGatewaySnapshot Snapshot { get { lock (sync) return snapshot; } }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsAvailable) return;
            SetState(MT5TerminalConnectionState.Connecting);
            EnsureRuntimeConfiguration();
            await ConnectCoreAsync(cancellationToken, allowStart: true).ConfigureAwait(false);
            metrics.IncrementCounter(TelemetryMetricNames.Mt5Connections, 1, Dimensions("Connect", "Succeeded"));
        }
        catch (OperationCanceledException) { SetState(MT5TerminalConnectionState.Faulted); throw; }
        catch (Exception exception)
        {
            SetState(MT5TerminalConnectionState.Faulted);
            if (ownsTerminalProcess)
            {
                try { await processController.StopAsync(CancellationToken.None).ConfigureAwait(false); } catch (Exception) { }
                ownsTerminalProcess = false;
            }
            log.LogError(exception, "The real demo terminal gateway could not start.");
            throw new InvalidOperationException("The real demo terminal gateway could not start.", exception);
        }
        finally { lifecycleGate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SetState(MT5TerminalConnectionState.Closed);
            using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            shutdown.CancelAfter(TimeSpan.FromSeconds(configuration.TerminalShutdownTimeoutSeconds));
            try { await transport.DisconnectAsync(shutdown.Token).ConfigureAwait(false); } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            if (ownsTerminalProcess)
            {
                try { await processController.StopAsync(shutdown.Token).ConfigureAwait(false); } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
                ownsTerminalProcess = false;
            }
        }
        finally { lifecycleGate.Release(); }
    }

    public async Task<TerminalCommandResult> ExecuteAsync(TerminalCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        if (command.Command.Equals("heartbeat", StringComparison.Ordinal)) return await PingAsync(cancellationToken).ConfigureAwait(false);
        if (!IsAvailable) return Failure("TERMINAL_UNAVAILABLE", "The demo terminal is unavailable.");
        if (!await EnsureHeartbeatAsync(cancellationToken).ConfigureAwait(false)) return Failure("TERMINAL_UNAVAILABLE", "The demo terminal heartbeat is unavailable.");
        var decision = safetyPolicy.Validate(command, Snapshot, timeProvider.GetUtcNow(), TimeSpan.FromSeconds(configuration.TerminalHeartbeatTimeoutSeconds), configuration.EnableWriteTests);
        if (!decision.Allowed) return Failure(decision.Code, decision.Message);

        var started = timeProvider.GetTimestamp();
        try
        {
            var result = await transport.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
            metrics.RecordDuration(TelemetryMetricNames.Mt5Latency, timeProvider.GetElapsedTime(started), Dimensions(command.Command, result.Success ? "Succeeded" : "Rejected"));
            if (!result.Success && result.Code.Equals("TERMINAL_UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
            {
                SetState(MT5TerminalConnectionState.Reconnecting);
                await ReconnectAsync(cancellationToken).ConfigureAwait(false);
            }
            return result.Success ? result : Failure(NormalizeFailureCode(result.Code), result.Message);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            SetState(MT5TerminalConnectionState.Reconnecting);
            log.LogWarning(exception, "The demo terminal command failed without exposing terminal details.");
            await ReconnectAsync(cancellationToken).ConfigureAwait(false);
            return Failure("TERMINAL_UNAVAILABLE", "The demo terminal is unavailable.");
        }
    }

    private async Task<TerminalCommandResult> PingAsync(CancellationToken cancellationToken)
    {
        if (!IsAvailable) return Failure("TERMINAL_UNAVAILABLE", "The demo terminal is unavailable.");
        var started = timeProvider.GetTimestamp();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TerminalHeartbeatTimeoutSeconds));
            var response = await transport.PingAsync(timeout.Token).ConfigureAwait(false);
            metrics.RecordDuration(TelemetryMetricNames.Mt5Latency, timeProvider.GetElapsedTime(started), Dimensions("Heartbeat", response.Success ? "Succeeded" : "Failed"));
            if (!response.Success)
            {
                SetState(MT5TerminalConnectionState.Reconnecting);
                return Failure("HEARTBEAT_INVALID", "The demo terminal heartbeat failed.");
            }
            UpdateHeartbeat(response.HeartbeatUtc ?? timeProvider.GetUtcNow(), response.TerminalVersion);
            metrics.IncrementCounter(TelemetryMetricNames.Mt5Heartbeats, 1, Dimensions("Heartbeat", "Succeeded"));
            return new(true, "HEARTBEAT", "Heartbeat acknowledged", new Dictionary<string, string> { ["terminal_version"] = TerminalVersion, ["heartbeat_at_utc"] = (response.HeartbeatUtc ?? timeProvider.GetUtcNow()).ToString("O") });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            SetState(MT5TerminalConnectionState.Reconnecting);
            return Failure("HEARTBEAT_INVALID", "The demo terminal heartbeat timed out.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            SetState(MT5TerminalConnectionState.Reconnecting);
            log.LogWarning(exception, "The demo terminal heartbeat failed.");
            return Failure("HEARTBEAT_INVALID", "The demo terminal heartbeat failed.");
        }
    }

    private async Task<bool> EnsureHeartbeatAsync(CancellationToken cancellationToken)
    {
        var result = await PingAsync(cancellationToken).ConfigureAwait(false);
        if (result.Success) return true;
        return await ReconnectAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ReconnectAsync(CancellationToken cancellationToken)
    {
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsAvailable && Snapshot.LastHeartbeatUtc is not null && timeProvider.GetUtcNow() - Snapshot.LastHeartbeatUtc.Value <= TimeSpan.FromSeconds(configuration.TerminalHeartbeatTimeoutSeconds)) return true;
            SetState(MT5TerminalConnectionState.Reconnecting);
            for (var attempt = 1; attempt <= configuration.ReconnectMaximumAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var delay = TimeSpan.FromMilliseconds(configuration.ReconnectBackoffMilliseconds * attempt);
                if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                try
                {
                    await ConnectCoreAsync(cancellationToken, allowStart: configuration.AutoStartTerminal).ConfigureAwait(false);
                    var current = Snapshot;
                    SetSnapshot(current with { ReconnectCount = current.ReconnectCount + 1, LastReconnectUtc = timeProvider.GetUtcNow() });
                    metrics.IncrementCounter(TelemetryMetricNames.Mt5Reconnects, 1, Dimensions("Reconnect", "Succeeded"));
                    return true;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception exception) { log.LogWarning(exception, "The demo terminal reconnect attempt failed."); }
            }

            metrics.IncrementCounter(TelemetryMetricNames.Mt5Reconnects, 1, Dimensions("Reconnect", "Failed"));
            SetState(MT5TerminalConnectionState.Faulted);
            return false;
        }
        finally { lifecycleGate.Release(); }
    }

    private async Task ConnectCoreAsync(CancellationToken cancellationToken, bool allowStart)
    {
        var descriptor = await discovery.DiscoverAsync(configuration.TerminalPath, cancellationToken).ConfigureAwait(false);
        ValidateDescriptor(descriptor);
        log.LogInformation("Demo terminal discovered with build {TerminalBuild}, architecture {TerminalArchitecture}, running {IsRunning}.", descriptor.Build, descriptor.Architecture, descriptor.IsRunning);
        if (!descriptor.IsRunning)
        {
            if (!allowStart || !configuration.AutoStartTerminal) throw new InvalidOperationException("The configured demo terminal is not running.");
            await processController.StartAsync(descriptor.Path, cancellationToken).ConfigureAwait(false);
            ownsTerminalProcess = true;
            log.LogInformation("Demo terminal process started by the gateway.");
            var deadline = timeProvider.GetUtcNow().AddSeconds(configuration.TerminalStartupTimeoutSeconds);
            while (!discovery.IsRunning(descriptor.Path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (timeProvider.GetUtcNow() >= deadline) throw new TimeoutException("The demo terminal did not start before the configured timeout.");
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
        }

        SetSnapshot(Snapshot with { ConnectionState = MT5TerminalConnectionState.Authenticating, TerminalVersion = descriptor.Version, TerminalBuild = descriptor.Build, TerminalArchitecture = descriptor.Architecture });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TerminalStartupTimeoutSeconds));
        var started = timeProvider.GetTimestamp();
        var handshake = await transport.HandshakeAsync(timeout.Token).ConfigureAwait(false);
        ValidateHandshake(handshake);
        var ping = await transport.PingAsync(timeout.Token).ConfigureAwait(false);
        if (!ping.Success) throw new InvalidOperationException("The demo terminal heartbeat could not be established.");
        var heartbeat = ping.HeartbeatUtc ?? timeProvider.GetUtcNow();
        var terminalVersion = string.IsNullOrWhiteSpace(handshake.TerminalVersion) ? descriptor.Version : handshake.TerminalVersion;
        var terminalBuild = handshake.TerminalBuild == 0 ? descriptor.Build : handshake.TerminalBuild;
        var terminalArchitecture = string.IsNullOrWhiteSpace(handshake.TerminalArchitecture) ? descriptor.Architecture : handshake.TerminalArchitecture;
        SetSnapshot(new(MT5TerminalConnectionState.Connected, terminalVersion, terminalBuild, terminalArchitecture, handshake.ProtocolVersion, handshake.AccountEnvironment, handshake.TradingEnabled, handshake.ReadOnly, timeProvider.GetElapsedTime(started), heartbeat, Snapshot.LastReconnectUtc, Snapshot.ReconnectCount));
        log.LogInformation("Demo terminal bridge handshake succeeded with protocol {ProtocolVersion}, terminal build {TerminalBuild}, account environment {AccountEnvironment}, read-only {ReadOnly}.", handshake.ProtocolVersion, terminalBuild, handshake.AccountEnvironment, handshake.ReadOnly);
    }

    private void EnsureRuntimeConfiguration()
    {
        if (!configuration.DemoOnly || configuration.AllowLive) throw new InvalidOperationException("Live execution is disabled.");
        if (configuration.RequireTerminalBridgeAuthentication && string.IsNullOrWhiteSpace(secrets.GetSecret(configuration.TerminalBridgeTokenConfigurationKey))) throw new InvalidOperationException("The terminal bridge authentication secret is not configured.");
    }

    private void ValidateDescriptor(MT5TerminalDescriptor descriptor)
    {
        if (!MT5TerminalArchitectureParser.TryParse(configuration.SupportedTerminalArchitecture, out var expected) || !descriptor.Architecture.Equals(expected.ToString().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The demo terminal architecture is not supported.");
        if (descriptor.Build < configuration.MinimumSupportedTerminalBuild || descriptor.Build > configuration.MaximumSupportedTerminalBuild) throw new InvalidOperationException("The demo terminal build is not supported.");
    }

    private void ValidateHandshake(TerminalBridgeHandshakeResult handshake)
    {
        if (!handshake.Success)
        {
            var message = handshake.ErrorCode.Equals("TERMINAL_BRIDGE_UNAVAILABLE", StringComparison.OrdinalIgnoreCase)
                ? "The terminal-side bridge is unavailable. Start the external bridge and verify its configured endpoint."
                : "The terminal-side bridge rejected the demo handshake.";
            throw new InvalidOperationException(message);
        }
        if (!BridgeHostProtocolVersion.TryParse(configuration.ExpectedTerminalProtocolVersion, out var expected) || !BridgeHostProtocolVersion.TryParse(handshake.ProtocolVersion, out var actual) || !actual.IsCompatibleWith(expected)) throw new InvalidOperationException("The demo terminal protocol is not supported.");
        if (!handshake.DemoAccount || !handshake.AccountEnvironment.Equals("Demo", StringComparison.OrdinalIgnoreCase) || handshake.AccountEnvironment.Equals("Live", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The connected account is not a demo account.");
        if (!handshake.TradingEnabled || handshake.ReadOnly) throw new InvalidOperationException("The demo account is not enabled for trading.");
    }

    private void UpdateHeartbeat(DateTimeOffset heartbeatUtc, string terminalVersion)
    {
        var current = Snapshot;
        SetSnapshot(current with { ConnectionState = MT5TerminalConnectionState.Connected, TerminalVersion = string.IsNullOrWhiteSpace(terminalVersion) ? current.TerminalVersion : terminalVersion, LastHeartbeatUtc = heartbeatUtc });
    }

    private void SetState(MT5TerminalConnectionState state) { lock (sync) snapshot = snapshot with { ConnectionState = state }; }
    private void SetSnapshot(MT5TerminalGatewaySnapshot value) { lock (sync) snapshot = value; }
    private static TerminalCommandResult Failure(string code, string message) => new(false, code, message, new Dictionary<string, string>());
    private static string NormalizeFailureCode(string code) => code switch { "TERMINAL_UNAVAILABLE" => "TERMINAL_UNAVAILABLE", "ORDER_REJECTED" => "ORDER_REJECTED", "ORDER_NOT_FOUND" => "ORDER_NOT_FOUND", "POSITION_NOT_FOUND" => "POSITION_NOT_FOUND", _ => "TERMINAL_FAILURE" };
    private static MetricDimensions Dimensions(string operation, string? outcome = null) => new(Module: "MT5Bridge", Operation: operation, Stage: "Broker", Connector: "mt5", Mode: "Demo", Outcome: outcome);
}
