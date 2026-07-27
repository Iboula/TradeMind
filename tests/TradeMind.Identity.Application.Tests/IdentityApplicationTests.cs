using TradeMind.Identity.Application.Authorization;
using TradeMind.Identity.Application.DTOs;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.Actors;
using TradeMind.Identity.Domain.ApiKeys;
using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Permissions;
using TradeMind.Identity.Domain.Tenancy;

namespace TradeMind.Identity.Application.Tests;

public sealed class IdentityApplicationTests
{
    [Fact]
    public void Permission_evaluator_grants_exact_permission_only()
    {
        var actor = Actor("TradeMind.ExecutionSessions.Read");
        var evaluator = new PermissionEvaluator();
        Assert.True(evaluator.Evaluate(actor, new Permission(TradeMindPermissions.ExecutionSessionsRead)).IsAllowed);
        Assert.False(evaluator.Evaluate(actor, new Permission(TradeMindPermissions.ExecutionSessionsCreate)).IsAllowed);
    }

    [Fact]
    public void Permission_evaluator_denies_anonymous_actor()
    {
        var result = new PermissionEvaluator().Evaluate(ActorIdentity.Anonymous(), new Permission(TradeMindPermissions.SystemReadVersion));
        Assert.False(result.IsAllowed);
        Assert.Equal("FORBIDDEN", result.Code);
    }

    [Fact]
    public void Identity_dto_never_contains_api_key_secret_or_hash()
    {
        var dto = IdentityDtoMapper.ToDto(Actor("TradeMind.ApiKeys.Read"));
        var text = System.Text.Json.JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unknown_permission_is_rejected_by_api_key_request_policy()
    {
        var request = new CreateApiKeyRequest("key", null, ["TradeMind.Unknown"], null);
        Assert.Equal("TradeMind.Unknown", request.Permissions.Single());
    }

    [Fact]
    public void Tenant_context_preserves_explicit_administrative_override()
    {
        var actor = Actor(TradeMindPermissions.AdministrationManageOrganizations);
        var context = new TenantContext(new OrganizationId("org-1"), new TenantId("tenant-2"), actor, true);
        Assert.True(context.IsAdministrativeOverride);
        Assert.Equal("tenant-2", context.TenantId.Value);
    }

    private static ActorIdentity Actor(params string[] permissions) => new("actor-1", ActorType.HumanUser, new TradeMind.Identity.Domain.Users.UserId("user-1"), null,
        new OrganizationId("org-1"), new TenantId("tenant-1"), "Actor", "test", [], new PermissionSet(permissions.Select(value => new Permission(value))), null,
        new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 27, 13, 0, 0, TimeSpan.Zero), true);
}
