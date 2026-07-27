using TradeMind.Identity.Application;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.ApiKeys;
using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Users;

namespace TradeMind.Identity.Infrastructure.Persistence;

public sealed class NotConfiguredApiKeyRepository : IApiKeyRepository
{
    private static IdentityPersistenceNotConfiguredException NotConfigured() => new();
    public Task<ApiKey?> GetByIdAsync(ApiKeyId id, CancellationToken cancellationToken) => Task.FromException<ApiKey?>(NotConfigured());
    public Task<ApiKey?> GetByPublicKeyIdAsync(string publicKeyId, CancellationToken cancellationToken) => Task.FromException<ApiKey?>(NotConfigured());
    public Task<IReadOnlyList<ApiKey>> SearchAsync(OrganizationId organizationId, CancellationToken cancellationToken) => Task.FromException<IReadOnlyList<ApiKey>>(NotConfigured());
    public Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken) => Task.FromException(NotConfigured());
    public Task UpdateAsync(ApiKey apiKey, long expectedConcurrencyVersion, CancellationToken cancellationToken) => Task.FromException(NotConfigured());
    public Task<int> CountActiveAsync(OrganizationId organizationId, CancellationToken cancellationToken) => Task.FromException<int>(NotConfigured());
}

public sealed class NotConfiguredIdentityUnitOfWork : IIdentityUnitOfWork
{
    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) => Task.FromException<T>(new IdentityPersistenceNotConfiguredException());
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.FromException(new IdentityPersistenceNotConfiguredException());
}

public sealed class NotConfiguredIdentityAuditWriter : IIdentityAuditWriter
{
    public Task WriteAsync(IdentityAuditEntry entry, CancellationToken cancellationToken) => Task.FromException(new IdentityPersistenceNotConfiguredException());
}

public sealed class NotConfiguredApiKeyUsageRecorder : IApiKeyUsageRecorder
{
    public Task RecordUseAsync(ApiKeyId id, DateTimeOffset usedAtUtc, CancellationToken cancellationToken) => Task.FromException(new IdentityPersistenceNotConfiguredException());
}

public sealed class NotConfiguredOrganizationRepository : IOrganizationRepository
{
    public Task<Organization?> GetAsync(OrganizationId id, CancellationToken cancellationToken) => Task.FromException<Organization?>(new IdentityPersistenceNotConfiguredException());
    public Task AddAsync(Organization organization, CancellationToken cancellationToken) => Task.FromException(new IdentityPersistenceNotConfiguredException());
}

public sealed class NotConfiguredUserIdentityRepository : IUserIdentityRepository
{
    public Task<UserIdentity?> GetByProviderSubjectAsync(string provider, string subject, CancellationToken cancellationToken) => Task.FromException<UserIdentity?>(new IdentityPersistenceNotConfiguredException());
    public Task AddAsync(UserIdentity identity, CancellationToken cancellationToken) => Task.FromException(new IdentityPersistenceNotConfiguredException());
    public Task UpdateAsync(UserIdentity identity, long expectedConcurrencyVersion, CancellationToken cancellationToken) => Task.FromException(new IdentityPersistenceNotConfiguredException());
}

public sealed class IdentityPersistenceNotConfiguredException : InvalidOperationException
{
    public IdentityPersistenceNotConfiguredException() : base("Identity PostgreSQL persistence is not configured.") { }
}
