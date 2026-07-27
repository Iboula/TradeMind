namespace TradeMind.Identity.Infrastructure.Persistence.Entities;

public sealed class OrganizationEntity
{
    public string Id { get; set; } = null!;
    public string TenantId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public long ConcurrencyVersion { get; set; }
}

public sealed class UserIdentityEntity
{
    public string Id { get; set; } = null!;
    public string Provider { get; set; } = null!;
    public string ProviderSubject { get; set; } = null!;
    public string OrganizationId { get; set; } = null!;
    public string TenantId { get; set; } = null!;
    public string? DisplayName { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastSeenAtUtc { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public long ConcurrencyVersion { get; set; }
}

public sealed class ApiKeyEntity
{
    public Guid Id { get; set; }
    public string PublicKeyId { get; set; } = null!;
    public string OrganizationId { get; set; } = null!;
    public string TenantId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public string SecretAlgorithm { get; set; } = null!;
    public byte[] SecretSalt { get; set; } = [];
    public byte[] SecretHash { get; set; } = [];
    public int SecretIterations { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedByActorId { get; set; } = null!;
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset? LastUsedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? RevokedByActorId { get; set; }
    public string? RevocationReason { get; set; }
    public int KeyVersion { get; set; }
    public long ConcurrencyVersion { get; set; }
    public List<ApiKeyPermissionEntity> Permissions { get; set; } = [];
}

public sealed class ApiKeyPermissionEntity
{
    public Guid ApiKeyId { get; set; }
    public string Permission { get; set; } = null!;
    public ApiKeyEntity ApiKey { get; set; } = null!;
}

public sealed class IdentityAuditEntity
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string ActorId { get; set; } = null!;
    public string ActorType { get; set; } = null!;
    public string? OrganizationId { get; set; }
    public string? TenantId { get; set; }
    public string CorrelationId { get; set; } = null!;
    public string Outcome { get; set; } = null!;
    public string? Permission { get; set; }
    public string ResourceType { get; set; } = null!;
    public string? ResourceId { get; set; }
    public string MetadataJson { get; set; } = "{}";
}
