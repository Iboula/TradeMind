using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TradeMind.Brokers.Infrastructure.Persistence;

#nullable disable

namespace TradeMind.Brokers.Infrastructure.Persistence.Migrations;

[Migration("20260801000000_InitialBrokerExecution")]
[DbContext(typeof(BrokerDbContext))]
public partial class InitialBrokerExecution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("broker_executions", table => new
        {
            id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            connector_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            account_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            execution_session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            error_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("pk_broker_executions", x => x.id));

        migrationBuilder.CreateTable("broker_orders", table => new
        {
            id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            execution_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            connector_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            account_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            client_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            instrument = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            quantity = table.Column<decimal>(type: "numeric", nullable: false),
            filled_quantity = table.Column<decimal>(type: "numeric", nullable: false),
            created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            concurrency_version = table.Column<long>(type: "bigint", nullable: false)
        }, constraints: table => table.PrimaryKey("pk_broker_orders", x => x.id));

        migrationBuilder.CreateTable("broker_positions", table => new
        {
            id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            connector_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            account_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            instrument = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            quantity = table.Column<decimal>(type: "numeric", nullable: false),
            average_price = table.Column<decimal>(type: "numeric", nullable: false),
            opened_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            concurrency_version = table.Column<long>(type: "bigint", nullable: false)
        }, constraints: table => table.PrimaryKey("pk_broker_positions", x => x.id));

        migrationBuilder.CreateTable("broker_fills", table => new
        {
            id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            execution_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            position_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            connector_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            account_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            quantity = table.Column<decimal>(type: "numeric", nullable: false),
            price = table.Column<decimal>(type: "numeric", nullable: false),
            filled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("pk_broker_fills", x => x.id));

        migrationBuilder.CreateTable("broker_idempotency", table => new
        {
            key_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            request_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            result_json = table.Column<string>(type: "jsonb", nullable: true),
            created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            concurrency_version = table.Column<long>(type: "bigint", nullable: false)
        }, constraints: table => table.PrimaryKey("pk_broker_idempotency", x => x.key_hash));

        migrationBuilder.CreateTable("broker_audit", table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            operation = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            actor_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            organization_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            connector_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            account_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            execution_session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            timestamp_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            metadata_json = table.Column<string>(type: "jsonb", nullable: false)
        }, constraints: table => table.PrimaryKey("pk_broker_audit", x => x.id));

        migrationBuilder.CreateTable("broker_reconciliation_runs", table => new
        {
            id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            connector_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            account_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            is_consistent = table.Column<bool>(type: "boolean", nullable: false),
            error_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
            started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("pk_broker_reconciliation_runs", x => x.id));

        migrationBuilder.CreateTable("broker_reconciliation_items", table => new
        {
            id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            reconciliation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
        }, constraints: table => table.PrimaryKey("pk_broker_reconciliation_items", x => x.id));

        migrationBuilder.CreateIndex("ix_broker_executions_tenant_account_created", "broker_executions", new[] { "tenant_id", "account_id", "created_at_utc" });
        migrationBuilder.CreateIndex("ix_broker_executions_execution_session", "broker_executions", "execution_session_id");
        migrationBuilder.CreateIndex("ux_broker_orders_tenant_account_client_order", "broker_orders", new[] { "tenant_id", "account_id", "client_order_id" }, unique: true);
        migrationBuilder.CreateIndex("ix_broker_orders_execution", "broker_orders", "execution_id");
        migrationBuilder.CreateIndex("ix_broker_positions_tenant_account_instrument", "broker_positions", new[] { "tenant_id", "account_id", "instrument" });
        migrationBuilder.CreateIndex("ix_broker_fills_tenant_account_filled", "broker_fills", new[] { "tenant_id", "account_id", "filled_at_utc" });
        migrationBuilder.CreateIndex("ux_broker_fills_execution", "broker_fills", "execution_id", unique: true);
        migrationBuilder.CreateIndex("ix_broker_idempotency_expires", "broker_idempotency", "expires_at_utc");
        migrationBuilder.CreateIndex("ix_broker_audit_tenant_timestamp", "broker_audit", new[] { "tenant_id", "timestamp_utc" });
        migrationBuilder.CreateIndex("ix_broker_audit_execution_session", "broker_audit", "execution_session_id");
        migrationBuilder.CreateIndex("ix_broker_reconciliation_tenant_account_started", "broker_reconciliation_runs", new[] { "tenant_id", "account_id", "started_at_utc" });
        migrationBuilder.CreateIndex("ix_broker_reconciliation_items_run", "broker_reconciliation_items", "reconciliation_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("broker_reconciliation_items");
        migrationBuilder.DropTable("broker_reconciliation_runs");
        migrationBuilder.DropTable("broker_audit");
        migrationBuilder.DropTable("broker_idempotency");
        migrationBuilder.DropTable("broker_positions");
        migrationBuilder.DropTable("broker_fills");
        migrationBuilder.DropTable("broker_orders");
        migrationBuilder.DropTable("broker_executions");
    }
}
