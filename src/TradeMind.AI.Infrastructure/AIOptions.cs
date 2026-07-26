using System.ComponentModel.DataAnnotations;

namespace TradeMind.AI.Infrastructure;

public sealed class AIOptions
{
    public const string SectionName = "AI";

    [Required]
    public string Provider { get; init; } = string.Empty;

    [Required]
    public OpenAIOptions OpenAI { get; init; } = new();
}

public sealed class OpenAIOptions
{
    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Required]
    public string ChatModel { get; init; } = string.Empty;

    [Required]
    public string EmbeddingModel { get; init; } = string.Empty;
}
