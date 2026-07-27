using TradeMind.Identity.Domain.Actors;
using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Tenancy;

namespace TradeMind.Identity.Application.Abstractions;

public interface IIdentityAuditWriter
{
    Task WriteAsync(IdentityAuditEntry entry, CancellationToken cancellationToken);
}

public sealed record IdentityAuditEntry(
    string EventType,
    DateTimeOffset OccurredAtUtc,
    ActorIdentity Actor,
    OrganizationId? OrganizationId,
    TenantId? TenantId,
    string CorrelationId,
    string Outcome,
    string? Permission,
    string ResourceType,
    string? ResourceId,
    IReadOnlyDictionary<string, string> Metadata);
