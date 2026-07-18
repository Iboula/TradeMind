namespace TradeMind.AI.Application;

public sealed record AIOrchestrationContextItemKey(string Name)
{
    public static AIOrchestrationContextItemKey Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new AIOrchestrationContextItemKey(name);
    }
}
