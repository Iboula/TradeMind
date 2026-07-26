namespace TradeMind.AI.Application;

public sealed record PromptRenderedMessage(
    PromptMessageRole Role,
    string Content);
