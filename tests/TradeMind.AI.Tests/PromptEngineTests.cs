using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Application;

namespace TradeMind.AI.Tests;

public sealed class PromptEngineTests
{
    [Fact]
    public void PromptTemplateVersion_ShouldParseValidVersion()
    {
        var version = PromptTemplateVersion.Parse("1.0");

        Assert.Equal(1, version.Major);
        Assert.Equal(0, version.Minor);
    }

    [Fact]
    public void PromptTemplateVersion_ShouldRejectInvalidVersion()
    {
        Assert.Throws<ArgumentException>(() => PromptTemplateVersion.Parse("1"));
    }

    [Fact]
    public void PromptTemplateVersion_ShouldCompareMinorVersions()
    {
        Assert.True(PromptTemplateVersion.Parse("1.1").CompareTo(PromptTemplateVersion.Parse("1.0")) > 0);
    }

    [Fact]
    public void PromptTemplateVersion_ShouldCompareMajorVersions()
    {
        Assert.True(PromptTemplateVersion.Parse("2.0").CompareTo(PromptTemplateVersion.Parse("1.9")) > 0);
    }

    [Fact]
    public async Task Registry_ShouldReturnExplicitVersion()
    {
        var registry = new InMemoryPromptTemplateRegistry([Template("sample", "1.0"), Template("sample", "1.1")]);

        var template = await registry.GetAsync(new PromptTemplateId("sample"), PromptTemplateVersion.Parse("1.0"), CancellationToken.None);

        Assert.Equal("1.0", template.Version.ToString());
    }

    [Fact]
    public async Task Registry_ShouldReturnLatestVersion()
    {
        var registry = new InMemoryPromptTemplateRegistry([Template("sample", "1.0"), Template("sample", "1.1")]);

        var template = await registry.GetLatestAsync(new PromptTemplateId("sample"), CancellationToken.None);

        Assert.Equal("1.1", template.Version.ToString());
    }

    [Fact]
    public void Registry_ShouldRejectDuplicateVersions()
    {
        Assert.Throws<ArgumentException>(() =>
            new InMemoryPromptTemplateRegistry([Template("sample", "1.0"), Template("sample", "1.0")]));
    }

    [Fact]
    public async Task Registry_ShouldFailForUnknownTemplate()
    {
        var registry = new InMemoryPromptTemplateRegistry([Template("sample", "1.0")]);

        await Assert.ThrowsAsync<PromptTemplateNotFoundException>(() =>
            registry.GetLatestAsync(new PromptTemplateId("missing"), CancellationToken.None));
    }

    [Fact]
    public async Task Registry_ShouldFailForUnknownVersion()
    {
        var registry = new InMemoryPromptTemplateRegistry([Template("sample", "1.0")]);

        await Assert.ThrowsAsync<PromptTemplateVersionNotFoundException>(() =>
            registry.GetAsync(new PromptTemplateId("sample"), PromptTemplateVersion.Parse("2.0"), CancellationToken.None));
    }

    [Fact]
    public async Task Registry_ShouldSupportMultipleTemplates()
    {
        var registry = new InMemoryPromptTemplateRegistry([Template("first", "1.0"), Template("second", "1.0")]);

        var template = await registry.GetLatestAsync(new PromptTemplateId("second"), CancellationToken.None);

        Assert.Equal("second", template.Id.Value);
    }

    [Fact]
    public async Task Renderer_ShouldRejectMissingRequiredVariable()
    {
        var renderer = Renderer(TemplateWithVariables([Variable("name", PromptVariableType.String, required: true)]));

        var exception = await Assert.ThrowsAsync<PromptValidationException>(() =>
            renderer.RenderAsync(RenderRequest(EmptyVariables()), CancellationToken.None));

        Assert.Equal("name", exception.VariableName);
    }

    [Fact]
    public async Task Renderer_ShouldApplyDefaultValue()
    {
        var renderer = Renderer(TemplateWithVariables([Variable("name", PromptVariableType.String, required: false, defaultValue: "Ada")]));

        var result = await renderer.RenderAsync(RenderRequest(EmptyVariables()), CancellationToken.None);

        Assert.Contains(result.Messages, message => message.Content.Contains("Ada", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Renderer_ShouldRejectIncorrectType()
    {
        var renderer = Renderer(TemplateWithVariables([Variable("count", PromptVariableType.Integer, required: true)]));

        await Assert.ThrowsAsync<PromptValidationException>(() =>
            renderer.RenderAsync(RenderRequest(new Dictionary<string, string> { ["count"] = "many" }), CancellationToken.None));
    }

    [Fact]
    public async Task Renderer_ShouldRejectMaxLengthOverflow()
    {
        var renderer = Renderer(TemplateWithVariables([Variable("name", PromptVariableType.String, required: true, maxLength: 3)]));

        await Assert.ThrowsAsync<PromptValidationException>(() =>
            renderer.RenderAsync(RenderRequest(new Dictionary<string, string> { ["name"] = "long" }), CancellationToken.None));
    }

    [Fact]
    public async Task Renderer_ShouldRejectUndeclaredVariable()
    {
        var renderer = Renderer(TemplateWithVariables([Variable("name", PromptVariableType.String, required: true)]));

        await Assert.ThrowsAsync<PromptValidationException>(() =>
            renderer.RenderAsync(
                RenderRequest(new Dictionary<string, string> { ["name"] = "Ada", ["extra"] = "value" }),
                CancellationToken.None));
    }

    [Fact]
    public async Task Renderer_ShouldNotRevealSensitiveValueInException()
    {
        var renderer = Renderer(TemplateWithVariables([Variable("secret", PromptVariableType.Integer, required: true, isSensitive: true)]));

        var exception = await Assert.ThrowsAsync<PromptValidationException>(() =>
            renderer.RenderAsync(RenderRequest(new Dictionary<string, string> { ["secret"] = "super-secret-value" }), CancellationToken.None));

        Assert.Null(exception.VariableName);
        Assert.DoesNotContain("super-secret-value", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PromptVariableType.String, "hello", "hello")]
    [InlineData(PromptVariableType.Integer, "42", "42")]
    [InlineData(PromptVariableType.Decimal, "42.50", "42.50")]
    [InlineData(PromptVariableType.Boolean, "true", "True")]
    [InlineData(PromptVariableType.DateTime, "2026-07-18T10:00:00Z", "2026-07-18T10:00:00.0000000+00:00")]
    [InlineData(PromptVariableType.Json, "{\"ok\":true}", "{\"ok\":true}")]
    public async Task Renderer_ShouldSupportVariableTypes(
        PromptVariableType type,
        string input,
        string expected)
    {
        var renderer = Renderer(TemplateWithVariables(
            [Variable("value", type, required: true)],
            contentTemplate: "{{value}}"));

        var result = await renderer.RenderAsync(RenderRequest(new Dictionary<string, string> { ["value"] = input }), CancellationToken.None);

        Assert.Contains(result.Messages, message => message.Content.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Renderer_ShouldReplaceVariable()
    {
        var renderer = Renderer(TemplateWithVariables([Variable("name", PromptVariableType.String, required: true)]));

        var result = await renderer.RenderAsync(RenderRequest(new Dictionary<string, string> { ["name"] = "Ada" }), CancellationToken.None);

        Assert.Contains(result.Messages, message => message.Content.Contains("Ada", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Renderer_ShouldReplaceMultipleOccurrences()
    {
        var renderer = Renderer(TemplateWithVariables(
            [Variable("name", PromptVariableType.String, required: true)],
            contentTemplate: "{{name}} meets {{name}}."));

        var result = await renderer.RenderAsync(RenderRequest(new Dictionary<string, string> { ["name"] = "Ada" }), CancellationToken.None);

        Assert.Equal("Ada meets Ada.", result.Messages.Single(message => message.Role == PromptMessageRole.User).Content);
    }

    [Fact]
    public async Task Renderer_ShouldPreserveMessageOrder()
    {
        var template = new PromptTemplate(
            new PromptTemplateId("test"),
            "Test",
            PromptTemplateVersion.Parse("1.0"),
            null,
            "Test",
            [
                new PromptMessageTemplate(PromptMessageRole.User, "{{name}}", 2),
                new PromptMessageTemplate(PromptMessageRole.System, "system", 1),
                new PromptMessageTemplate(PromptMessageRole.Assistant, "assistant", 3)
            ],
            [Variable("name", PromptVariableType.String, required: true)],
            DateTimeOffset.UtcNow);
        var renderer = Renderer(template);

        var result = await renderer.RenderAsync(RenderRequest(new Dictionary<string, string> { ["name"] = "Ada" }), CancellationToken.None);

        Assert.Equal([PromptMessageRole.System, PromptMessageRole.User, PromptMessageRole.Assistant], result.Messages.Select(message => message.Role));
    }

    [Fact]
    public async Task Renderer_ShouldDetectUnresolvedPlaceholder()
    {
        var renderer = Renderer(TemplateWithVariables([]));

        await Assert.ThrowsAsync<PromptValidationException>(() =>
            renderer.RenderAsync(RenderRequest(EmptyVariables()), CancellationToken.None));
    }

    [Fact]
    public void PromptTemplate_ShouldRequireUserMessage()
    {
        Assert.Throws<ArgumentException>(() =>
            new PromptTemplate(
                new PromptTemplateId("test"),
                "Test",
                PromptTemplateVersion.Parse("1.0"),
                null,
                "Test",
                [new PromptMessageTemplate(PromptMessageRole.System, "system", 0)],
                [],
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Renderer_ShouldKeepTemplateIdAndVersion()
    {
        var renderer = Renderer(Template("test", "1.0"));

        var result = await renderer.RenderAsync(RenderRequest(new Dictionary<string, string> { ["name"] = "Ada" }), CancellationToken.None);

        Assert.Equal("test", result.TemplateId.Value);
        Assert.Equal("1.0", result.Version.ToString());
    }

    [Fact]
    public async Task Renderer_ShouldUseTimeProviderForRenderedAtUtc()
    {
        var now = new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
        var renderer = Renderer(Template("test", "1.0"), now);

        var result = await renderer.RenderAsync(RenderRequest(new Dictionary<string, string> { ["name"] = "Ada" }), CancellationToken.None);

        Assert.Equal(now, result.RenderedAtUtc);
    }

    [Fact]
    public async Task Renderer_ShouldPreserveCorrelationId()
    {
        var renderer = Renderer(Template("test", "1.0"));

        var result = await renderer.RenderAsync(
            new PromptRenderRequest(new PromptTemplateId("test"), "Test", new Dictionary<string, string> { ["name"] = "Ada" }, correlationId: "corr-1"),
            CancellationToken.None);

        Assert.Equal("corr-1", result.CorrelationId);
    }

    [Fact]
    public async Task Orchestrator_ShouldUsePromptEngineWhenTemplateIsProvided()
    {
        var chatProvider = new FakeChatProvider();
        using var provider = CreateProvider(chatProvider);

        await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(GenericChatRequest(), CancellationToken.None);

        Assert.Equal("custom system", chatProvider.LastRequest?.Messages[0].Content);
        Assert.Equal("custom user", chatProvider.LastRequest?.Messages[1].Content);
    }

    [Fact]
    public async Task Orchestrator_ShouldKeepLegacyPathWithoutTemplate()
    {
        var chatProvider = new FakeChatProvider();
        using var provider = CreateProvider(chatProvider);

        await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(new AIOrchestrationRequest("legacy system", "legacy user", "Legacy"), CancellationToken.None);

        Assert.Equal("legacy system", chatProvider.LastRequest?.Messages[0].Content);
        Assert.Equal("legacy user", chatProvider.LastRequest?.Messages[1].Content);
    }

    [Fact]
    public async Task Orchestrator_ShouldSendRenderedMessagesToProvider()
    {
        var chatProvider = new FakeChatProvider();
        using var provider = CreateProvider(chatProvider);

        await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(GenericChatRequest(), CancellationToken.None);

        Assert.Equal([ChatRole.System, ChatRole.User], chatProvider.LastRequest?.Messages.Select(message => message.Role));
    }

    [Fact]
    public async Task Orchestrator_ShouldNotCallProviderWhenRenderingFails()
    {
        var chatProvider = new FakeChatProvider();
        using var provider = CreateProvider(chatProvider);

        await Assert.ThrowsAsync<PromptValidationException>(() =>
            provider.GetRequiredService<IAIOrchestrator>()
                .ExecuteAsync(GenericChatRequest(extraVariables: new Dictionary<string, string> { ["undeclared"] = "value" }), CancellationToken.None));

        Assert.Equal(0, chatProvider.CallCount);
    }

    [Fact]
    public async Task Orchestrator_ShouldPropagateCancellationToken()
    {
        var chatProvider = new FakeChatProvider();
        using var provider = CreateProvider(chatProvider);
        using var cancellationTokenSource = new CancellationTokenSource();

        await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(GenericChatRequest(), cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, chatProvider.LastCancellationToken);
    }

    [Fact]
    public async Task Orchestrator_ShouldNotLogSensitiveVariableValues()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var template = TemplateWithVariables(
            [Variable("secret", PromptVariableType.String, required: true, isSensitive: true)],
            contentTemplate: "{{secret}}");
        var chatProvider = new FakeChatProvider();
        using var provider = CreateProvider(chatProvider, loggerProvider, [template]);

        await provider.GetRequiredService<IPromptRenderer>()
            .RenderAsync(RenderRequest(new Dictionary<string, string> { ["secret"] = "sensitive-value" }), CancellationToken.None);

        Assert.DoesNotContain("sensitive-value", string.Join(Environment.NewLine, loggerProvider.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Orchestrator_ShouldKeepSessionPropagationWithTemplate()
    {
        using var provider = CreateProvider(new FakeChatProvider());

        var response = await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(GenericChatRequest(sessionId: "session-1"), CancellationToken.None);

        Assert.Equal("session-1", response.SessionId);
    }

    [Fact]
    public async Task Orchestrator_ShouldKeepMetricsWithTemplate()
    {
        using var provider = CreateProvider(new FakeChatProvider());

        var response = await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(GenericChatRequest(), CancellationToken.None);

        Assert.True(response.TotalDuration >= TimeSpan.Zero);
        Assert.NotNull(response.ProviderDuration);
    }

    [Fact]
    public void DependencyInjection_ShouldResolvePromptRenderer()
    {
        using var provider = CreateProvider(new FakeChatProvider());

        Assert.NotNull(provider.GetRequiredService<IPromptRenderer>());
    }

    [Fact]
    public void DependencyInjection_ShouldResolvePromptTemplateRegistry()
    {
        using var provider = CreateProvider(new FakeChatProvider());

        Assert.NotNull(provider.GetRequiredService<IPromptTemplateRegistry>());
    }

    [Fact]
    public async Task DependencyInjection_ShouldExposeGenericChatTemplate()
    {
        using var provider = CreateProvider(new FakeChatProvider());

        var template = await provider.GetRequiredService<IPromptTemplateRegistry>()
            .GetLatestAsync(new PromptTemplateId("generic-chat"), CancellationToken.None);

        Assert.Equal("GenericChat", template.Scenario);
    }

    [Fact]
    public void DependencyInjection_ShouldBeCompatibleWithAIOrchestration()
    {
        using var provider = CreateProvider(new FakeChatProvider());

        Assert.NotNull(provider.GetRequiredService<IAIOrchestrator>());
        Assert.NotNull(provider.GetRequiredService<IPromptRenderer>());
    }

    private static PromptTemplate Template(string id, string version)
    {
        return TemplateWithVariables([Variable("name", PromptVariableType.String, required: true)], id, version);
    }

    private static PromptTemplate TemplateWithVariables(
        IReadOnlyList<PromptVariableDefinition> variables,
        string id = "test",
        string version = "1.0",
        string contentTemplate = "{{name}}")
    {
        return new PromptTemplate(
            new PromptTemplateId(id),
            "Test Template",
            PromptTemplateVersion.Parse(version),
            null,
            "Test",
            [
                new PromptMessageTemplate(PromptMessageRole.System, "system", 0),
                new PromptMessageTemplate(PromptMessageRole.User, contentTemplate, 1)
            ],
            variables,
            DateTimeOffset.UtcNow);
    }

    private static PromptVariableDefinition Variable(
        string name,
        PromptVariableType type,
        bool required,
        string? defaultValue = null,
        bool isSensitive = false,
        int? maxLength = null)
    {
        return new PromptVariableDefinition(name, type, required, defaultValue: defaultValue, isSensitive: isSensitive, maxLength: maxLength);
    }

    private static IPromptRenderer Renderer(PromptTemplate template, DateTimeOffset? now = null)
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(now ?? DateTimeOffset.UtcNow));
        services.AddSingleton<IPromptTemplateRegistry>(new InMemoryPromptTemplateRegistry([template]));
        services.AddTransient<IPromptRenderer, PromptRenderer>();
        return services.BuildServiceProvider(validateScopes: true).GetRequiredService<IPromptRenderer>();
    }

    private static PromptRenderRequest RenderRequest(IReadOnlyDictionary<string, string> variables)
    {
        return new PromptRenderRequest(new PromptTemplateId("test"), "Test", variables, correlationId: "corr-1");
    }

    private static IReadOnlyDictionary<string, string> EmptyVariables()
    {
        return new Dictionary<string, string>();
    }

    private static AIOrchestrationRequest GenericChatRequest(
        string? sessionId = null,
        IReadOnlyDictionary<string, string>? extraVariables = null)
    {
        var variables = new Dictionary<string, string>
        {
            ["systemInstruction"] = "custom system",
            ["userMessage"] = "custom user"
        };

        if (extraVariables is not null)
        {
            foreach (var variable in extraVariables)
            {
                variables[variable.Key] = variable.Value;
            }
        }

        return new AIOrchestrationRequest("ignored system", "fallback user", "GenericChat")
        {
            SessionId = sessionId,
            PromptTemplateId = new PromptTemplateId("generic-chat"),
            PromptTemplateVersion = PromptTemplateVersion.Parse("1.0"),
            PromptVariables = variables
        };
    }

    private static ServiceProvider CreateProvider(
        FakeChatProvider chatProvider,
        RecordingLoggerProvider? loggerProvider = null,
        IReadOnlyList<PromptTemplate>? templates = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            if (loggerProvider is not null)
            {
                builder.AddProvider(loggerProvider);
            }
        });
        services.AddSingleton<IChatProvider>(chatProvider);
        services.AddSingleton<IAIProviderMetadata>(new FakeProviderMetadata());
        if (templates is not null)
        {
            foreach (var template in templates)
            {
                services.AddSingleton(template);
            }
        }

        services.AddTradeMindAIOrchestration();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class FakeChatProvider : IChatProvider
    {
        public int CallCount { get; private set; }

        public ChatRequest? LastRequest { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            LastCancellationToken = cancellationToken;

            return Task.FromResult(new ChatResponse(
                "response",
                "Fake",
                "fake-model",
                new ChatUsage(1, 2, 3),
                "response-id",
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class FakeProviderMetadata : IAIProviderMetadata
    {
        public string ProviderName => "Fake";

        public AIProviderCapabilities Capabilities { get; } = new(
            SupportsChat: true,
            SupportsEmbeddings: false,
            SupportsStreaming: false,
            SupportsToolCalling: false,
            SupportsVision: false);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public IList<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            return new RecordingLogger(Messages);
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(IList<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
