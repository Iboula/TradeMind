namespace TradeMind.AI.Memory;

public class MemoryOperationException : InvalidOperationException
{
    public MemoryOperationException(
        string message,
        ConversationMemoryKey? key = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Key = key;
    }

    public ConversationMemoryKey? Key { get; }
}

public sealed class MemoryValidationException : MemoryOperationException
{
    public MemoryValidationException(string message, ConversationMemoryKey? key = null)
        : base(message, key)
    {
    }
}

public sealed class ConversationMemoryNotFoundException : MemoryOperationException
{
    public ConversationMemoryNotFoundException(ConversationMemoryKey key)
        : base("Conversation memory was not found.", key)
    {
    }
}
