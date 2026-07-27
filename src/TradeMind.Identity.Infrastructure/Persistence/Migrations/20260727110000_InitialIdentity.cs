using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeMind.Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260727110000_InitialIdentity")]
public partial class InitialIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "identity_organizations",
            columns: table => new
            {
                id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                concurrency_version = table.Column<long>(type: "bigint", nullable: false)
            }, constraints: table => table.PrimaryKey("pk_identity_organizations", x => x.id));

        migrationBuilder.CreateTable(
            name: "identity_users",
            columns: table => new
            {
                id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                provider_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                organization_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                metadata_json = table.Column<string>(type: "jsonb", nullable: false),
                concurrency_version = table.Column<long>(type: "bigint", nullable: false)
            }, constraints: table => table.PrimaryKey("pk_identity_users", x => x.id));

        migrationBuilder.CreateTable(
            name: "identity_api_keys",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                public_key_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                organization_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                secret_algorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                secret_salt = table.Column<byte[]>(type: "bytea", nullable: false),
                secret_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                secret_iterations = table.Column<int>(type: "integer", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by_actor_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_used_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                revoked_by_actor_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                revocation_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                key_version = table.Column<int>(type: "integer", nullable: false),
                concurrency_version = table.Column<long>(type: "bigint", nullable: false)
            }, constraints: table => table.PrimaryKey("pk_identity_api_keys", x => x.id));

        migrationBuilder.CreateTable(
            name: "identity_audit",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                actor_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                actor_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                organization_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                permission = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                resource_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                resource_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                metadata_json = table.Column<string>(type: "jsonb", nullable: false)
            }, constraints: table => table.PrimaryKey("pk_identity_audit", x => x.id));

        migrationBuilder.CreateTable(
            name: "identity_api_key_permissions",
            columns: table => new
            {
                api_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                permission = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("pk_identity_api_key_permissions", x => new { x.api_key_id, x.permission });
                table.ForeignKey("fk_identity_api_key_permissions_api_keys", x => x.api_key_id, "identity_api_keys", "id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("ux_identity_organizations_slug", "identity_organizations", "slug", unique: true);
        migrationBuilder.CreateIndex("ux_identity_users_provider_subject", "identity_users", new[] { "provider", "provider_subject" }, unique: true);
        migrationBuilder.CreateIndex("ix_identity_users_organization", "identity_users", "organization_id");
        migrationBuilder.CreateIndex("ix_identity_users_tenant", "identity_users", "tenant_id");
        migrationBuilder.CreateIndex("ux_identity_api_keys_public_key", "identity_api_keys", "public_key_id", unique: true);
        migrationBuilder.CreateIndex("ix_identity_api_keys_organization_status", "identity_api_keys", new[] { "organization_id", "status" });
        migrationBuilder.CreateIndex("ix_identity_api_keys_tenant", "identity_api_keys", "tenant_id");
        migrationBuilder.CreateIndex("ix_identity_api_keys_expiration", "identity_api_keys", "expires_at_utc");
        migrationBuilder.CreateIndex("ix_identity_api_keys_last_used", "identity_api_keys", "last_used_at_utc");
        migrationBuilder.CreateIndex("ix_identity_audit_tenant_occurred", "identity_audit", new[] { "tenant_id", "occurred_at_utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("identity_api_key_permissions");
        migrationBuilder.DropTable("identity_audit");
        migrationBuilder.DropTable("identity_api_keys");
        migrationBuilder.DropTable("identity_users");
        migrationBuilder.DropTable("identity_organizations");
    }
}
