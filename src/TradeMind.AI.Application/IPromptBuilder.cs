using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Application;

public interface IPromptBuilder
{
    IPromptBuilder WithSystemMessage(string? content);

    IPromptBuilder AddUserMessage(string? content);

    IPromptBuilder AddAssistantMessage(string? content);

    IPromptBuilder AddContext(string? name, string? content);

    ChatRequest Build(
        string? model = null,
        float? temperature = null,
        int? maxOutputTokens = null,
        IReadOnlyDictionary<string, string>? metadata = null);
}
