using TradeMind.Identity.Domain.Permissions;

namespace TradeMind.Identity.Application.Authorization;

public sealed record AuthorizationDecision(bool IsAllowed, string Code, Permission Permission, string? SafeReason)
{
    public static AuthorizationDecision Allow(Permission permission) => new(true, "AUTHORIZED", permission, null);
    public static AuthorizationDecision Deny(Permission permission, string reason = "The actor does not have the required permission.") => new(false, "FORBIDDEN", permission, reason);
}
