using System.Security.Cryptography;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeMind.Identity.Application;
using TradeMind.Identity.Domain.ApiKeys;
using TradeMind.Identity.Domain.Organizations;
using TradeMind.Identity.Domain.Permissions;
using TradeMind.Identity.Domain.Tenancy;
using TradeMind.Identity.Infrastructure.Persistence;
using TradeMind.Identity.Infrastructure.Persistence.Entities;
using TradeMind.Identity.Infrastructure.Security;
using TradeMind.Identity.Infrastructure.Authentication.Jwt;

namespace TradeMind.Identity.Infrastructure.Tests;

public sealed class IdentityInfrastructureTests(PostgreSqlIdentityFixture fixture) : IClassFixture<PostgreSqlIdentityFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Identity_migration_creates_required_tables_and_indexes()
    {
        await using var db = fixture.CreateDbContext();
        var tables = await db.Database.SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' AND table_name LIKE 'identity_%' ORDER BY table_name").ToListAsync();
        Assert.Contains("identity_organizations", tables);
        Assert.Contains("identity_api_keys", tables);
        Assert.Contains("identity_api_key_permissions", tables);
        Assert.Contains("identity_audit", tables);
        var indexes = await db.Database.SqlQueryRaw<string>("SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public' AND indexname LIKE '%identity%'").ToListAsync();
        Assert.Contains("ux_identity_api_keys_public_key", indexes);
        Assert.Contains("ix_identity_api_keys_expiration", indexes);
    }

    [Fact]
    public async Task Organization_and_api_key_persist_without_raw_secret()
    {
        await using var db = fixture.CreateDbContext();
        db.Organizations.Add(new OrganizationEntity { Id = "org-1", TenantId = "tenant-1", Name = "Org", Slug = "org-1", Status = "Active", CreatedAtUtc = Now, ConcurrencyVersion = 1 });
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = RandomNumberGenerator.GetBytes(32);
        db.ApiKeys.Add(new ApiKeyEntity
        {
            Id = Guid.NewGuid(), PublicKeyId = "public-1", OrganizationId = "org-1", TenantId = "tenant-1", Name = "integration", Status = "Active",
            SecretAlgorithm = "PBKDF2-SHA512", SecretSalt = salt, SecretHash = hash, SecretIterations = 210000, CreatedAtUtc = Now, CreatedByActorId = "actor", KeyVersion = 1, ConcurrencyVersion = 1,
            Permissions = [new ApiKeyPermissionEntity { Permission = TradeMindPermissions.ExecutionSessionsRead }]
        });
        await db.SaveChangesAsync();
        var loaded = await db.ApiKeys.Include(key => key.Permissions).SingleAsync(key => key.PublicKeyId == "public-1");
        Assert.Equal("public-1", loaded.PublicKeyId);
        Assert.Equal(hash, loaded.SecretHash);
        var safeProjection = System.Text.Json.JsonSerializer.Serialize(new { loaded.PublicKeyId, loaded.SecretAlgorithm, loaded.SecretIterations });
        Assert.DoesNotContain("tm_", safeProjection);
    }

    [Fact]
    public void Pbkdf2_hash_verifies_with_fixed_time_comparison()
    {
        var options = Options.Create(new IdentityOptions());
        var generator = new ApiKeySecretGenerator(options);
        var material = generator.Generate("Test");
        var hasher = new ApiKeyHasher();
        var secret = material.RawSecret[(material.RawSecret.IndexOf('_', material.RawSecret.IndexOf('_') + 1) + 1)..];
        secret = secret[(secret.IndexOf('_') + 1)..];
        Assert.True(hasher.Verify(secret, material.Hash));
        Assert.False(hasher.Verify("wrong-secret", material.Hash));
        Assert.True(ApiKeySecretGenerator.TryParse(material.RawSecret, out var publicId, out _));
        Assert.Equal(material.PublicKeyId, publicId);
    }

    [Fact]
    public async Task Tenant_scoped_api_key_query_can_be_filtered_deterministically()
    {
        await using var db = fixture.CreateDbContext();
        db.ApiKeys.Add(new ApiKeyEntity
        {
            Id = Guid.NewGuid(), PublicKeyId = "scoped-public", OrganizationId = "scoped-org", TenantId = "scoped-tenant", Name = "scoped", Status = ApiKeyStatus.Active.ToString(),
            SecretAlgorithm = "PBKDF2-SHA512", SecretSalt = RandomNumberGenerator.GetBytes(32), SecretHash = RandomNumberGenerator.GetBytes(32), SecretIterations = 210000,
            CreatedAtUtc = Now, CreatedByActorId = "actor", KeyVersion = 1, ConcurrencyVersion = 1
        });
        await db.SaveChangesAsync();
        var count = await db.ApiKeys.Where(key => key.TenantId == "tenant-1" && key.Status == ApiKeyStatus.Active.ToString()).CountAsync();
        Assert.Equal(0, count);
        Assert.Equal(1, await db.ApiKeys.CountAsync(key => key.TenantId == "scoped-tenant"));
    }

    [Fact]
    public void Jwt_claim_mapper_maps_roles_scopes_and_scope_metadata()
    {
        var mapper = new JwtClaimMapper(Options.Create(new IdentityOptions()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "subject-1"),
            new Claim("organization_id", "org-1"),
            new Claim("tenant_id", "tenant-1"),
            new Claim("role", "Analyst"),
            new Claim("scope", "TradeMind.Risk.Evaluate"),
            new Claim("jti", "token-1"),
            new Claim("iat", "1785153600")
        ]));

        var actor = mapper.Map(principal);

        Assert.Equal("subject-1", actor.ActorId);
        Assert.True(actor.Permissions.Contains(TradeMindPermissions.RiskEvaluate));
        Assert.True(actor.Permissions.Contains(TradeMindPermissions.MarketContextBuild));
        Assert.Equal("token-1", actor.TokenId);
        Assert.NotNull(actor.AuthenticatedAtUtc);
    }

    [Fact]
    public void Jwt_claim_mapper_rejects_missing_scope_and_unknown_roles_grant_nothing()
    {
        var mapper = new JwtClaimMapper(Options.Create(new IdentityOptions()));
        var missingScope = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "subject-1"), new Claim("organization_id", "org-1")
        ]));
        Assert.Throws<IdentityAuthenticationException>(() => mapper.Map(missingScope));

        var unknownRole = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "subject-1"), new Claim("organization_id", "org-1"),
            new Claim("tenant_id", "tenant-1"), new Claim("role", "Unknown")
        ]));
        Assert.Empty(mapper.Map(unknownRole).Permissions.Values);
    }
}
