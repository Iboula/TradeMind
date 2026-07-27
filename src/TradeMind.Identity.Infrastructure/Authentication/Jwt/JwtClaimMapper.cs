using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using TradeMind.Identity.Application;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.Actors;
using TradeMind.Identity.Domain.ApiKeys;
using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Permissions;
using TradeMind.Identity.Domain.Roles;
using TradeMind.Identity.Domain.Tenancy;
using TradeMind.Identity.Domain.Users;

namespace TradeMind.Identity.Infrastructure.Authentication.Jwt;

public interface IJwtClaimMapper
{
    ActorIdentity Map(ClaimsPrincipal principal);
}

public sealed class JwtClaimMapper(IOptions<IdentityOptions> options) : IJwtClaimMapper
{
    public ActorIdentity Map(ClaimsPrincipal principal)
    {
        var jwt = options.Value.Jwt;
        var claims = principal.Claims.Take(jwt.MaximumClaims + 1).ToArray();
        if (claims.Length > jwt.MaximumClaims) throw new IdentityAuthenticationException("The token contains too many claims.");
        var subject = First(claims, jwt.NameClaimType) ?? First(claims, JwtRegisteredClaimNames.Sub);
        var organization = First(claims, jwt.OrganizationClaimType);
        var tenant = First(claims, jwt.TenantClaimType);
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(tenant))
            throw new IdentityAuthenticationException("The token does not contain the required identity scope.");
        var roles = claims.Where(claim => claim.Type == jwt.RoleClaimType || claim.Type == ClaimTypes.Role).SelectMany(SplitValues).Distinct(StringComparer.Ordinal).ToArray();
        var direct = claims.Where(claim => claim.Type == jwt.PermissionClaimType).SelectMany(SplitValues);
        var scopes = claims.Where(claim => claim.Type == jwt.ScopeClaimType || claim.Type == "scp").SelectMany(SplitValues).Select(scope => MapScope(scope));
        var permissions = direct.Concat(scopes).Where(value => value is not null).Select(value => new Permission(value!)).Where(permission => TradeMindPermissions.All.Contains(permission)).ToArray();
        if (permissions.Length > jwt.MaximumPermissions) throw new IdentityAuthenticationException("The token contains too many permissions.");
        var rolePermissions = RolePermissionMapping.GetPermissions(roles);
        var allPermissions = permissions.Concat(rolePermissions.Values).Distinct().ToArray();
        var authAt = ReadTime(claims, JwtRegisteredClaimNames.Iat);
        var expires = ReadTime(claims, JwtRegisteredClaimNames.Exp);
        return new ActorIdentity(subject, roles.Any(role => role == TradeMindRoles.ServiceAccount) ? ActorType.ServiceAccount : ActorType.HumanUser,
            new UserId(subject), null, new OrganizationId(organization), new TenantId(tenant), First(claims, jwt.NameClaimType), "jwt", roles,
            new PermissionSet(allPermissions), First(claims, JwtRegisteredClaimNames.Jti), authAt, expires, true);
    }

    private static IEnumerable<string> SplitValues(Claim claim) => claim.ValueKind() ? claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [claim.Value];
    private static string? First(IEnumerable<Claim> claims, string type) => claims.FirstOrDefault(claim => claim.Type == type)?.Value;
    private static DateTimeOffset? ReadTime(IEnumerable<Claim> claims, string type) => long.TryParse(First(claims, type), out var value) ? DateTimeOffset.FromUnixTimeSeconds(value) : null;
    private static string? MapScope(string scope) => scope.StartsWith("TradeMind.", StringComparison.Ordinal) ? scope : null;
}

internal static class ClaimExtensions
{
    public static bool ValueKind(this Claim claim) => claim.Value.Contains(' ', StringComparison.Ordinal);
}
