namespace TradeMind.AI.Application;

public sealed record AIExecutionContextItemKey(string Name)
{
    public static AIExecutionContextItemKey Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new AIExecutionContextItemKey(name);
    }
}
