using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.Actors;
using TradeMind.Identity.Domain.Permissions;

namespace TradeMind.Identity.Application.Authorization;

public sealed class PermissionEvaluator : IAuthorizationService
{
    public AuthorizationDecision Evaluate(ActorIdentity actor, Permission permission)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.IsAuthenticated || actor.ActorType == ActorType.Anonymous) return AuthorizationDecision.Deny(permission, "Authentication is required.");
        return actor.Permissions.Contains(permission)
            ? AuthorizationDecision.Allow(permission)
            : AuthorizationDecision.Deny(permission);
    }
}
