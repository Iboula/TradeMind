namespace TradeMind.AI.Application;

public static class BuiltInPromptTemplates
{
    public static PromptTemplate GenericChat { get; } = new(
        new PromptTemplateId("generic-chat"),
        "Generic Chat",
        new PromptTemplateVersion(1, 0),
        "Generic provider-agnostic chat prompt.",
        "GenericChat",
        [
            new PromptMessageTemplate(PromptMessageRole.System, "{{systemInstruction}}", 0),
            new PromptMessageTemplate(PromptMessageRole.User, "{{userMessage}}", 1)
        ],
        [
            new PromptVariableDefinition(
                "systemInstruction",
                PromptVariableType.String,
                required: false,
                defaultValue: "You are a helpful assistant."),
            new PromptVariableDefinition(
                "userMessage",
                PromptVariableType.String,
                required: true)
        ],
        new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero));
}
