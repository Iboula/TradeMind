namespace TradeMind.AI.Application;

public sealed record AIExecutionContextItemKey(string Name)
{
    public static AIExecutionContextItemKey MemoryChatMessages { get; } = Create("trademind.ai.memory.chat_messages");

    public static AIExecutionContextItemKey MemoryUsed { get; } = Create("trademind.ai.memory.used");

    public static AIExecutionContextItemKey MemorySelectedEntryCount { get; } = Create("trademind.ai.memory.selected_entry_count");

    public static AIExecutionContextItemKey Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new AIExecutionContextItemKey(name);
    }
}
