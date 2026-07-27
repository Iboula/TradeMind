using TradeMind.Identity.Domain.Actors;

namespace TradeMind.Identity.Application.Abstractions;

public interface ICurrentActor
{
    ActorIdentity Identity { get; }
}
