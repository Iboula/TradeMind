using System.Collections.ObjectModel;
using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Tenancy;

namespace TradeMind.Identity.Domain.Users;

public sealed record UserIdentity
{
    public UserIdentity(
        UserId id,
        string provider,
        string providerSubject,
        OrganizationId organizationId,
        TenantId tenantId,
        string? displayName,
        UserStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? lastSeenAtUtc,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(provider) || provider.Trim().Length > 128) throw new ArgumentException("Provider is required and must be at most 128 characters.", nameof(provider));
        if (string.IsNullOrWhiteSpace(providerSubject) || providerSubject.Trim().Length > 256) throw new ArgumentException("Provider subject is required and must be at most 256 characters.", nameof(providerSubject));
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        if (lastSeenAtUtc is { } seen) EnsureUtc(seen, nameof(lastSeenAtUtc));
        if (lastSeenAtUtc < createdAtUtc) throw new ArgumentException("Last seen cannot precede creation.", nameof(lastSeenAtUtc));
        Id = id;
        Provider = provider.Trim();
        ProviderSubject = providerSubject.Trim();
        OrganizationId = organizationId;
        TenantId = tenantId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        Status = status;
        CreatedAtUtc = createdAtUtc;
        LastSeenAtUtc = lastSeenAtUtc;
        Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata ?? new Dictionary<string, string>(), StringComparer.Ordinal));
    }

    public UserId Id { get; }
    public string Provider { get; }
    public string ProviderSubject { get; }
    public OrganizationId OrganizationId { get; }
    public TenantId TenantId { get; }
    public string? DisplayName { get; }
    public UserStatus Status { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset? LastSeenAtUtc { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; }

    public UserIdentity SeenAt(DateTimeOffset timestampUtc)
    {
        EnsureUtc(timestampUtc, nameof(timestampUtc));
        if (timestampUtc < CreatedAtUtc) throw new ArgumentException("Last seen cannot precede creation.", nameof(timestampUtc));
        return this with { LastSeenAtUtc = timestampUtc };
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", parameterName);
    }
}
