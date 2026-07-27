using TradeMind.Identity.Domain.Actors;
using TradeMind.Identity.Domain.Organizations;

namespace TradeMind.Identity.Domain.Tenancy;

public sealed record TenantContext
{
    public TenantContext(OrganizationId organizationId, TenantId tenantId, ActorIdentity actor, bool isAdministrativeOverride = false)
    {
        if (!actor.IsAuthenticated && isAdministrativeOverride) throw new ArgumentException("Anonymous actors cannot override tenant scope.", nameof(actor));
        OrganizationId = organizationId;
        TenantId = tenantId;
        Actor = actor;
        IsAdministrativeOverride = isAdministrativeOverride;
    }

    public OrganizationId OrganizationId { get; }
    public TenantId TenantId { get; }
    public ActorIdentity Actor { get; }
    public bool IsAdministrativeOverride { get; }
}
