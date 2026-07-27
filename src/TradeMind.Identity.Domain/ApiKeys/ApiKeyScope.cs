using TradeMind.Identity.Domain.Permissions;

namespace TradeMind.Identity.Domain.ApiKeys;

public sealed record ApiKeyScope(PermissionSet Permissions)
{
    public ApiKeyScope(IEnumerable<Permission> permissions) : this(new PermissionSet(permissions)) { }
}
