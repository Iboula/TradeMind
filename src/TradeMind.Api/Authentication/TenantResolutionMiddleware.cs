using Microsoft.Extensions.Options;
using TradeMind.Api.Errors;
using TradeMind.Identity.Application;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.Actors;
using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Tenancy;
using TradeMind.Identity.Domain.Roles;

namespace TradeMind.Api.Authentication;

public sealed class TenantResolutionMiddleware(
    RequestDelegate next,
    ICurrentActor currentActor,
    IOptions<IdentityOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var actor = currentActor.Identity;
        if (actor.IsAuthenticated)
        {
            var configured = options.Value.Tenancy;
            var requested = context.Request.Headers[configured.SelectorHeaderName].FirstOrDefault();
            var selected = actor.TenantId!.Value.Value;
            var overrideUsed = false;
            if (!string.IsNullOrWhiteSpace(requested) && !string.Equals(requested, selected, StringComparison.Ordinal))
            {
                if (actor.Roles.Contains(TradeMindRoles.PlatformAdministrator, StringComparer.Ordinal) && configured.AllowPlatformAdministratorOverride)
                {
                    selected = requested.Trim();
                    overrideUsed = true;
                }
                else
                {
                    throw new TenantSelectionException();
                }
            }

            var tenant = new TenantContext(actor.OrganizationId!.Value, new TenantId(selected), actor, overrideUsed);
            context.Items[IdentityHttpContextKeys.Tenant] = tenant;
            context.Response.Headers[configured.ResponseHeaderName] = selected;
        }

        await next(context).ConfigureAwait(false);
    }
}

public sealed class TenantSelectionException : InvalidOperationException
{
    public TenantSelectionException() : base("The requested tenant is not authorized for this identity.") { }
}
