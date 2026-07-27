using System.Collections.Frozen;

namespace TradeMind.Identity.Domain.Permissions;

public sealed record PermissionSet
{
    private readonly FrozenSet<Permission> _permissions;

    public PermissionSet(IEnumerable<Permission>? permissions = null)
    {
        _permissions = (permissions ?? []).ToFrozenSet(EqualityComparer<Permission>.Default);
    }

    public IReadOnlySet<Permission> Values => _permissions;
    public bool Contains(Permission permission) => _permissions.Contains(permission);
    public bool Contains(string permission) => Contains(new Permission(permission));
    public bool IsEmpty => _permissions.Count == 0;
}
