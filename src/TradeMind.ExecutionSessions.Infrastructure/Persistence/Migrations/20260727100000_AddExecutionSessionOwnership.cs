using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeMind.ExecutionSessions.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ExecutionSessionsDbContext))]
[Migration("20260727100000_AddExecutionSessionOwnership")]
public partial class AddExecutionSessionOwnership : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("organization_id", "execution_sessions", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>("created_by_actor_id", "execution_sessions", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>("created_by_actor_type", "execution_sessions", maxLength: 32, nullable: true);
        migrationBuilder.Sql("UPDATE execution_sessions SET organization_id = 'system', tenant_id = COALESCE(tenant_id, 'system'), created_by_actor_id = 'system', created_by_actor_type = 'System' WHERE organization_id IS NULL;");
        migrationBuilder.CreateIndex("ix_execution_sessions_organization_tenant_started", "execution_sessions", new[] { "organization_id", "tenant_id", "started_at_utc" });
        migrationBuilder.CreateIndex("ix_execution_sessions_created_by_actor", "execution_sessions", "created_by_actor_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("ix_execution_sessions_created_by_actor", "execution_sessions");
        migrationBuilder.DropIndex("ix_execution_sessions_organization_tenant_started", "execution_sessions");
        migrationBuilder.DropColumn("created_by_actor_type", "execution_sessions");
        migrationBuilder.DropColumn("created_by_actor_id", "execution_sessions");
        migrationBuilder.DropColumn("organization_id", "execution_sessions");
    }
}
