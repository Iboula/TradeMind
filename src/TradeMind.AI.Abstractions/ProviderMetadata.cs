namespace TradeMind.AI.Abstractions;

public sealed record AIProviderCapabilities(
    bool SupportsChat,
    bool SupportsEmbeddings,
    bool SupportsStreaming,
    bool SupportsToolCalling,
    bool SupportsVision);

public interface IAIProviderMetadata
{
    string ProviderName { get; }
    AIProviderCapabilities Capabilities { get; }
}
