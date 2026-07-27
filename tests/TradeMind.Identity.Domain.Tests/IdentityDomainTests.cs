using System.Security.Cryptography;
using TradeMind.Identity.Domain.Actors;
using TradeMind.Identity.Domain.ApiKeys;
using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Permissions;
using TradeMind.Identity.Domain.Roles;
using TradeMind.Identity.Domain.Tenancy;
using TradeMind.Identity.Domain.Users;

namespace TradeMind.Identity.Domain.Tests;

public sealed class IdentityDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void User_id_is_stable_and_rejects_email_values()
    {
        var id = new UserId("external-subject-1");
        Assert.Equal("external-subject-1", id.Value);
        Assert.Throws<ArgumentException>(() => new UserId("person@example.com"));
    }

    [Fact]
    public void Organization_and_tenant_are_distinct_value_objects()
    {
        var organization = new OrganizationId("org-1");
        var tenant = new TenantId("tenant-1");
        Assert.NotEqual(organization.Value, tenant.Value);
        Assert.Throws<ArgumentException>(() => new TenantId(" "));
    }

    [Fact]
    public void Permission_set_is_defensively_immutable_and_unknown_roles_grant_nothing()
    {
        var set = new PermissionSet([new Permission(TradeMindPermissions.ExecutionSessionsRead)]);
        Assert.True(set.Contains(TradeMindPermissions.ExecutionSessionsRead));
        Assert.DoesNotContain(new Permission("TradeMind.Unknown"), RolePermissionMapping.GetPermissions(["Unknown"]).Values);
        Assert.IsNotType<HashSet<Permission>>(set.Values);
    }

    [Fact]
    public void Roles_map_to_permissions_without_trusting_arbitrary_role_names()
    {
        var permissions = RolePermissionMapping.GetPermissions([TradeMindRoles.Auditor, "Admin"]);
        Assert.True(permissions.Contains(TradeMindPermissions.ExecutionSessionsReadAudit));
        Assert.False(permissions.Contains(TradeMindPermissions.ApiKeysCreate));
    }

    [Fact]
    public void Anonymous_actor_has_no_permissions()
    {
        var actor = ActorIdentity.Anonymous();
        Assert.False(actor.IsAuthenticated);
        Assert.Empty(actor.Permissions.Values);
        Assert.Equal(ActorType.Anonymous, actor.ActorType);
    }

    [Fact]
    public void Api_key_lifecycle_rejects_reactivation_after_revoke()
    {
        var key = CreateKey();
        var disabled = key.Disable();
        Assert.Equal(ApiKeyStatus.Disabled, disabled.Status);
        var enabled = disabled.Enable();
        Assert.Equal(ApiKeyStatus.Active, enabled.Status);
        var revoked = enabled.Revoke("actor-2", Now.AddMinutes(1), "rotation");
        Assert.Equal(ApiKeyStatus.Revoked, revoked.Status);
        Assert.Throws<InvalidOperationException>(() => revoked.Enable());
    }

    [Fact]
    public void Expired_disabled_and_revoked_keys_are_not_usable()
    {
        var permission = new Permission(TradeMindPermissions.ExecutionSessionsRead);
        var key = CreateKey(expiresAtUtc: Now.AddMinutes(1));
        Assert.True(key.IsUsableAt(Now, permission));
        Assert.False(key.IsUsableAt(Now.AddMinutes(2), permission));
        Assert.False(key.Disable().IsUsableAt(Now, permission));
        Assert.False(key.Revoke("actor", Now, "test").IsUsableAt(Now, permission));
    }

    [Fact]
    public void Rotation_changes_key_version_and_secret_hash_without_exposing_raw_secret()
    {
        var key = CreateKey();
        var rotated = key.Rotate(new ApiKeySecretHash("PBKDF2-SHA512", RandomNumberGenerator.GetBytes(16), RandomNumberGenerator.GetBytes(32), 210000), "public-2");
        Assert.Equal(2, rotated.KeyVersion);
        Assert.Equal("public-2", rotated.PublicKeyId);
    }

    [Fact]
    public void Actor_identity_requires_scope_for_authenticated_actors()
    {
        Assert.Throws<ArgumentException>(() => new ActorIdentity("actor", ActorType.HumanUser, new UserId("user"), null, null, null, null, "jwt", [], new PermissionSet(), null, Now, Now.AddHours(1), true));
    }

    [Fact]
    public void Api_key_secret_hash_defensively_copies_byte_arrays()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = RandomNumberGenerator.GetBytes(32);
        var value = new ApiKeySecretHash("PBKDF2-SHA512", salt, hash, 210000);
        var returned = value.Hash;
        returned[0] ^= 0xFF;
        Assert.NotEqual(returned[0], value.Hash[0]);
    }

    private static ApiKey CreateKey(DateTimeOffset? expiresAtUtc = null) => new(new ApiKeyId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")), "public-1",
        new OrganizationId("org-1"), new TenantId("tenant-1"), "key", null, ApiKeyStatus.Active,
        new ApiKeySecretHash("PBKDF2-SHA512", RandomNumberGenerator.GetBytes(16), RandomNumberGenerator.GetBytes(32), 210000),
        new PermissionSet([new Permission(TradeMindPermissions.ExecutionSessionsRead)]), Now, "actor-1", expiresAtUtc, null, null, null, null, 1);
}
