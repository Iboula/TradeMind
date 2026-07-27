using Microsoft.EntityFrameworkCore;
using TradeMind.Identity.Application;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.ApiKeys;
using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Users;

namespace TradeMind.Identity.Infrastructure.Persistence.Repositories;

public sealed class ApiKeyRepository(IdentityDbContext db) : IApiKeyRepository
{
    public async Task<ApiKey?> GetByIdAsync(ApiKeyId id, CancellationToken cancellationToken) =>
        await db.ApiKeys.AsNoTracking().Include(key => key.Permissions).SingleOrDefaultAsync(key => key.Id == id.Value, cancellationToken).ConfigureAwait(false) is { } entity
            ? IdentityMappings.ToDomain(entity) : null;

    public async Task<ApiKey?> GetByPublicKeyIdAsync(string publicKeyId, CancellationToken cancellationToken) =>
        await db.ApiKeys.Include(key => key.Permissions).SingleOrDefaultAsync(key => key.PublicKeyId == publicKeyId, cancellationToken).ConfigureAwait(false) is { } entity
            ? IdentityMappings.ToDomain(entity) : null;

    public async Task<IReadOnlyList<ApiKey>> SearchAsync(OrganizationId organizationId, CancellationToken cancellationToken) =>
        (await db.ApiKeys.AsNoTracking().Include(key => key.Permissions).Where(key => key.OrganizationId == organizationId.Value).OrderBy(key => key.PublicKeyId).ToListAsync(cancellationToken).ConfigureAwait(false)).Select(IdentityMappings.ToDomain).ToArray();

    public Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken) => db.ApiKeys.AddAsync(IdentityMappings.ToEntity(apiKey), cancellationToken).AsTask();

    public async Task UpdateAsync(ApiKey apiKey, long expectedConcurrencyVersion, CancellationToken cancellationToken)
    {
        var entity = await db.ApiKeys.Include(key => key.Permissions).SingleOrDefaultAsync(key => key.Id == apiKey.Id.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new ApiKeyNotFoundException(apiKey.Id);
        entity.PublicKeyId = apiKey.PublicKeyId;
        entity.Status = apiKey.Status.ToString();
        entity.SecretAlgorithm = apiKey.SecretHash.Algorithm;
        entity.SecretSalt = apiKey.SecretHash.Salt.ToArray();
        entity.SecretHash = apiKey.SecretHash.Hash.ToArray();
        entity.SecretIterations = apiKey.SecretHash.Iterations;
        entity.LastUsedAtUtc = apiKey.LastUsedAtUtc;
        entity.RevokedAtUtc = apiKey.RevokedAtUtc;
        entity.RevokedByActorId = apiKey.RevokedByActorId;
        entity.RevocationReason = apiKey.RevocationReason;
        entity.KeyVersion = apiKey.KeyVersion;
        entity.ConcurrencyVersion = apiKey.ConcurrencyVersion;
        db.Entry(entity).Property(key => key.ConcurrencyVersion).OriginalValue = expectedConcurrencyVersion;
        db.ApiKeyPermissions.RemoveRange(entity.Permissions);
        entity.Permissions = apiKey.Permissions.Values.Select(permission => new Entities.ApiKeyPermissionEntity { ApiKeyId = apiKey.Id.Value, Permission = permission.Value }).ToList();
        await db.ApiKeyPermissions.AddRangeAsync(entity.Permissions, cancellationToken).ConfigureAwait(false);
    }

    public Task<int> CountActiveAsync(OrganizationId organizationId, CancellationToken cancellationToken) =>
        db.ApiKeys.CountAsync(key => key.OrganizationId == organizationId.Value && key.Status == ApiKeyStatus.Active.ToString(), cancellationToken);
}

public sealed class OrganizationRepository(IdentityDbContext db) : IOrganizationRepository
{
    public async Task<Organization?> GetAsync(OrganizationId id, CancellationToken cancellationToken) =>
        await db.Organizations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken).ConfigureAwait(false) is { } entity
            ? IdentityMappings.ToDomain(entity) : null;

    public Task AddAsync(Organization organization, CancellationToken cancellationToken) => db.Organizations.AddAsync(IdentityMappings.ToEntity(organization), cancellationToken).AsTask();
}

public sealed class UserIdentityRepository(IdentityDbContext db) : IUserIdentityRepository
{
    public async Task<UserIdentity?> GetByProviderSubjectAsync(string provider, string subject, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Provider == provider && item.ProviderSubject == subject, cancellationToken).ConfigureAwait(false) is { } entity
            ? IdentityMappings.ToDomain(entity) : null;
    public Task AddAsync(UserIdentity identity, CancellationToken cancellationToken) => db.Users.AddAsync(IdentityMappings.ToEntity(identity), cancellationToken).AsTask();
    public Task UpdateAsync(UserIdentity identity, long expectedConcurrencyVersion, CancellationToken cancellationToken) => throw new NotSupportedException("User mapping updates are composed through the identity unit of work.");
}
