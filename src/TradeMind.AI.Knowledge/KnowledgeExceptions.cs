namespace TradeMind.AI.Knowledge;

public class KnowledgeRetrievalException : InvalidOperationException
{
    public KnowledgeRetrievalException(
        string message,
        string? correlationId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        CorrelationId = correlationId;
    }

    public string? CorrelationId { get; }
}

public sealed class KnowledgeContextValidationException : KnowledgeRetrievalException
{
    public KnowledgeContextValidationException(string message, string? correlationId = null)
        : base(message, correlationId)
    {
    }
}

public sealed class KnowledgeCompositionException : KnowledgeRetrievalException
{
    public KnowledgeCompositionException(string message, string? correlationId = null)
        : base(message, correlationId)
    {
    }
}
