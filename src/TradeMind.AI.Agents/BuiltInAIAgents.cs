using TradeMind.AI.Application;
using TradeMind.AI.Memory;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Agents;

public static class BuiltInAIAgentPromptTemplates
{
    public static PromptTemplate TradingCoach { get; } = new(
        new PromptTemplateId("trading-coach-skeleton"),
        "Trading Coach Skeleton",
        new PromptTemplateVersion(0, 1),
        "Educational, provider-agnostic trading reflection prompt without advice or market access.",
        "TradingCoach",
        [
            new PromptMessageTemplate(
                PromptMessageRole.System,
                "You are an educational trading reflection assistant. Discuss learning frameworks and journaling only. Do not provide financial advice, market data, buy or sell signals, portfolio calculations, broker actions, or trade execution.",
                0),
            new PromptMessageTemplate(PromptMessageRole.User, "{{userMessage}}", 1)
        ],
        [
            new PromptVariableDefinition(
                "userMessage",
                PromptVariableType.String,
                required: true)
        ],
        new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero));
}

public sealed class GenericAssistantAgent : IAIAgent
{
    public AIAgentDefinition Definition { get; } = new(
        new AIAgentId("generic-assistant"),
        "Generic Assistant",
        "Provider-agnostic reference assistant with explicitly optional context engines and tools.",
        new AIAgentVersion(1, 0, 0),
        AIAgentAvailability.Enabled,
        new AIAgentCapabilities(
            supportsPromptTemplates: true,
            supportsMemory: true,
            supportsKnowledge: true,
            supportsTools: true,
            supportsConversation: true,
            supportsMultiTurn: true),
        new AIAgentPolicy(
            new AIAgentPromptPolicy(
                BuiltInPromptTemplates.GenericChat.Id,
                BuiltInPromptTemplates.GenericChat.Version,
                requiredVariables: ["userMessage"],
                allowRequestVariableOverride: true,
                maximumRenderedCharacters: 16_000),
            new AIAgentMemoryPolicy(
                enabled: true,
                required: false,
                defaultWindowOptions: new MemoryWindowOptions(
                    maxEntries: 20,
                    maxCharacters: 8_000),
                allowRequestOverride: true,
                failureMode: MemoryFailureMode.ContinueWithoutMemory),
            new AIAgentKnowledgePolicy(
                enabled: true,
                required: false,
                defaultMaxResults: 5,
                maxCharacters: 4_000,
                allowedFilters: ["sourceId", "sourceType"],
                allowExplicitQuery: true,
                includeCitations: true,
                failureMode: KnowledgeFailureMode.ContinueWithoutKnowledge),
            new AIAgentToolPolicy(
                enabled: true,
                allowedToolIds: [new AIToolId("echo"), new AIToolId("add-numbers")],
                maximumAllowedSideEffectLevel: AIToolSideEffectLevel.None,
                allowedScenarios: ["GenericChat"],
                defaultFailureMode: AIToolFailureMode.ContinueWithoutTool,
                maximumTimeout: TimeSpan.FromSeconds(5),
                requireIdempotencyKeyForWrites: true),
            maximumExecutionDuration: TimeSpan.FromSeconds(30),
            failureMode: AIAgentFailureMode.ContinueWithReducedCapabilities,
            maximumAllowedSideEffectLevel: AIToolSideEffectLevel.None,
            maximumContextCharacters: 20_000),
        supportedScenarios: ["GenericChat"],
        tags: ["generic", "reference"],
        metadata: new Dictionary<string, string>
        {
            ["classification"] = "general-purpose",
            ["execution-model"] = "declarative"
        });
}

public sealed class TradingCoachAgent : IAIAgent
{
    public AIAgentDefinition Definition { get; } = new(
        new AIAgentId("trading-coach"),
        "Trading Coach",
        "Development-only educational skeleton for future trading reflection capabilities.",
        new AIAgentVersion(0, 1, 0),
        AIAgentAvailability.DevelopmentOnly,
        new AIAgentCapabilities(
            supportsPromptTemplates: true,
            supportsConversation: true),
        new AIAgentPolicy(
            new AIAgentPromptPolicy(
                BuiltInAIAgentPromptTemplates.TradingCoach.Id,
                BuiltInAIAgentPromptTemplates.TradingCoach.Version,
                requiredVariables: ["userMessage"],
                maximumRenderedCharacters: 8_000),
            new AIAgentMemoryPolicy(),
            new AIAgentKnowledgePolicy(),
            new AIAgentToolPolicy(
                enabled: false,
                maximumAllowedSideEffectLevel: AIToolSideEffectLevel.None,
                allowExplicitInvocation: false),
            maximumExecutionDuration: TimeSpan.FromSeconds(20),
            failureMode: AIAgentFailureMode.FailClosed,
            maximumAllowedSideEffectLevel: AIToolSideEffectLevel.None,
            maximumContextCharacters: 8_000),
        supportedScenarios: ["TradingCoach"],
        tags: ["development", "education", "trading-coach"],
        metadata: new Dictionary<string, string>
        {
            ["classification"] = "educational-skeleton",
            ["market-access"] = "none",
            ["trading-actions"] = "none"
        });
}
