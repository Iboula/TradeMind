using TradeMind.Identity.Domain.ApiKeys;
using TradeMind.Identity.Domain.Organizations;

namespace TradeMind.Identity.Application.Abstractions;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(ApiKeyId id, CancellationToken cancellationToken);
    Task<ApiKey?> GetByPublicKeyIdAsync(string publicKeyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApiKey>> SearchAsync(OrganizationId organizationId, CancellationToken cancellationToken);
    Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken);
    Task UpdateAsync(ApiKey apiKey, long expectedConcurrencyVersion, CancellationToken cancellationToken);
    Task<int> CountActiveAsync(OrganizationId organizationId, CancellationToken cancellationToken);
}
