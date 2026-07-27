using TradeMind.Identity.Domain.Permissions;

namespace TradeMind.Identity.Domain.Roles;

public sealed record Role
{
    public Role(string name, PermissionSet permissions)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Role name is required.", nameof(name));
        Name = name.Trim();
        Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }

    public string Name { get; }
    public PermissionSet Permissions { get; }
}
