namespace TradeMind.AI.Application;

public sealed record AIExecutionContextItemKey(string Name)
{
    public static AIExecutionContextItemKey MemoryChatMessages { get; } = Create("trademind.ai.memory.chat_messages");

    public static AIExecutionContextItemKey MemoryUsed { get; } = Create("trademind.ai.memory.used");

    public static AIExecutionContextItemKey MemorySelectedEntryCount { get; } = Create("trademind.ai.memory.selected_entry_count");

    public static AIExecutionContextItemKey KnowledgeChatMessages { get; } = Create("trademind.ai.knowledge.chat_messages");

    public static AIExecutionContextItemKey KnowledgeUsed { get; } = Create("trademind.ai.knowledge.used");

    public static AIExecutionContextItemKey KnowledgeCitationIds { get; } = Create("trademind.ai.knowledge.citation_ids");

    public static AIExecutionContextItemKey KnowledgeSelectedResultCount { get; } = Create("trademind.ai.knowledge.selected_result_count");

    public static AIExecutionContextItemKey KnowledgeRetrievalDuration { get; } = Create("trademind.ai.knowledge.retrieval_duration");

    public static AIExecutionContextItemKey Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new AIExecutionContextItemKey(name);
    }
}
