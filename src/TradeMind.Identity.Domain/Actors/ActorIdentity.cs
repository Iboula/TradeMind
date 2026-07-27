using TradeMind.Identity.Domain.ApiKeys;
using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Permissions;
using TradeMind.Identity.Domain.Tenancy;
using TradeMind.Identity.Domain.Users;

namespace TradeMind.Identity.Domain.Actors;

public sealed record ActorIdentity
{
    public ActorIdentity(
        string actorId,
        ActorType actorType,
        UserId? userId,
        ApiKeyId? apiKeyId,
        OrganizationId? organizationId,
        TenantId? tenantId,
        string? displayName,
        string authenticationMethod,
        IEnumerable<string>? roles,
        PermissionSet permissions,
        string? tokenId,
        DateTimeOffset? authenticatedAtUtc,
        DateTimeOffset? expiresAtUtc,
        bool isAuthenticated)
    {
        if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Actor identifier is required.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(authenticationMethod)) throw new ArgumentException("Authentication method is required.", nameof(authenticationMethod));
        EnsureUtc(authenticatedAtUtc, nameof(authenticatedAtUtc));
        EnsureUtc(expiresAtUtc, nameof(expiresAtUtc));
        if (expiresAtUtc < authenticatedAtUtc) throw new ArgumentException("Expiration cannot precede authentication.", nameof(expiresAtUtc));
        if (actorType == ActorType.Anonymous && (isAuthenticated || permissions is not null && !permissions.IsEmpty)) throw new ArgumentException("Anonymous actors cannot be authenticated or receive permissions.", nameof(actorType));
        if (isAuthenticated && (organizationId is null || tenantId is null)) throw new ArgumentException("Authenticated actors require organization and tenant scope.", nameof(organizationId));
        ActorId = actorId.Trim();
        ActorType = actorType;
        UserId = userId;
        ApiKeyId = apiKeyId;
        OrganizationId = organizationId;
        TenantId = tenantId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        AuthenticationMethod = authenticationMethod.Trim();
        Roles = Array.AsReadOnly((roles ?? []).Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => role.Trim()).Distinct(StringComparer.Ordinal).OrderBy(role => role, StringComparer.Ordinal).ToArray());
        Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        TokenId = tokenId;
        AuthenticatedAtUtc = authenticatedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        IsAuthenticated = isAuthenticated;
    }

    public string ActorId { get; }
    public ActorType ActorType { get; }
    public UserId? UserId { get; }
    public ApiKeyId? ApiKeyId { get; }
    public OrganizationId? OrganizationId { get; }
    public TenantId? TenantId { get; }
    public string? DisplayName { get; }
    public string AuthenticationMethod { get; }
    public IReadOnlyList<string> Roles { get; }
    public PermissionSet Permissions { get; }
    public string? TokenId { get; }
    public DateTimeOffset? AuthenticatedAtUtc { get; }
    public DateTimeOffset? ExpiresAtUtc { get; }
    public bool IsAuthenticated { get; }

    public static ActorIdentity Anonymous() => new("anonymous", ActorType.Anonymous, null, null, null, null, null, "none", [], new PermissionSet(), null, null, null, false);

    private static void EnsureUtc(DateTimeOffset? value, string parameterName)
    {
        if (value is { } timestamp && timestamp.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", parameterName);
    }
}
