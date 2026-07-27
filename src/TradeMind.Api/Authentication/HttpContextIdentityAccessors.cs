using Microsoft.AspNetCore.Http;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.Actors;
using TradeMind.Identity.Domain.Tenancy;
using TradeMind.Identity.Infrastructure.Authentication.Jwt;

namespace TradeMind.Api.Authentication;

public static class IdentityHttpContextKeys
{
    public const string Actor = "TradeMind.Identity.Actor";
    public const string Tenant = "TradeMind.Identity.Tenant";
}

public sealed class HttpContextCurrentActor(IHttpContextAccessor accessor, IJwtClaimMapper jwtClaimMapper) : ICurrentActor
{
    public ActorIdentity Identity
    {
        get
        {
            var context = accessor.HttpContext;
            if (context is null || context.User.Identity?.IsAuthenticated != true) return ActorIdentity.Anonymous();
            if (context.Items[IdentityHttpContextKeys.Actor] is ActorIdentity actor) return actor;
            return jwtClaimMapper.Map(context.User);
        }
    }
}

public sealed class HttpContextCurrentTenant(IHttpContextAccessor accessor) : ICurrentTenant
{
    public TenantContext? Context => accessor.HttpContext?.Items[IdentityHttpContextKeys.Tenant] as TenantContext;
}
