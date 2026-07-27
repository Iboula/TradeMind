using TradeMind.Identity.Domain.Tenancy;

namespace TradeMind.Identity.Domain.Organizations;

public sealed record Organization
{
    public Organization(
        OrganizationId id,
        TenantId tenantId,
        string name,
        string slug,
        OrganizationStatus status,
        DateTimeOffset createdAtUtc,
        long concurrencyVersion = 1)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 256) throw new ArgumentException("Organization name is required and must be at most 256 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug) || slug.Trim().Length > 128) throw new ArgumentException("Organization slug is required and must be at most 128 characters.", nameof(slug));
        if (concurrencyVersion < 1) throw new ArgumentOutOfRangeException(nameof(concurrencyVersion));
        if (createdAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(createdAtUtc));
        Id = id;
        TenantId = tenantId;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Status = status;
        CreatedAtUtc = createdAtUtc;
        ConcurrencyVersion = concurrencyVersion;
    }

    public OrganizationId Id { get; }
    public TenantId TenantId { get; }
    public string Name { get; }
    public string Slug { get; }
    public OrganizationStatus Status { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public long ConcurrencyVersion { get; }
}
