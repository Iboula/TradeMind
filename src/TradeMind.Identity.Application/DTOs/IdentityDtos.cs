using TradeMind.Identity.Domain.ApiKeys;
using TradeMind.Identity.Domain.Actors;

namespace TradeMind.Identity.Application.DTOs;

public sealed record CurrentIdentityDto(
    string ActorId,
    string ActorType,
    string? UserId,
    string? ApiKeyId,
    string? OrganizationId,
    string? TenantId,
    string? DisplayName,
    string AuthenticationMethod,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool IsAuthenticated);

public sealed record ApiKeyDto(
    string Id,
    string PublicKeyId,
    string OrganizationId,
    string TenantId,
    string Name,
    string? Description,
    string Status,
    IReadOnlyList<string> Permissions,
    DateTimeOffset CreatedAtUtc,
    string CreatedByActorId,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? LastUsedAtUtc,
    DateTimeOffset? RevokedAtUtc,
    string? RevokedByActorId,
    string? RevocationReason,
    int KeyVersion,
    long ConcurrencyVersion);

public sealed record ApiKeySecretResponse(ApiKeyDto Key, string RawSecret);

public sealed record CreateApiKeyRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions,
    DateTimeOffset? ExpiresAtUtc);

public sealed record RotateApiKeyRequest(long ExpectedConcurrencyVersion);
public sealed record RevokeApiKeyRequest(long ExpectedConcurrencyVersion, string Reason);
public sealed record ApiKeyMutationRequest(long ExpectedConcurrencyVersion);

public static class IdentityDtoMapper
{
    public static CurrentIdentityDto ToDto(ActorIdentity actor) => new(actor.ActorId, actor.ActorType.ToString(), actor.UserId?.ToString(), actor.ApiKeyId?.ToString(),
        actor.OrganizationId?.ToString(), actor.TenantId?.ToString(), actor.DisplayName, actor.AuthenticationMethod, actor.Roles,
        actor.Permissions.Values.Select(permission => permission.Value).OrderBy(value => value, StringComparer.Ordinal).ToArray(), actor.IsAuthenticated);

    public static ApiKeyDto ToDto(ApiKey key) => new(key.Id.ToString(), key.PublicKeyId, key.OrganizationId.ToString(), key.TenantId.ToString(), key.Name,
        key.Description, key.Status.ToString(), key.Permissions.Values.Select(permission => permission.Value).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        key.CreatedAtUtc, key.CreatedByActorId, key.ExpiresAtUtc, key.LastUsedAtUtc, key.RevokedAtUtc, key.RevokedByActorId, key.RevocationReason,
        key.KeyVersion, key.ConcurrencyVersion);
}
