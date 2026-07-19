namespace TradeMind.AI.Application;

public static class AIOrchestrationStepNames
{
    public const string RequestValidation = "ai.request.validation";
    public const string PromptConstruction = "ai.prompt.construction";
    public const string ProviderCapabilityValidation = "ai.provider.capability.validation";
    public const string ProviderExecution = "ai.provider.execution";
    public const string ResponseNormalization = "ai.response.normalization";
}
