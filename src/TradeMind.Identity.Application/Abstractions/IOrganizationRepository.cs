using TradeMind.Identity.Domain.Organizations;

namespace TradeMind.Identity.Application.Abstractions;

public interface IOrganizationRepository
{
    Task<Organization?> GetAsync(OrganizationId id, CancellationToken cancellationToken);
    Task AddAsync(Organization organization, CancellationToken cancellationToken);
}
