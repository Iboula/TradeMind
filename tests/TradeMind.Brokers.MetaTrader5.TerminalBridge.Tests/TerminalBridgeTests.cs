using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Configuration;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Protocol;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Security;
using TradeMind.Brokers.MetaTrader5.TerminalBridge.Terminal;
using Xunit;

namespace TradeMind.Brokers.MetaTrader5.TerminalBridge.Tests;

public sealed class TerminalBridgeTests
{
    [Fact]
    public void Options_accept_a_local_https_demo_configuration()
    {
        var result = new TerminalBridgeOptionsValidator().Validate(null, new TerminalBridgeOptions());
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Failures ?? []));
    }

    [Fact]
    public void Options_reject_live_or_non_local_configuration()
    {
        var validator = new TerminalBridgeOptionsValidator();
        Assert.False(validator.Validate(null, new TerminalBridgeOptions { AllowLive = true }).Succeeded);
        Assert.False(validator.Validate(null, new TerminalBridgeOptions { Urls = "https://example.test" }).Succeeded);
        Assert.False(validator.Validate(null, new TerminalBridgeOptions { AuthenticationMode = "MutualTls" }).Succeeded);
    }

    [Fact]
    public void Request_signatures_are_deterministic_and_secret_dependent()
    {
        var first = BridgeRequestSigning.CreateSignature("secret", "POST", "/terminal/v1/ping", "100", "nonce", "{}");
        var second = BridgeRequestSigning.CreateSignature("secret", "POST", "/terminal/v1/ping", "100", "nonce", "{}");
        var other = BridgeRequestSigning.CreateSignature("other", "POST", "/terminal/v1/ping", "100", "nonce", "{}");

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.True(BridgeRequestSigning.VerifySignature("secret", "POST", "/terminal/v1/ping", "100", "nonce", "{}", first));
    }

    [Fact]
    public async Task Health_and_handshake_are_contract_compatible()
    {
        await using var factory = new TerminalBridgeFactory();
        using var client = factory.CreateClient();

        var health = await client.GetFromJsonAsync<TerminalHealthResponse>("/health");
        Assert.Equal("not-ready", health!.Status);

        var response = await SendSignedAsync<TerminalHandshakeResponse>(client, HttpMethod.Post, "/terminal/v1/handshake", "{\"protocolVersion\":\"1.0\"}");
        Assert.False(response.Body.Success);
        Assert.Equal("TERMINAL_NOT_READY", response.Body.ErrorCode);
    }

    [Fact]
    public async Task Agent_poll_makes_demo_handshake_ready_and_ping_works()
    {
        await using var factory = new TerminalBridgeFactory();
        using var client = factory.CreateClient();
        await RegisterDemoAgentAsync(client);

        var handshake = await SendSignedAsync<TerminalHandshakeResponse>(client, HttpMethod.Post, "/terminal/v1/handshake", "{\"protocolVersion\":\"1.0\"}");
        var ping = await SendSignedAsync<TerminalPingResponse>(client, HttpMethod.Post, "/terminal/v1/ping", "{\"requestedAtUtc\":null}");

        Assert.True(handshake.Body.Success);
        Assert.Equal("Demo", handshake.Body.AccountEnvironment);
        Assert.True(ping.Body.Success);
    }

    [Fact]
    public async Task Read_command_round_trips_through_the_agent_without_exposing_credentials()
    {
        await using var factory = new TerminalBridgeFactory();
        using var client = factory.CreateClient();
        await RegisterDemoAgentAsync(client);

        var executeTask = SendSignedAsync<TerminalExecuteResponse>(client, HttpMethod.Post, "/terminal/v1/execute", "{\"command\":\"get-account\",\"fields\":{}}");
        AgentPollResponse? poll = null;
        for (var attempt = 0; attempt < 20 && poll?.Command is null; attempt++)
        {
            poll = await PollAgentAsync(client);
            if (poll.Command is null) await Task.Delay(25);
        }
        var command = poll?.Command ?? throw new InvalidOperationException("The terminal agent did not receive the queued command.");
        Assert.Equal("get-account", command.Command);

        await SendAgentResultAsync(client, "/terminal/v1/agent/result", new AgentResultRequest(command.Id, true, "ACCOUNT", "Account returned", new Dictionary<string, string> { ["items"] = "[]" }));
        var result = await executeTask;

        Assert.True(result.Body.Success);
        Assert.Equal("ACCOUNT", result.Body.Code);
        Assert.DoesNotContain("password", result.Raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", result.Raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Phase_one_rejects_every_write_command()
    {
        await using var factory = new TerminalBridgeFactory();
        using var client = factory.CreateClient();
        await RegisterDemoAgentAsync(client);

        foreach (var command in new[] { "submit-order", "modify-order", "cancel-order", "close-position" })
        {
            var body = JsonSerializer.Serialize(new { command, fields = new Dictionary<string, string>() });
            var result = await SendSignedAsync<TerminalExecuteResponse>(client, HttpMethod.Post, "/terminal/v1/execute", body);
            Assert.False(result.Body.Success);
            Assert.Equal("LIVE_MODE_FORBIDDEN", result.Body.Code);
        }
    }

    [Fact]
    public async Task Duplicate_nonce_is_rejected_without_replaying_the_request()
    {
        await using var factory = new TerminalBridgeFactory();
        using var client = factory.CreateClient();
        var body = "{\"protocolVersion\":\"1.0\"}";
        var nonce = "fixed-test-nonce";
        var first = await SendSignedAsync<TerminalHandshakeResponse>(client, HttpMethod.Post, "/terminal/v1/handshake", body, nonce);
        var second = await SendSignedAsync<TerminalHandshakeResponse>(client, HttpMethod.Post, "/terminal/v1/handshake", body, nonce);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Agent_timeout_is_normalized_without_an_orphaned_task()
    {
        await using var provider = new ServiceProviderFactory();
        var session = provider.Session;
        session.Update(new AgentPollRequest("1.0", "MT5", 6090, "x64", true, "Demo", true, false, "demo", []), DateTimeOffset.UtcNow);
        var result = await session.EnqueueAsync("get-orders", new Dictionary<string, string>(), TimeSpan.FromMilliseconds(20), CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal("TIMEOUT", result.Code);
    }

    [Fact]
    public async Task Real_read_only_smoke_is_opt_in_and_does_not_write_orders()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("MT5_REAL_TESTS"), "true", StringComparison.OrdinalIgnoreCase)) return;
        var endpoint = Environment.GetEnvironmentVariable("MT5_TERMINAL_BRIDGE_ENDPOINT");
        var token = Environment.GetEnvironmentVariable("MT5_TERMINAL_BRIDGE_TOKEN");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(token)) return;

        using var client = new HttpClient { BaseAddress = new Uri(endpoint) };
        var handshake = await SendSignedAsync<TerminalHandshakeResponse>(client, HttpMethod.Post, "/terminal/v1/handshake", "{\"protocolVersion\":\"1.0\"}", token: token);
        Assert.True(handshake.Body.Success, handshake.Raw);
        var ping = await SendSignedAsync<TerminalPingResponse>(client, HttpMethod.Post, "/terminal/v1/ping", "{\"requestedAtUtc\":null}", token: token);
        Assert.True(ping.Body.Success, ping.Raw);
    }

    [Fact]
    public async Task Real_read_only_commands_round_trip_without_write_operations()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("MT5_REAL_TESTS"), "true", StringComparison.OrdinalIgnoreCase)) return;
        var endpoint = Environment.GetEnvironmentVariable("MT5_TERMINAL_BRIDGE_ENDPOINT");
        var token = Environment.GetEnvironmentVariable("MT5_TERMINAL_BRIDGE_TOKEN");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(token)) return;

        using var client = new HttpClient { BaseAddress = new Uri(endpoint) };
        var handshake = await SendSignedAsync<TerminalHandshakeResponse>(client, HttpMethod.Post, "/terminal/v1/handshake", "{\"protocolVersion\":\"1.0\"}", token: token);
        Assert.True(handshake.Body.Success);
        Assert.Equal("Demo", handshake.Body.AccountEnvironment);

        var commands = new (string Name, string Body)[]
        {
            ("heartbeat", "{\"command\":\"heartbeat\",\"fields\":{}}"),
            ("get-account", "{\"command\":\"get-account\",\"fields\":{}}"),
            ("get-instrument", "{\"command\":\"get-instrument\",\"fields\":{\"instrument\":\"EURUSD\"}}"),
            ("get-orders", "{\"command\":\"get-orders\",\"fields\":{}}"),
            ("get-positions", "{\"command\":\"get-positions\",\"fields\":{}}")
        };

        foreach (var command in commands)
        {
            var result = await SendSignedAsync<TerminalExecuteResponse>(client, HttpMethod.Post, "/terminal/v1/execute", command.Body, token: token);
            Assert.True(result.Body.Success, $"The real read command '{command.Name}' failed with code '{result.Body.Code}'.");
        }
    }

    private static async Task RegisterDemoAgentAsync(HttpClient client)
    {
        var response = await PostAgentAsync(client, "/terminal/v1/agent/poll", new AgentPollRequest("1.0", "MT5", 6090, "x64", true, "Demo", true, false, "demo-account", ["account.read", "instrument.read", "orders.read", "positions.read"]));
        Assert.True(response.Success);
    }

    private static async Task<AgentPollResponse> PollAgentAsync(HttpClient client) => await PostAgentAsync(client, "/terminal/v1/agent/poll", new AgentPollRequest("1.0", "MT5", 6090, "x64", true, "Demo", true, false, "demo-account", []));

    private static async Task<AgentPollResponse> PostAgentAsync(HttpClient client, string path, AgentPollRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(request) };
        message.Headers.TryAddWithoutValidation("X-TradeMind-Agent-Token", TerminalBridgeFactory.AgentToken);
        using var response = await client.SendAsync(message);
        return (await response.Content.ReadFromJsonAsync<AgentPollResponse>())!;
    }

    private static async Task SendAgentResultAsync(HttpClient client, string path, AgentResultRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(request) };
        message.Headers.TryAddWithoutValidation("X-TradeMind-Agent-Token", TerminalBridgeFactory.AgentToken);
        using var response = await client.SendAsync(message);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<SignedResponse<T>> SendSignedAsync<T>(HttpClient client, HttpMethod method, string path, string body, string? nonce = null, string token = TerminalBridgeFactory.BridgeToken)
    {
        nonce ??= Guid.NewGuid().ToString("N");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var message = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        message.Headers.TryAddWithoutValidation(BridgeRequestSigning.TimestampHeader, timestamp);
        message.Headers.TryAddWithoutValidation(BridgeRequestSigning.NonceHeader, nonce);
        message.Headers.TryAddWithoutValidation(BridgeRequestSigning.SignatureVersionHeader, BridgeRequestSigning.SignatureVersion);
        message.Headers.TryAddWithoutValidation(BridgeRequestSigning.SignatureHeader, BridgeRequestSigning.CreateSignature(token, method.Method, path, timestamp, nonce, body));
        using var response = await client.SendAsync(message);
        var raw = await response.Content.ReadAsStringAsync();
        var parsed = JsonSerializer.Deserialize<T>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new(response.StatusCode, raw, parsed!);
    }

    private sealed record SignedResponse<T>(HttpStatusCode StatusCode, string Raw, T Body);

    private sealed class TerminalBridgeFactory : WebApplicationFactory<Program>
    {
        public const string BridgeToken = "bridge-test-token";
        public const string AgentToken = "agent-test-token";

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("TradeMind:Brokers:MetaTrader5:TerminalBridge:AllowInsecureDemoTransport", "true");
            builder.UseSetting("TradeMind:Brokers:MetaTrader5:TerminalBridge:Urls", "http://localhost:5001");
            builder.UseSetting("TradeMind:Brokers:MetaTrader5:TerminalBridge:TokenConfigurationKey", "BridgeTestToken");
            builder.UseSetting("TradeMind:Brokers:MetaTrader5:TerminalBridge:AgentTokenConfigurationKey", "AgentTestToken");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["BridgeTestToken"] = BridgeToken, ["AgentTestToken"] = AgentToken }));
        }
    }

    private sealed class ServiceProviderFactory : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        public ServiceProviderFactory()
        {
            var services = new ServiceCollection();
            services.AddOptions<TerminalBridgeOptions>().Configure(options => options.MaxConcurrentRequests = 2);
            services.AddSingleton<IOptions<TerminalBridgeOptions>>(new Microsoft.Extensions.Options.OptionsWrapper<TerminalBridgeOptions>(new TerminalBridgeOptions()));
            services.AddSingleton<TimeProvider>(TimeProvider.System);
            services.AddSingleton<ITerminalAgentSession, InMemoryTerminalAgentSession>();
            provider = services.BuildServiceProvider();
        }
        public ITerminalAgentSession Session => provider.GetRequiredService<ITerminalAgentSession>();
        public ValueTask DisposeAsync() { provider.Dispose(); return ValueTask.CompletedTask; }
    }
}
