namespace TradeMind.AI.Infrastructure;

public sealed class AIConfigurationException(string message) : InvalidOperationException(message);

public sealed class AIProviderException : InvalidOperationException
{
    public AIProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
