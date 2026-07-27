using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Infrastructure.Persistence;
using TradeMind.Identity.Infrastructure.Persistence.Entities;

namespace TradeMind.Identity.Infrastructure.Audit;

public sealed class IdentityAuditWriter(IdentityDbContext db) : IIdentityAuditWriter
{
    public Task WriteAsync(IdentityAuditEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Metadata.Keys.Any(key => key.Contains("secret", StringComparison.OrdinalIgnoreCase) || key.Contains("token", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Sensitive metadata is not allowed in identity audit entries.", nameof(entry));
        db.Audit.Add(new IdentityAuditEntity
        {
            Id = Guid.NewGuid(), EventType = entry.EventType, OccurredAtUtc = entry.OccurredAtUtc, ActorId = entry.Actor.ActorId,
            ActorType = entry.Actor.ActorType.ToString(), OrganizationId = entry.OrganizationId?.Value, TenantId = entry.TenantId?.Value,
            CorrelationId = entry.CorrelationId, Outcome = entry.Outcome, Permission = entry.Permission, ResourceType = entry.ResourceType,
            ResourceId = entry.ResourceId, MetadataJson = IdentityDbContext.Serialize(entry.Metadata)
        });
        return Task.CompletedTask;
    }
}
