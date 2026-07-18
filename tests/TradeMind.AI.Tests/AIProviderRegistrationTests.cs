using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Infrastructure;
using TradeMind.KnowledgeHub.Application;
using TradeMind.KnowledgeHub.Infrastructure;

namespace TradeMind.AI.Tests;

public sealed class AIProviderRegistrationTests
{
    [Fact]
    public void AddTradeMindAI_ShouldResolveChatProviderForOpenAI()
    {
        using var provider = CreateProvider(OpenAIConfiguration());

        var chatProvider = provider.GetRequiredService<IChatProvider>();

        Assert.IsType<OpenAIChatProvider>(chatProvider);
    }

    [Fact]
    public void AddTradeMindAI_ShouldResolveEmbeddingProviderForOpenAI()
    {
        using var provider = CreateProvider(OpenAIConfiguration());

        var embeddingProvider = provider.GetRequiredService<IEmbeddingProvider>();

        Assert.IsType<OpenAIEmbeddingProvider>(embeddingProvider);
    }

    [Fact]
    public void Metadata_ShouldExposeOpenAICapabilities()
    {
        using var provider = CreateProvider(OpenAIConfiguration());

        var metadata = provider.GetRequiredService<IAIProviderMetadata>();

        Assert.Equal("OpenAI", metadata.ProviderName);
        Assert.True(metadata.Capabilities.SupportsChat);
        Assert.True(metadata.Capabilities.SupportsEmbeddings);
        Assert.False(metadata.Capabilities.SupportsStreaming);
        Assert.False(metadata.Capabilities.SupportsToolCalling);
        Assert.False(metadata.Capabilities.SupportsVision);
    }

    [Fact]
    public void AddTradeMindAI_ShouldSelectProviderWithoutCaseSensitivity()
    {
        using var provider = CreateProvider(OpenAIConfiguration(providerName: "openai"));

        var metadata = provider.GetRequiredService<IAIProviderMetadata>();

        Assert.Equal("OpenAI", metadata.ProviderName);
    }

    [Fact]
    public void AddTradeMindAI_ShouldFailForUnknownProvider()
    {
        var exception = Assert.Throws<AIConfigurationException>(() =>
            CreateProvider(OpenAIConfiguration(providerName: "Unknown")));

        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddTradeMindAI_ShouldFailForEmptyProvider()
    {
        var exception = Assert.Throws<AIConfigurationException>(() =>
            CreateProvider(OpenAIConfiguration(providerName: string.Empty)));

        Assert.Contains("AI:Provider is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddTradeMindAI_ShouldFailValidationForIncompleteOpenAIConfiguration()
    {
        using var provider = CreateProvider(OpenAIConfiguration(apiKey: string.Empty));

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<AIOptions>>().Value);

        Assert.Contains("AI configuration", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task KnowledgeHubEmbeddingAdapter_ShouldUseAbstractEmbeddingProvider()
    {
        var embeddingProvider = new FakeEmbeddingProvider([0.25f, 0.75f]);
        var generator = new AIEmbeddingGeneratorAdapter(embeddingProvider);

        var vector = await generator.GenerateAsync("liquidity sweep", CancellationToken.None);

        Assert.Equal([0.25f, 0.75f], vector);
        Assert.Equal("liquidity sweep", embeddingProvider.LastText);
        Assert.IsAssignableFrom<IEmbeddingGenerator>(generator);
    }

    private static ServiceProvider CreateProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTradeMindAI(configuration);
        return services.BuildServiceProvider(validateScopes: true);
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

    private sealed class FakeEmbeddingProvider(IReadOnlyList<float> vector) : IEmbeddingProvider
    {
        public string? LastText { get; private set; }

        public Task<EmbeddingResponse> GenerateAsync(
            EmbeddingRequest request,
            CancellationToken cancellationToken)
        {
            LastText = request.Texts.Single();
            return Task.FromResult(new EmbeddingResponse(
                [vector],
                "Fake",
                "fake-embedding-model",
                null));
        }
    }
}
