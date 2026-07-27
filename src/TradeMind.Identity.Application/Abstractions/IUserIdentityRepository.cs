using TradeMind.Identity.Domain.Users;

namespace TradeMind.Identity.Application.Abstractions;

public interface IUserIdentityRepository
{
    Task<UserIdentity?> GetByProviderSubjectAsync(string provider, string subject, CancellationToken cancellationToken);
    Task AddAsync(UserIdentity identity, CancellationToken cancellationToken);
    Task UpdateAsync(UserIdentity identity, long expectedConcurrencyVersion, CancellationToken cancellationToken);
}
