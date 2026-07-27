using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradeMind.Identity.Infrastructure.Persistence.Entities;

namespace TradeMind.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
    public DbSet<UserIdentityEntity> Users => Set<UserIdentityEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
    public DbSet<ApiKeyPermissionEntity> ApiKeyPermissions => Set<ApiKeyPermissionEntity>();
    public DbSet<IdentityAuditEntity> Audit => Set<IdentityAuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationEntity>(entity =>
        {
            entity.ToTable("identity_organizations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(128);
            entity.Property(item => item.TenantId).HasColumnName("tenant_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
            entity.Property(item => item.Slug).HasColumnName("slug").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(item => item.ConcurrencyVersion).HasColumnName("concurrency_version").IsConcurrencyToken().IsRequired();
            entity.HasIndex(item => item.Slug).IsUnique().HasDatabaseName("ux_identity_organizations_slug");
        });

        modelBuilder.Entity<UserIdentityEntity>(entity =>
        {
            entity.ToTable("identity_users");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(128);
            entity.Property(item => item.Provider).HasColumnName("provider").HasMaxLength(128).IsRequired();
            entity.Property(item => item.ProviderSubject).HasColumnName("provider_subject").HasMaxLength(256).IsRequired();
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.TenantId).HasColumnName("tenant_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.DisplayName).HasColumnName("display_name").HasMaxLength(256);
            entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(item => item.LastSeenAtUtc).HasColumnName("last_seen_at_utc");
            entity.Property(item => item.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.ConcurrencyVersion).HasColumnName("concurrency_version").IsConcurrencyToken().IsRequired();
            entity.HasIndex(item => new { item.Provider, item.ProviderSubject }).IsUnique().HasDatabaseName("ux_identity_users_provider_subject");
            entity.HasIndex(item => item.OrganizationId).HasDatabaseName("ix_identity_users_organization");
            entity.HasIndex(item => item.TenantId).HasDatabaseName("ix_identity_users_tenant");
        });

        modelBuilder.Entity<ApiKeyEntity>(entity =>
        {
            entity.ToTable("identity_api_keys");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.PublicKeyId).HasColumnName("public_key_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.TenantId).HasColumnName("tenant_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Description).HasColumnName("description").HasMaxLength(512);
            entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(item => item.SecretAlgorithm).HasColumnName("secret_algorithm").HasMaxLength(64).IsRequired();
            entity.Property(item => item.SecretSalt).HasColumnName("secret_salt").HasColumnType("bytea").IsRequired();
            entity.Property(item => item.SecretHash).HasColumnName("secret_hash").HasColumnType("bytea").IsRequired();
            entity.Property(item => item.SecretIterations).HasColumnName("secret_iterations").IsRequired();
            entity.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(item => item.CreatedByActorId).HasColumnName("created_by_actor_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entity.Property(item => item.LastUsedAtUtc).HasColumnName("last_used_at_utc");
            entity.Property(item => item.RevokedAtUtc).HasColumnName("revoked_at_utc");
            entity.Property(item => item.RevokedByActorId).HasColumnName("revoked_by_actor_id").HasMaxLength(128);
            entity.Property(item => item.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(512);
            entity.Property(item => item.KeyVersion).HasColumnName("key_version").IsRequired();
            entity.Property(item => item.ConcurrencyVersion).HasColumnName("concurrency_version").IsConcurrencyToken().IsRequired();
            entity.HasIndex(item => item.PublicKeyId).IsUnique().HasDatabaseName("ux_identity_api_keys_public_key");
            entity.HasIndex(item => new { item.OrganizationId, item.Status }).HasDatabaseName("ix_identity_api_keys_organization_status");
            entity.HasIndex(item => item.TenantId).HasDatabaseName("ix_identity_api_keys_tenant");
            entity.HasIndex(item => item.ExpiresAtUtc).HasDatabaseName("ix_identity_api_keys_expiration");
            entity.HasIndex(item => item.LastUsedAtUtc).HasDatabaseName("ix_identity_api_keys_last_used");
            entity.HasMany(item => item.Permissions).WithOne(item => item.ApiKey).HasForeignKey(item => item.ApiKeyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiKeyPermissionEntity>(entity =>
        {
            entity.ToTable("identity_api_key_permissions");
            entity.HasKey(item => new { item.ApiKeyId, item.Permission });
            entity.Property(item => item.ApiKeyId).HasColumnName("api_key_id");
            entity.Property(item => item.Permission).HasColumnName("permission").HasMaxLength(256);
        });

        modelBuilder.Entity<IdentityAuditEntity>(entity =>
        {
            entity.ToTable("identity_audit");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
            entity.Property(item => item.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
            entity.Property(item => item.ActorId).HasColumnName("actor_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.ActorType).HasColumnName("actor_type").HasMaxLength(32).IsRequired();
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id").HasMaxLength(128);
            entity.Property(item => item.TenantId).HasColumnName("tenant_id").HasMaxLength(128);
            entity.Property(item => item.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Outcome).HasColumnName("outcome").HasMaxLength(32).IsRequired();
            entity.Property(item => item.Permission).HasColumnName("permission").HasMaxLength(256);
            entity.Property(item => item.ResourceType).HasColumnName("resource_type").HasMaxLength(128).IsRequired();
            entity.Property(item => item.ResourceId).HasColumnName("resource_id").HasMaxLength(256);
            entity.Property(item => item.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb").IsRequired();
            entity.HasIndex(item => new { item.TenantId, item.OccurredAtUtc }).HasDatabaseName("ix_identity_audit_tenant_occurred");
        });
    }

    public static string Serialize(IReadOnlyDictionary<string, string> metadata) => JsonSerializer.Serialize(metadata);
    public static IReadOnlyDictionary<string, string> Deserialize(string json) => JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
}
