namespace TradeMind.ExecutionSessions.Domain;

public sealed class ExecutionSessionDomainException : InvalidOperationException
{
    public ExecutionSessionDomainException(string message) : base(message)
    {
    }
}
