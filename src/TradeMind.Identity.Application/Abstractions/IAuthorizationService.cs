using TradeMind.Identity.Domain.Actors;
using TradeMind.Identity.Domain.Permissions;
using TradeMind.Identity.Application.Authorization;

namespace TradeMind.Identity.Application.Abstractions;

public interface IAuthorizationService
{
    AuthorizationDecision Evaluate(ActorIdentity actor, Permission permission);
}
