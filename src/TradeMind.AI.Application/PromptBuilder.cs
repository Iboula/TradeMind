using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Application;

public sealed class PromptBuilder : IPromptBuilder
{
    private readonly List<ChatMessage> _messages = [];

    public IPromptBuilder WithSystemMessage(string? content)
    {
        AddMessage(ChatRole.System, content);
        return this;
    }

    public IPromptBuilder AddUserMessage(string? content)
    {
        AddMessage(ChatRole.User, content);
        return this;
    }

    public IPromptBuilder AddAssistantMessage(string? content)
    {
        AddMessage(ChatRole.Assistant, content);
        return this;
    }

    public IPromptBuilder AddContext(string? name, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return this;
        }

        var contextLines = string.IsNullOrWhiteSpace(name)
            ? [content.Trim()]
            : new[] { $"Context: {name.Trim()}", content.Trim() };

        _messages.Add(new ChatMessage(ChatRole.System, string.Join(Environment.NewLine, contextLines)));
        return this;
    }

    public ChatRequest Build(
        string? model = null,
        float? temperature = null,
        int? maxOutputTokens = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (!_messages.Any(message => message.Role == ChatRole.User))
        {
            throw new AIOrchestrationValidationException(
                ["At least one user message is required to build a chat request."]);
        }

        return new ChatRequest(
            _messages.ToArray(),
            string.IsNullOrWhiteSpace(model) ? null : model,
            temperature,
            maxOutputTokens,
            metadata);
    }

    private void AddMessage(ChatRole role, string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            _messages.Add(new ChatMessage(role, content.Trim()));
        }
    }
}
