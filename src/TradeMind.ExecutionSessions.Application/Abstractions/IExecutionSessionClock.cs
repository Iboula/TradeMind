namespace TradeMind.ExecutionSessions.Application.Abstractions;

public interface IExecutionSessionClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class TimeProviderExecutionSessionClock(TimeProvider timeProvider) : IExecutionSessionClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
