using System.ComponentModel.DataAnnotations;

namespace TradeMind.Identity.Application;

public sealed class IdentityOptions
{
    public const string SectionName = "TradeMind:Identity";
    public bool Enabled { get; set; }
    public bool ApplyMigrationsOnStartup { get; set; }
    public JwtIdentityOptions Jwt { get; set; } = new();
    public ApiKeyIdentityOptions ApiKeys { get; set; } = new();
    public TenantIdentityOptions Tenancy { get; set; } = new();
    public DevelopmentIdentityOptions Development { get; set; } = new();
    public RateLimitIdentityOptions RateLimiting { get; set; } = new();
}

public sealed class JwtIdentityOptions
{
    public bool Enabled { get; set; } = true;
    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
    public bool ValidateLifetime { get; set; } = true;
    [Range(0, 300)] public int ClockSkewSeconds { get; set; } = 60;
    public string NameClaimType { get; set; } = "sub";
    public string RoleClaimType { get; set; } = "role";
    public string OrganizationClaimType { get; set; } = "organization_id";
    public string TenantClaimType { get; set; } = "tenant_id";
    public string PermissionClaimType { get; set; } = "permission";
    public string ScopeClaimType { get; set; } = "scope";
    public int MaximumClaims { get; set; } = 128;
    public int MaximumPermissions { get; set; } = 128;
}

public sealed class ApiKeyIdentityOptions
{
    public bool Enabled { get; set; } = true;
    public string HeaderName { get; set; } = "X-TradeMind-Api-Key";
    [Range(1, 1000)] public int MaximumActiveKeysPerOrganization { get; set; } = 25;
    [Range(100000, 1000000)] public int Pbkdf2Iterations { get; set; } = 210000;
    [Range(16, 128)] public int SaltSizeBytes { get; set; } = 32;
    [Range(16, 128)] public int HashSizeBytes { get; set; } = 32;
}

public sealed class TenantIdentityOptions
{
    public string SelectorHeaderName { get; set; } = "X-TradeMind-Tenant-ID";
    public string ResponseHeaderName { get; set; } = "X-TradeMind-Tenant-ID";
    public bool RequireTenantForProtectedResources { get; set; } = true;
    public bool AllowPlatformAdministratorOverride { get; set; }
}

public sealed class DevelopmentIdentityOptions
{
    public bool EnableTestAuthentication { get; set; }
    public bool EnableSignedDeveloperTokens { get; set; }
}

public sealed class RateLimitIdentityOptions
{
    public bool Enabled { get; set; } = true;
    [Range(1, 10000)] public int PermitLimit { get; set; } = 120;
    [Range(1, 3600)] public int WindowSeconds { get; set; } = 60;
}
