using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Permissions;
using TradeMind.Identity.Domain.Tenancy;

namespace TradeMind.Identity.Domain.ApiKeys;

public sealed record ApiKey
{
    public ApiKey(
        ApiKeyId id,
        string publicKeyId,
        OrganizationId organizationId,
        TenantId tenantId,
        string name,
        string? description,
        ApiKeyStatus status,
        ApiKeySecretHash secretHash,
        PermissionSet permissions,
        DateTimeOffset createdAtUtc,
        string createdByActorId,
        DateTimeOffset? expiresAtUtc,
        DateTimeOffset? lastUsedAtUtc,
        DateTimeOffset? revokedAtUtc,
        string? revokedByActorId,
        string? revocationReason,
        int keyVersion,
        long concurrencyVersion = 1)
    {
        if (string.IsNullOrWhiteSpace(publicKeyId) || publicKeyId.Trim().Length > 128) throw new ArgumentException("Public key identifier is required and must be at most 128 characters.", nameof(publicKeyId));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 128) throw new ArgumentException("API key name is required and must be at most 128 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(createdByActorId)) throw new ArgumentException("Created-by actor is required.", nameof(createdByActorId));
        if (keyVersion < 1 || concurrencyVersion < 1) throw new ArgumentOutOfRangeException(nameof(keyVersion));
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        EnsureUtc(expiresAtUtc, nameof(expiresAtUtc));
        EnsureUtc(lastUsedAtUtc, nameof(lastUsedAtUtc));
        EnsureUtc(revokedAtUtc, nameof(revokedAtUtc));
        if (expiresAtUtc < createdAtUtc) throw new ArgumentException("Expiration cannot precede creation.", nameof(expiresAtUtc));
        Id = id;
        PublicKeyId = publicKeyId.Trim();
        OrganizationId = organizationId;
        TenantId = tenantId;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Status = status;
        SecretHash = secretHash ?? throw new ArgumentNullException(nameof(secretHash));
        Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        CreatedAtUtc = createdAtUtc;
        CreatedByActorId = createdByActorId.Trim();
        ExpiresAtUtc = expiresAtUtc;
        LastUsedAtUtc = lastUsedAtUtc;
        RevokedAtUtc = revokedAtUtc;
        RevokedByActorId = revokedByActorId;
        RevocationReason = revocationReason;
        KeyVersion = keyVersion;
        ConcurrencyVersion = concurrencyVersion;
    }

    public ApiKeyId Id { get; }
    public string PublicKeyId { get; init; }
    public OrganizationId OrganizationId { get; }
    public TenantId TenantId { get; }
    public string Name { get; }
    public string? Description { get; }
    public ApiKeyStatus Status { get; init; }
    public ApiKeySecretHash SecretHash { get; init; }
    public PermissionSet Permissions { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public string CreatedByActorId { get; }
    public DateTimeOffset? ExpiresAtUtc { get; }
    public DateTimeOffset? LastUsedAtUtc { get; init; }
    public DateTimeOffset? RevokedAtUtc { get; init; }
    public string? RevokedByActorId { get; init; }
    public string? RevocationReason { get; init; }
    public int KeyVersion { get; init; }
    public long ConcurrencyVersion { get; init; }

    public bool IsUsableAt(DateTimeOffset timestampUtc, Permission permission)
    {
        EnsureUtc(timestampUtc, nameof(timestampUtc));
        return Status == ApiKeyStatus.Active && (ExpiresAtUtc is null || ExpiresAtUtc > timestampUtc) && Permissions.Contains(permission);
    }

    public ApiKey Disable()
    {
        if (Status == ApiKeyStatus.Revoked) throw new InvalidOperationException("A revoked API key cannot be disabled.");
        return this with { Status = ApiKeyStatus.Disabled, ConcurrencyVersion = ConcurrencyVersion + 1 };
    }

    public ApiKey Enable()
    {
        if (Status == ApiKeyStatus.Revoked) throw new InvalidOperationException("A revoked API key cannot be enabled.");
        return this with { Status = ApiKeyStatus.Active, ConcurrencyVersion = ConcurrencyVersion + 1 };
    }

    public ApiKey Revoke(string actorId, DateTimeOffset revokedAtUtc, string reason)
    {
        if (Status == ApiKeyStatus.Revoked) throw new InvalidOperationException("The API key is already revoked.");
        EnsureUtc(revokedAtUtc, nameof(revokedAtUtc));
        if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Revocation actor and reason are required.");
        return this with { Status = ApiKeyStatus.Revoked, RevokedAtUtc = revokedAtUtc, RevokedByActorId = actorId.Trim(), RevocationReason = reason.Trim(), ConcurrencyVersion = ConcurrencyVersion + 1 };
    }

    public ApiKey UsedAt(DateTimeOffset usedAtUtc)
    {
        EnsureUtc(usedAtUtc, nameof(usedAtUtc));
        return this with { LastUsedAtUtc = usedAtUtc > (LastUsedAtUtc ?? CreatedAtUtc) ? usedAtUtc : LastUsedAtUtc };
    }

    public ApiKey Rotate(ApiKeySecretHash newHash, string newPublicKeyId)
    {
        if (Status == ApiKeyStatus.Revoked) throw new InvalidOperationException("A revoked API key cannot be rotated.");
        if (string.IsNullOrWhiteSpace(newPublicKeyId)) throw new ArgumentException("Public key identifier is required.", nameof(newPublicKeyId));
        return this with { SecretHash = newHash, PublicKeyId = newPublicKeyId.Trim(), KeyVersion = KeyVersion + 1, ConcurrencyVersion = ConcurrencyVersion + 1 };
    }

    private static void EnsureUtc(DateTimeOffset? value, string parameterName)
    {
        if (value is { } timestamp && timestamp.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", parameterName);
    }
}
