using TradeMind.Identity.Domain.ApiKeys;
using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Permissions;
using TradeMind.Identity.Domain.Users;
using TradeMind.Identity.Domain.Tenancy;
using TradeMind.Identity.Infrastructure.Persistence.Entities;

namespace TradeMind.Identity.Infrastructure.Persistence;

internal static class IdentityMappings
{
    public static ApiKeyEntity ToEntity(ApiKey value) => new()
    {
        Id = value.Id.Value, PublicKeyId = value.PublicKeyId, OrganizationId = value.OrganizationId.Value, TenantId = value.TenantId.Value,
        Name = value.Name, Description = value.Description, Status = value.Status.ToString(), SecretAlgorithm = value.SecretHash.Algorithm,
        SecretSalt = value.SecretHash.Salt.ToArray(), SecretHash = value.SecretHash.Hash.ToArray(), SecretIterations = value.SecretHash.Iterations,
        CreatedAtUtc = value.CreatedAtUtc, CreatedByActorId = value.CreatedByActorId, ExpiresAtUtc = value.ExpiresAtUtc, LastUsedAtUtc = value.LastUsedAtUtc,
        RevokedAtUtc = value.RevokedAtUtc, RevokedByActorId = value.RevokedByActorId, RevocationReason = value.RevocationReason,
        KeyVersion = value.KeyVersion, ConcurrencyVersion = value.ConcurrencyVersion,
        Permissions = value.Permissions.Values.Select(permission => new ApiKeyPermissionEntity { ApiKeyId = value.Id.Value, Permission = permission.Value }).ToList()
    };

    public static ApiKey ToDomain(ApiKeyEntity value) => new(new ApiKeyId(value.Id), value.PublicKeyId, new OrganizationId(value.OrganizationId), new TenantId(value.TenantId),
        value.Name, value.Description, Enum.Parse<ApiKeyStatus>(value.Status), new ApiKeySecretHash(value.SecretAlgorithm, value.SecretSalt, value.SecretHash, value.SecretIterations),
        new PermissionSet(value.Permissions.Select(permission => new Permission(permission.Permission))), value.CreatedAtUtc, value.CreatedByActorId, value.ExpiresAtUtc,
        value.LastUsedAtUtc, value.RevokedAtUtc, value.RevokedByActorId, value.RevocationReason, value.KeyVersion, value.ConcurrencyVersion);

    public static OrganizationEntity ToEntity(Organization value) => new() { Id = value.Id.Value, TenantId = value.TenantId.Value, Name = value.Name, Slug = value.Slug, Status = value.Status.ToString(), CreatedAtUtc = value.CreatedAtUtc, ConcurrencyVersion = value.ConcurrencyVersion };
    public static Organization ToDomain(OrganizationEntity value) => new(new OrganizationId(value.Id), new TenantId(value.TenantId), value.Name, value.Slug, Enum.Parse<OrganizationStatus>(value.Status), value.CreatedAtUtc, value.ConcurrencyVersion);
    public static UserIdentityEntity ToEntity(UserIdentity value) => new() { Id = value.Id.Value, Provider = value.Provider, ProviderSubject = value.ProviderSubject, OrganizationId = value.OrganizationId.Value, TenantId = value.TenantId.Value, DisplayName = value.DisplayName, Status = value.Status.ToString(), CreatedAtUtc = value.CreatedAtUtc, LastSeenAtUtc = value.LastSeenAtUtc, MetadataJson = IdentityDbContext.Serialize(value.Metadata), ConcurrencyVersion = 1 };
    public static UserIdentity ToDomain(UserIdentityEntity value) => new(new UserId(value.Id), value.Provider, value.ProviderSubject, new OrganizationId(value.OrganizationId), new TenantId(value.TenantId), value.DisplayName, Enum.Parse<UserStatus>(value.Status), value.CreatedAtUtc, value.LastSeenAtUtc, IdentityDbContext.Deserialize(value.MetadataJson));
}
