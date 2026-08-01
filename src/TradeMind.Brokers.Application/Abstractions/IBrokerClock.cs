namespace TradeMind.Brokers.Application.Abstractions;

public interface IBrokerClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class TimeProviderBrokerClock(TimeProvider timeProvider) : IBrokerClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
