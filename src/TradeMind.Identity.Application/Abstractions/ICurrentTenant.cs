using TradeMind.Identity.Domain.Tenancy;

namespace TradeMind.Identity.Application.Abstractions;

public interface ICurrentTenant
{
    TenantContext? Context { get; }
}
