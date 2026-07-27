using Microsoft.EntityFrameworkCore;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.ApiKeys;
using TradeMind.Identity.Infrastructure.Persistence;

namespace TradeMind.Identity.Infrastructure.Security;

public sealed class ApiKeyUsageRecorder(IdentityDbContext db) : IApiKeyUsageRecorder
{
    public async Task RecordUseAsync(ApiKeyId id, DateTimeOffset usedAtUtc, CancellationToken cancellationToken)
    {
        await db.ApiKeys.Where(key => key.Id == id.Value && key.Status == ApiKeyStatus.Active.ToString())
            .ExecuteUpdateAsync(setters => setters.SetProperty(key => key.LastUsedAtUtc, usedAtUtc), cancellationToken)
            .ConfigureAwait(false);
    }
}
