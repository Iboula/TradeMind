using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.Actors;
using TradeMind.Identity.Domain.ApiKeys;
using TradeMind.Identity.Domain.Permissions;
using TradeMind.Identity.Infrastructure.Persistence.Repositories;

namespace TradeMind.Identity.Infrastructure.Authentication.ApiKeys;

public sealed class ApiKeyCredentialValidator(
    IApiKeyRepository repository,
    IApiKeyHasher hasher,
    IIdentityClock clock,
    IApiKeyUsageRecorder usageRecorder) : IApiKeyCredentialValidator
{
    public async Task<ActorIdentity?> ValidateAsync(string publicKeyId, string rawSecret, CancellationToken cancellationToken)
    {
        var key = await repository.GetByPublicKeyIdAsync(publicKeyId, cancellationToken).ConfigureAwait(false);
        if (key is null || key.Permissions.IsEmpty || !hasher.Verify(rawSecret, key.SecretHash) || key.Status != ApiKeyStatus.Active || key.ExpiresAtUtc is { } expiry && expiry <= clock.UtcNow) return null;
        try
        {
            await usageRecorder.RecordUseAsync(key.Id, clock.UtcNow, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Authentication remains available when the non-critical last-used update is unavailable.
        }
        return new ActorIdentity(key.Id.ToString(), ActorType.ApiKey, null, key.Id, key.OrganizationId, key.TenantId, key.Name, "api-key", [], key.Permissions, null, clock.UtcNow, key.ExpiresAtUtc, true);
    }
}
