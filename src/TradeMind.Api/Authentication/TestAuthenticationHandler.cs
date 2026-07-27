using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TradeMind.Identity.Domain.Actors;
using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Permissions;
using TradeMind.Identity.Domain.Tenancy;

namespace TradeMind.Api.Authentication;

public sealed class TestAuthenticationOptions : AuthenticationSchemeOptions;

#pragma warning disable CS0618
public sealed class TestAuthenticationHandler(
    IOptionsMonitor<TestAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TimeProvider timeProvider) : AuthenticationHandler<TestAuthenticationOptions>(options, logger, encoder, new TimeProviderSystemClock(timeProvider))
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-TradeMind-Test-Actor", out var values) || string.IsNullOrWhiteSpace(values.SingleOrDefault())) return Task.FromResult(AuthenticateResult.NoResult());
        var fields = values.Single()!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(value => value.Length == 2)
            .ToDictionary(value => value[0], value => value[1], StringComparer.OrdinalIgnoreCase);
        if (!fields.TryGetValue("actor", out var actorId) || !fields.TryGetValue("organization", out var organization) || !fields.TryGetValue("tenant", out var tenant))
            return Task.FromResult(AuthenticateResult.Fail("Invalid test credentials."));
        var permissions = (fields.TryGetValue("permissions", out var rawPermissions) ? rawPermissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [])
            .Select(value => new Permission(value)).Where(permission => TradeMindPermissions.All.Contains(permission)).ToArray();
        var roles = fields.TryGetValue("roles", out var rawRoles) ? rawRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];
        var actor = new ActorIdentity(actorId, ActorType.HumanUser, new TradeMind.Identity.Domain.Users.UserId(actorId), null,
            new OrganizationId(organization), new TenantId(tenant), actorId, "test", roles, new PermissionSet(permissions), null,
            timeProvider.GetUtcNow(), timeProvider.GetUtcNow().AddHours(1), true);
        Context.Items[IdentityHttpContextKeys.Actor] = actor;
        var identity = new ClaimsIdentity([new Claim("sub", actorId)], IdentityAuthenticationDefaults.PolicyScheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), IdentityAuthenticationDefaults.PolicyScheme)));
    }

    private sealed class TimeProviderSystemClock(TimeProvider timeProvider) : ISystemClock
    {
        public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    }
}
#pragma warning restore CS0618
