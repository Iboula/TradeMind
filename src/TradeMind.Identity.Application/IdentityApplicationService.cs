using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Application.DTOs;
using TradeMind.Identity.Domain.ApiKeys;
using TradeMind.Identity.Domain.Actors;
using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Permissions;
using TradeMind.Identity.Domain.Tenancy;

namespace TradeMind.Identity.Application;

public interface IIdentityApplicationService
{
    CurrentIdentityDto GetCurrentIdentity();
    Task<ApiKeySecretResponse> CreateApiKeyAsync(CreateApiKeyRequest request, string? environmentName, CancellationToken cancellationToken);
    Task<ApiKeySecretResponse> RotateApiKeyAsync(Guid id, RotateApiKeyRequest request, string? environmentName, CancellationToken cancellationToken);
    Task<ApiKeyDto> RevokeApiKeyAsync(Guid id, RevokeApiKeyRequest request, CancellationToken cancellationToken);
    Task<ApiKeyDto> DisableApiKeyAsync(Guid id, ApiKeyMutationRequest request, CancellationToken cancellationToken);
    Task<ApiKeyDto> EnableApiKeyAsync(Guid id, ApiKeyMutationRequest request, CancellationToken cancellationToken);
    Task<ApiKeyDto> GetApiKeyAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApiKeyDto>> SearchApiKeysAsync(CancellationToken cancellationToken);
}

public sealed class IdentityApplicationService(
    ICurrentActor currentActor,
    ICurrentTenant currentTenant,
    IApiKeyRepository apiKeys,
    IApiKeySecretGenerator secretGenerator,
    IIdentityClock clock,
    IIdentityAuditWriter audit,
    IIdentityUnitOfWork unitOfWork,
    IdentityOptions options) : IIdentityApplicationService
{
    public CurrentIdentityDto GetCurrentIdentity() => IdentityDtoMapper.ToDto(currentActor.Identity);

    public async Task<ApiKeySecretResponse> CreateApiKeyAsync(CreateApiKeyRequest request, string? environmentName, CancellationToken cancellationToken)
    {
        var actor = RequirePermission(TradeMindPermissions.ApiKeysCreate);
        var tenant = RequireTenant();
        ValidateExpiry(request.ExpiresAtUtc);
        var count = await apiKeys.CountActiveAsync(new OrganizationId(tenant.OrganizationId.Value), cancellationToken).ConfigureAwait(false);
        if (count >= options.ApiKeys.MaximumActiveKeysPerOrganization) throw new ApiKeyLimitExceededException();
        var material = secretGenerator.Generate(environmentName ?? "test");
        var now = clock.UtcNow;
        var key = new ApiKey(new ApiKeyId(Guid.NewGuid()), material.PublicKeyId, tenant.OrganizationId, tenant.TenantId, request.Name,
            request.Description, ApiKeyStatus.Active, material.Hash, new ApiKeyScope(ParsePermissions(request.Permissions)).Permissions, now,
            actor.ActorId, request.ExpiresAtUtc, null, null, null, null, 1);
        await unitOfWork.ExecuteAsync(async token =>
        {
            await apiKeys.AddAsync(key, token).ConfigureAwait(false);
            await audit.WriteAsync(Audit("ApiKeyCreated", actor, tenant, key.Id.ToString(), "SUCCESS", null), token).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return new ApiKeySecretResponse(IdentityDtoMapper.ToDto(key), material.RawSecret);
    }

    public async Task<ApiKeySecretResponse> RotateApiKeyAsync(Guid id, RotateApiKeyRequest request, string? environmentName, CancellationToken cancellationToken)
    {
        var actor = RequirePermission(TradeMindPermissions.ApiKeysRotate);
        var tenant = RequireTenant();
        var current = await GetOwnedAsync(id, tenant, cancellationToken).ConfigureAwait(false);
        var material = secretGenerator.Generate(environmentName ?? "test");
        var rotated = current.Rotate(material.Hash, material.PublicKeyId);
        await unitOfWork.ExecuteAsync(async token =>
        {
            await apiKeys.UpdateAsync(rotated, request.ExpectedConcurrencyVersion, token).ConfigureAwait(false);
            await audit.WriteAsync(Audit("ApiKeyRotated", actor, tenant, rotated.Id.ToString(), "SUCCESS", null), token).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return new ApiKeySecretResponse(IdentityDtoMapper.ToDto(rotated), material.RawSecret);
    }

    public Task<ApiKeyDto> RevokeApiKeyAsync(Guid id, RevokeApiKeyRequest request, CancellationToken cancellationToken) =>
        MutateAsync(id, request.ExpectedConcurrencyVersion, TradeMindPermissions.ApiKeysRevoke, "ApiKeyRevoked", key => key.Revoke(currentActor.Identity.ActorId, clock.UtcNow, request.Reason), cancellationToken);

    public Task<ApiKeyDto> DisableApiKeyAsync(Guid id, ApiKeyMutationRequest request, CancellationToken cancellationToken) =>
        MutateAsync(id, request.ExpectedConcurrencyVersion, TradeMindPermissions.ApiKeysRevoke, "ApiKeyDisabled", key => key.Disable(), cancellationToken);

    public Task<ApiKeyDto> EnableApiKeyAsync(Guid id, ApiKeyMutationRequest request, CancellationToken cancellationToken) =>
        MutateAsync(id, request.ExpectedConcurrencyVersion, TradeMindPermissions.ApiKeysRevoke, "ApiKeyEnabled", key => key.Enable(), cancellationToken);

    public async Task<ApiKeyDto> GetApiKeyAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = RequirePermission(TradeMindPermissions.ApiKeysRead);
        var tenant = RequireTenant();
        var key = await GetOwnedAsync(id, tenant, cancellationToken).ConfigureAwait(false);
        await audit.WriteAsync(Audit("ApiKeyRead", actor, tenant, key.Id.ToString(), "SUCCESS", null), cancellationToken).ConfigureAwait(false);
        return IdentityDtoMapper.ToDto(key);
    }

    public async Task<IReadOnlyList<ApiKeyDto>> SearchApiKeysAsync(CancellationToken cancellationToken)
    {
        RequirePermission(TradeMindPermissions.ApiKeysRead);
        var tenant = RequireTenant();
        var keys = await apiKeys.SearchAsync(tenant.OrganizationId, cancellationToken).ConfigureAwait(false);
        return keys.Where(key => key.TenantId == tenant.TenantId).OrderBy(key => key.PublicKeyId, StringComparer.Ordinal).Select(IdentityDtoMapper.ToDto).ToArray();
    }

    private async Task<ApiKeyDto> MutateAsync(Guid id, long expected, string permission, string eventType, Func<ApiKey, ApiKey> mutation, CancellationToken cancellationToken)
    {
        var actor = RequirePermission(permission);
        var tenant = RequireTenant();
        var current = await GetOwnedAsync(id, tenant, cancellationToken).ConfigureAwait(false);
        var updated = mutation(current);
        await unitOfWork.ExecuteAsync(async token =>
        {
            await apiKeys.UpdateAsync(updated, expected, token).ConfigureAwait(false);
            await audit.WriteAsync(Audit(eventType, actor, tenant, updated.Id.ToString(), "SUCCESS", null), token).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return IdentityDtoMapper.ToDto(updated);
    }

    private async Task<ApiKey> GetOwnedAsync(Guid id, TenantContext tenant, CancellationToken cancellationToken)
    {
        var key = await apiKeys.GetByIdAsync(new ApiKeyId(id), cancellationToken).ConfigureAwait(false);
        if (key is null || key.OrganizationId != tenant.OrganizationId || key.TenantId != tenant.TenantId) throw new ApiKeyNotFoundException(new ApiKeyId(id));
        return key;
    }

    private ActorIdentity RequirePermission(string permission)
    {
        var actor = currentActor.Identity;
        if (!actor.IsAuthenticated || !actor.Permissions.Contains(permission)) throw new IdentityAuthorizationException(permission);
        return actor;
    }

    private TenantContext RequireTenant() => currentTenant.Context ?? throw new IdentityAuthorizationException("TradeMind.Tenant.Select");

    private static IReadOnlyList<Permission> ParsePermissions(IEnumerable<string> permissions)
    {
        var allowed = TradeMindPermissions.All.ToDictionary(permission => permission.Value, StringComparer.Ordinal);
        return permissions.Select(value => value?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal)
            .Select(value => allowed.TryGetValue(value!, out var permission) ? permission : throw new ArgumentException($"Unknown permission '{value}'."))
            .ToArray();
    }

    private static void ValidateExpiry(DateTimeOffset? expiry)
    {
        if (expiry is { } value && value.Offset != TimeSpan.Zero) throw new ArgumentException("Expiration must be UTC.", nameof(expiry));
    }

    private IdentityAuditEntry Audit(string eventType, ActorIdentity actor, TenantContext tenant, string resourceId, string outcome, string? permission) =>
        new(eventType, clock.UtcNow, actor, tenant.OrganizationId, tenant.TenantId, "identity", outcome, permission, "ApiKey", resourceId, new Dictionary<string, string>());
}
