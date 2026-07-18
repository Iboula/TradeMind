using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Application;
using TradeMind.AI.Infrastructure;

namespace TradeMind.AI.Tests;

public sealed class AIOrchestrationTests
{
    private const string ResponseContent = "normalized response";

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ShouldProduceResponse()
    {
        using var provider = CreateOrchestrationProvider();
        var orchestrator = provider.GetRequiredService<IAIOrchestrator>();

        var response = await orchestrator.ExecuteAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal(ResponseContent, response.Content);
        Assert.Equal("Fake", response.ProviderName);
        Assert.Equal("fake-chat-model", response.Model);
        Assert.Equal("response-1", response.ResponseId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallProviderOnce()
    {
        var chatProvider = new FakeChatProvider();
        using var provider = CreateOrchestrationProvider(chatProvider);

        await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal(1, chatProvider.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTransmitCancellationToken()
    {
        var chatProvider = new FakeChatProvider();
        using var provider = CreateOrchestrationProvider(chatProvider);
        using var cancellationTokenSource = new CancellationTokenSource();

        await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(ValidRequest(), cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, chatProvider.LastCancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExecuteStepsInOrder()
    {
        using var provider = CreateOrchestrationProvider();

        var response = await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal(
            [
                nameof(RequestValidationStep),
                nameof(PromptConstructionStep),
                nameof(ProviderCapabilityValidationStep),
                nameof(ProviderExecutionStep),
                nameof(ResponseNormalizationStep)
            ],
            response.ExecutedSteps);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExposeExecutedStepsInResponse()
    {
        using var provider = CreateOrchestrationProvider();

        var response = await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(ValidRequest(), CancellationToken.None);

        Assert.Contains(nameof(ProviderExecutionStep), response.ExecutedSteps);
        Assert.Contains(nameof(ResponseNormalizationStep), response.ExecutedSteps);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyUserMessage_ShouldFail()
    {
        using var provider = CreateOrchestrationProvider();

        var exception = await Assert.ThrowsAsync<AIOrchestrationValidationException>(() =>
            provider.GetRequiredService<IAIOrchestrator>()
                .ExecuteAsync(ValidRequest(userMessage: " "), CancellationToken.None));

        Assert.Contains("UserMessage", exception.Errors.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyScenario_ShouldFail()
    {
        using var provider = CreateOrchestrationProvider();

        var exception = await Assert.ThrowsAsync<AIOrchestrationValidationException>(() =>
            provider.GetRequiredService<IAIOrchestrator>()
                .ExecuteAsync(ValidRequest(scenario: " "), CancellationToken.None));

        Assert.Contains("Scenario", exception.Errors.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidTemperature_ShouldFail()
    {
        using var provider = CreateOrchestrationProvider();

        var exception = await Assert.ThrowsAsync<AIOrchestrationValidationException>(() =>
            provider.GetRequiredService<IAIOrchestrator>()
                .ExecuteAsync(ValidRequest(temperature: 3), CancellationToken.None));

        Assert.Contains("Temperature", exception.Errors.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidMaxOutputTokens_ShouldFail()
    {
        using var provider = CreateOrchestrationProvider();

        var exception = await Assert.ThrowsAsync<AIOrchestrationValidationException>(() =>
            provider.GetRequiredService<IAIOrchestrator>()
                .ExecuteAsync(ValidRequest(maxOutputTokens: 0), CancellationToken.None));

        Assert.Contains("MaxOutputTokens", exception.Errors.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderDoesNotSupportChat_ShouldFailExplicitly()
    {
        using var provider = CreateOrchestrationProvider(
            providerMetadata: new FakeProviderMetadata(supportsChat: false));

        var exception = await Assert.ThrowsAsync<AIProviderCapabilityException>(() =>
            provider.GetRequiredService<IAIOrchestrator>()
                .ExecuteAsync(ValidRequest(), CancellationToken.None));

        Assert.Equal("Fake", exception.ProviderName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderThrows_ShouldWrapExceptionAndPreserveInnerException()
    {
        var providerException = new InvalidOperationException("provider unavailable");
        using var provider = CreateOrchestrationProvider(new FakeChatProvider(providerException));

        var exception = await Assert.ThrowsAsync<AIOrchestrationException>(() =>
            provider.GetRequiredService<IAIOrchestrator>()
                .ExecuteAsync(ValidRequest(), CancellationToken.None));

        Assert.Same(providerException, exception.InnerException);
        Assert.Equal(nameof(ProviderExecutionStep), exception.StepName);
    }

    [Fact]
    public void PromptBuilder_ShouldPreserveMessageOrder()
    {
        var request = new PromptBuilder()
            .WithSystemMessage("system")
            .AddUserMessage("first user")
            .AddAssistantMessage("assistant")
            .AddContext("knowledge", "context")
            .AddUserMessage("second user")
            .Build();

        Assert.Equal(
            [ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.System, ChatRole.User],
            request.Messages.Select(message => message.Role));
        Assert.Equal("second user", request.Messages.Last().Content);
    }

    [Fact]
    public void PromptBuilder_WithoutUserMessage_ShouldFail()
    {
        var exception = Assert.Throws<AIOrchestrationValidationException>(() =>
            new PromptBuilder().WithSystemMessage("system").Build());

        Assert.Contains("user message", exception.Errors.Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotLogSecretsOrPromptContent()
    {
        const string secretPrompt = "sensitive journal content";
        const string metadataSecret = "test-api-key";
        var loggerProvider = new RecordingLoggerProvider();
        using var provider = CreateOrchestrationProvider(
            loggerProvider: loggerProvider);

        await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(
                ValidRequest(
                    userMessage: secretPrompt,
                    metadata: new Dictionary<string, string> { ["Secret"] = metadataSecret }),
                CancellationToken.None);

        var logs = string.Join(Environment.NewLine, loggerProvider.Messages);
        Assert.DoesNotContain(secretPrompt, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(metadataSecret, logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldKeepProvidedCorrelationId()
    {
        using var provider = CreateOrchestrationProvider();

        var response = await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(ValidRequest(correlationId: "corr-123"), CancellationToken.None);

        Assert.Equal("corr-123", response.CorrelationId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateCorrelationIdWhenAbsent()
    {
        using var provider = CreateOrchestrationProvider();

        var response = await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(ValidRequest(correlationId: null), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
        Assert.NotEqual("corr-123", response.CorrelationId);
    }

    [Fact]
    public void AddTradeMindAIOrchestration_ShouldResolveOrchestratorAndSteps()
    {
        using var provider = CreateOrchestrationProvider();

        Assert.NotNull(provider.GetRequiredService<IAIOrchestrator>());
        Assert.Equal(5, provider.GetServices<IAIOrchestrationStep>().Count());
    }

    [Fact]
    public void AddTradeMindAIOrchestration_ShouldRegisterPipelineOrder()
    {
        using var provider = CreateOrchestrationProvider();

        var orders = provider.GetServices<IAIOrchestrationStep>()
            .OrderBy(step => step.Order)
            .Select(step => step.Order);

        Assert.Equal([100, 200, 300, 400, 500], orders);
    }

    [Fact]
    public async Task AddTradeMindAIOrchestration_ShouldWorkWithInjectedFakeProvider()
    {
        var chatProvider = new FakeChatProvider();
        using var provider = CreateOrchestrationProvider(chatProvider);

        await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(ValidRequest(), CancellationToken.None);

        Assert.NotNull(chatProvider.LastRequest);
    }

    [Fact]
    public void AddTradeMindAI_ShouldStillFailForInvalidConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = OpenAIConfiguration(providerName: "Unknown");

        Assert.Throws<AIConfigurationException>(() => services.AddTradeMindAI(configuration));
    }

    [Fact]
    public void AddTradeMindAIOrchestration_ShouldBeCompatibleWithAddTradeMindAI()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTradeMindAI(OpenAIConfiguration());
        services.AddSingleton<IChatProvider>(new FakeChatProvider());
        services.AddSingleton<IAIProviderMetadata>(new FakeProviderMetadata());
        services.AddTradeMindAIOrchestration();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider.GetRequiredService<IAIOrchestrator>());
    }

    private static ServiceProvider CreateOrchestrationProvider(
        FakeChatProvider? chatProvider = null,
        IAIProviderMetadata? providerMetadata = null,
        RecordingLoggerProvider? loggerProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            if (loggerProvider is not null)
            {
                builder.AddProvider(loggerProvider);
            }
        });
        services.AddSingleton<IChatProvider>(chatProvider ?? new FakeChatProvider());
        services.AddSingleton(providerMetadata ?? new FakeProviderMetadata());
        services.AddTradeMindAIOrchestration();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static AIOrchestrationRequest ValidRequest(
        string userMessage = "Summarize the setup.",
        string scenario = "TradingCoach",
        float? temperature = 0.2f,
        int? maxOutputTokens = 256,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? correlationId = "corr-123")
    {
        return new AIOrchestrationRequest(
            "Answer with concise risk-aware language.",
            userMessage,
            scenario,
            "logical-fast",
            temperature,
            maxOutputTokens,
            metadata,
            correlationId);
    }

    private static IConfiguration OpenAIConfiguration(
        string providerName = "OpenAI",
        string apiKey = "test-api-key",
        string chatModel = "gpt-4.1-mini",
        string embeddingModel = "text-embedding-3-small")
    {
        var values = new Dictionary<string, string?>
        {
            ["AI:Provider"] = providerName,
            ["AI:OpenAI:ApiKey"] = apiKey,
            ["AI:OpenAI:ChatModel"] = chatModel,
            ["AI:OpenAI:EmbeddingModel"] = embeddingModel
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class FakeChatProvider(Exception? exception = null) : IChatProvider
    {
        public int CallCount { get; private set; }

        public ChatRequest? LastRequest { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            LastCancellationToken = cancellationToken;

            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(new ChatResponse(
                ResponseContent,
                "Fake",
                "fake-chat-model",
                new ChatUsage(10, 4, 14),
                "response-1",
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class FakeProviderMetadata(bool supportsChat = true) : IAIProviderMetadata
    {
        public string ProviderName => "Fake";

        public AIProviderCapabilities Capabilities { get; } = new(
            supportsChat,
            SupportsEmbeddings: false,
            SupportsStreaming: false,
            SupportsToolCalling: false,
            SupportsVision: false);
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
