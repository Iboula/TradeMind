using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.ExecutionSessions.Application;

public sealed class ExecutionSessionNotFoundException(ExecutionSessionId id)
    : InvalidOperationException($"Execution session '{id}' was not found.")
{
    public ExecutionSessionId SessionId { get; } = id;
}

public sealed class ExecutionSessionConcurrencyException(ExecutionSessionId id, long expectedVersion)
    : InvalidOperationException($"Execution session '{id}' changed after version {expectedVersion} was read.")
{
    public ExecutionSessionId SessionId { get; } = id;
    public long ExpectedVersion { get; } = expectedVersion;
}

public sealed class ExecutionSessionTransitionException : InvalidOperationException
{
    public ExecutionSessionTransitionException(string message) : base(message)
    {
    }
}

public sealed class ExecutionSessionNotConfiguredException : InvalidOperationException
{
    public ExecutionSessionNotConfiguredException()
        : base("Execution session persistence is not configured for this host.")
    {
    }
}
