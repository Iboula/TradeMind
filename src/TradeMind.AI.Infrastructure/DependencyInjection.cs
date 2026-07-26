using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAI.Embeddings;
using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var aiSection = configuration.GetSection(AIOptions.SectionName);
        services.AddOptions<AIOptions>()
            .Bind(aiSection)
            .ValidateDataAnnotations()
            .Validate(ValidateAIOptions, "The AI configuration is invalid.")
            .ValidateOnStart();

        var provider = aiSection[nameof(AIOptions.Provider)];
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new AIConfigurationException("AI:Provider is required.");
        }

        if (!provider.Equals(OpenAIProviderNames.OpenAI, StringComparison.OrdinalIgnoreCase))
        {
            throw new AIConfigurationException($"AI provider '{provider}' is not supported.");
        }

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AIOptions>>().Value.OpenAI;
            return new ChatClient(options.ChatModel, options.ApiKey);
        });
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AIOptions>>().Value.OpenAI;
            return new EmbeddingClient(options.EmbeddingModel, options.ApiKey);
        });
        services.AddSingleton<IChatProvider, OpenAIChatProvider>();
        services.AddSingleton<IEmbeddingProvider, OpenAIEmbeddingProvider>();
        services.AddSingleton<IAIProviderMetadata, OpenAIProviderMetadata>();
        return services;
    }

    private static bool ValidateAIOptions(AIOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            return false;
        }

        if (!options.Provider.Equals(OpenAIProviderNames.OpenAI, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(options.OpenAI.ApiKey)
            && !string.IsNullOrWhiteSpace(options.OpenAI.ChatModel)
            && !string.IsNullOrWhiteSpace(options.OpenAI.EmbeddingModel);
    }
}
