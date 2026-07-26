using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Infrastructure;

public sealed class OpenAIProviderMetadata : IAIProviderMetadata
{
    public string ProviderName => OpenAIProviderNames.OpenAI;

    public AIProviderCapabilities Capabilities { get; } = new(
        SupportsChat: true,
        SupportsEmbeddings: true,
        SupportsStreaming: false,
        SupportsToolCalling: false,
        SupportsVision: false);
}

internal static class OpenAIProviderNames
{
    public const string OpenAI = "OpenAI";
}
