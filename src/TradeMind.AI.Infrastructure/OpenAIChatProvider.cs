using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using TradeMind.AI.Abstractions;
using AbstractionChatMessage = TradeMind.AI.Abstractions.ChatMessage;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;

namespace TradeMind.AI.Infrastructure;

public sealed class OpenAIChatProvider(
    ChatClient chatClient,
    IOptions<AIOptions> options,
    ILogger<OpenAIChatProvider> logger)
    : IChatProvider
{
    public async Task<ChatResponse> CompleteAsync(
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Messages.Count == 0)
        {
            throw new ArgumentException("At least one chat message is required.", nameof(request));
        }

        var model = ResolveModel(request.Model, options.Value.OpenAI.ChatModel);
        var messages = request.Messages.Select(ToOpenAIMessage).ToArray();
        var completionOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = request.MaxOutputTokens,
            Temperature = request.Temperature
        };

        if (request.Metadata is not null)
        {
            foreach (var item in request.Metadata)
            {
                completionOptions.Metadata[item.Key] = item.Value;
            }
        }

        logger.LogInformation(
            "AI chat request started using provider {Provider} and model {Model} with {MessageCount} messages",
            OpenAIProviderNames.OpenAI,
            model,
            request.Messages.Count);

        try
        {
            var result = await chatClient.CompleteChatAsync(
                messages,
                completionOptions,
                cancellationToken);
            var completion = result.Value;
            var content = string.Concat(completion.Content.Select(part => part.Text));
            var usage = completion.Usage is null
                ? null
                : new ChatUsage(
                    completion.Usage.InputTokenCount,
                    completion.Usage.OutputTokenCount,
                    completion.Usage.TotalTokenCount);

            logger.LogInformation(
                "AI chat request completed using provider {Provider} and model {Model}",
                OpenAIProviderNames.OpenAI,
                completion.Model);

            return new ChatResponse(
                content,
                OpenAIProviderNames.OpenAI,
                completion.Model,
                usage,
                completion.Id,
                completion.CreatedAt);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "AI chat request failed using provider {Provider} and model {Model}",
                OpenAIProviderNames.OpenAI,
                model);
            throw new AIProviderException("The OpenAI chat request failed.", exception);
        }
    }

    private static OpenAIChatMessage ToOpenAIMessage(AbstractionChatMessage message) =>
        message.Role switch
        {
            ChatRole.System => new SystemChatMessage(message.Content),
            ChatRole.User => new UserChatMessage(message.Content),
            ChatRole.Assistant => new AssistantChatMessage(message.Content),
            ChatRole.Tool => new ToolChatMessage("tool", message.Content),
            _ => throw new ArgumentOutOfRangeException(nameof(message), "Unsupported chat role.")
        };

    private static string ResolveModel(string? requestedModel, string configuredModel) =>
        string.IsNullOrWhiteSpace(requestedModel) ? configuredModel : requestedModel.Trim();
}
