namespace TradeMind.AI.Application;

public static class AIOrchestrationStepNames
{
    public const string RequestValidation = "ai.request.validation";
    public const string MemoryRead = "ai.memory.read";
    public const string KnowledgeRetrieval = "ai.knowledge.retrieval";
    public const string PromptConstruction = "ai.prompt.construction";
    public const string ToolExecution = "ai.tool.execution";
    public const string ToolResultComposition = "ai.tool.result.composition";
    public const string ProviderCapabilityValidation = "ai.provider.capability.validation";
    public const string ProviderExecution = "ai.provider.execution";
    public const string MemoryWrite = "ai.memory.write";
    public const string ResponseNormalization = "ai.response.normalization";
}
