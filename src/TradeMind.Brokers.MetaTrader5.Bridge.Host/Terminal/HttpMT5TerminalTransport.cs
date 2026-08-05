using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Configuration;
using TradeMind.Brokers.MetaTrader5.Bridge.Host.Security;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;

internal sealed class HttpMT5TerminalTransport(
    IHttpClientFactory clientFactory,
    IOptions<MT5BridgeHostOptions> options,
    IMT5SecretProvider secrets,
    TimeProvider timeProvider) : IMT5TerminalTransport
{
    private readonly MT5BridgeHostOptions configuration = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TerminalBridgeHandshakeResult> HandshakeAsync(CancellationToken cancellationToken)
    {
        var response = await SendAsync<TerminalBridgeHandshakeResponse>("terminal/v1/handshake", new { protocolVersion = configuration.ExpectedTerminalProtocolVersion }, cancellationToken).ConfigureAwait(false);
        return response is null
            ? new(false, "TERMINAL_BRIDGE_UNAVAILABLE", string.Empty, string.Empty, 0, string.Empty, string.Empty, false, false, false, string.Empty, [])
            : new(response.Success, response.ErrorCode ?? "", response.ProtocolVersion ?? "", response.TerminalVersion ?? "", response.TerminalBuild, response.TerminalArchitecture ?? "", response.AccountEnvironment ?? "", response.DemoAccount, response.TradingEnabled, response.ReadOnly, response.AccountId ?? "", response.Capabilities ?? []);
    }

    public async Task<TerminalBridgePingResult> PingAsync(CancellationToken cancellationToken)
    {
        var response = await SendAsync<TerminalBridgePingResponse>("terminal/v1/ping", new { requestedAtUtc = timeProvider.GetUtcNow() }, cancellationToken).ConfigureAwait(false);
        return response is null
            ? new(false, "TERMINAL_UNAVAILABLE", string.Empty, null)
            : new(response.Success, response.ErrorCode ?? "", response.TerminalVersion ?? "", response.HeartbeatUtc);
    }

    public async Task<TerminalCommandResult> ExecuteAsync(TerminalCommand command, CancellationToken cancellationToken)
    {
        var response = await SendAsync<TerminalBridgeCommandResponse>("terminal/v1/execute", new { command = command.Command, fields = command.Fields }, cancellationToken).ConfigureAwait(false);
        if (response is null) return new(false, "TERMINAL_UNAVAILABLE", "The demo terminal is unavailable.", new Dictionary<string, string>());
        return new(response.Success, response.Code ?? "TERMINAL_FAILURE", response.Success ? response.Message ?? "" : "The demo terminal rejected the request.", response.Fields ?? new Dictionary<string, string>());
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        _ = await SendAsync<TerminalBridgeAckResponse>("terminal/v1/disconnect", new { }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T?> SendAsync<T>(string relativePath, object body, CancellationToken cancellationToken)
    {
        if (configuration.TerminalBridgeEndpoint is null) return default;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(configuration.TerminalBridgeEndpoint, relativePath));
        request.Content = JsonContent.Create(body, options: JsonOptions);
        var token = configuration.RequireTerminalBridgeAuthentication ? secrets.GetSecret(configuration.TerminalBridgeTokenConfigurationKey) : null;
        if (configuration.RequireTerminalBridgeAuthentication && string.IsNullOrWhiteSpace(token)) return default;
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await clientFactory.CreateClient("mt5-terminal").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return default;
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return default; }
        catch (HttpRequestException) { return default; }
        catch (JsonException) { return default; }
        catch (InvalidOperationException) { return default; }
        catch (UriFormatException) { return default; }
    }

    private sealed record TerminalBridgeHandshakeResponse(bool Success, string? ErrorCode, string? ProtocolVersion, string? TerminalVersion, int TerminalBuild, string? TerminalArchitecture, string? AccountEnvironment, bool DemoAccount, bool TradingEnabled, bool ReadOnly, string? AccountId, List<string>? Capabilities);
    private sealed record TerminalBridgePingResponse(bool Success, string? ErrorCode, string? TerminalVersion, DateTimeOffset? HeartbeatUtc);
    private sealed record TerminalBridgeCommandResponse(bool Success, string? Code, string? Message, Dictionary<string, string>? Fields);
    private sealed record TerminalBridgeAckResponse(bool Success);
}
