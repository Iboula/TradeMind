namespace TradeMind.Identity.Application.Abstractions;

public interface IIdentityClock
{
    DateTimeOffset UtcNow { get; }
}
