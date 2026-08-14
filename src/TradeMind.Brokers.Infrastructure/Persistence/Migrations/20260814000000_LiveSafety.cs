using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TradeMind.Brokers.Infrastructure.Persistence;

#nullable disable

namespace TradeMind.Brokers.Infrastructure.Persistence.Migrations;

[Migration("20260814000000_LiveSafety")]
[DbContext(typeof(BrokerDbContext))]
public partial class LiveSafety : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("broker_kill_switches", table => new
        {
            key = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
            scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            changed_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
        }, constraints: table => table.PrimaryKey("pk_broker_kill_switches", x => x.key));

        migrationBuilder.CreateTable("broker_execution_quarantine", table => new
        {
            execution_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            broker_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            account_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            instrument = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            changed_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            explanation = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
        }, constraints: table => table.PrimaryKey("pk_broker_execution_quarantine", x => x.execution_id));

        migrationBuilder.CreateTable("broker_position_ownership", table => new
        {
            position_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            execution_session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            broker_execution_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            trading_plan_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            risk_assessment_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            broker_account_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            symbol = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            opened_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            reconciliation_state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            concurrency_version = table.Column<long>(type: "bigint", nullable: false)
        }, constraints: table => table.PrimaryKey("pk_broker_position_ownership", x => x.position_id));

        migrationBuilder.CreateIndex("ux_broker_kill_switch_scope_value", "broker_kill_switches", new[] { "scope", "value" }, unique: true);
        migrationBuilder.CreateIndex("ix_broker_quarantine_tenant_state", "broker_execution_quarantine", new[] { "tenant_id", "state" });
        migrationBuilder.CreateIndex("ix_broker_position_ownership_tenant_account_symbol", "broker_position_ownership", new[] { "tenant_id", "broker_account_id", "symbol" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("broker_position_ownership");
        migrationBuilder.DropTable("broker_execution_quarantine");
        migrationBuilder.DropTable("broker_kill_switches");
    }
}
