using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeMind.ExecutionSessions.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ExecutionSessionsDbContext))]
[Migration("20260726150000_InitialExecutionSessions")]
public partial class InitialExecutionSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "execution_sessions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                idempotency_key_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                instrument = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                timeframe = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                current_stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                schema_version = table.Column<int>(type: "integer", nullable: false),
                core_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                api_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                trigger_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                failure_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                failure_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                failure_details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                metadata_json = table.Column<string>(type: "jsonb", nullable: false),
                concurrency_version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_execution_sessions", x => x.id));

        migrationBuilder.CreateTable(
            name: "execution_session_artifacts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                artifact_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                artifact_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                schema_version = table.Column<int>(type: "integer", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                content_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                storage_reference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                is_replayable = table.Column<bool>(type: "boolean", nullable: false),
                metadata_json = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_execution_session_artifacts", x => x.id);
                table.ForeignKey("fk_execution_session_artifacts_sessions", x => x.session_id, "execution_sessions", "id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "execution_session_timeline",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                metadata_json = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_execution_session_timeline", x => x.id);
                table.ForeignKey("fk_execution_session_timeline_sessions", x => x.session_id, "execution_sessions", "id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "execution_session_audit",
            columns: table => new
            {
                audit_id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                actor_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                actor_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                previous_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                new_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                previous_stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                new_stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                artifact_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                metadata_json = table.Column<string>(type: "jsonb", nullable: false),
                schema_version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_execution_session_audit", x => x.audit_id));

        migrationBuilder.CreateTable(
            name: "execution_session_outbox",
            columns: table => new
            {
                outbox_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false),
                occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                last_error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                concurrency_version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_execution_session_outbox", x => x.outbox_message_id));

        migrationBuilder.CreateTable(
            name: "execution_idempotency_records",
            columns: table => new
            {
                idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                request_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                execution_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                response_status_code = table.Column<int>(type: "integer", nullable: true),
                response_content_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                response_body = table.Column<byte[]>(type: "bytea", nullable: true),
                concurrency_version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_execution_idempotency_records", x => x.idempotency_key));

        migrationBuilder.CreateIndex("ix_execution_sessions_correlation_id", "execution_sessions", "correlation_id");
        migrationBuilder.CreateIndex("ix_execution_sessions_status_started", "execution_sessions", new[] { "status", "started_at_utc" });
        migrationBuilder.CreateIndex("ix_execution_sessions_instrument_started", "execution_sessions", new[] { "instrument", "started_at_utc" });
        migrationBuilder.CreateIndex("ix_execution_sessions_current_stage", "execution_sessions", "current_stage");
        migrationBuilder.CreateIndex("ix_execution_sessions_idempotency_hash", "execution_sessions", "idempotency_key_hash", filter: "idempotency_key_hash IS NOT NULL");
        migrationBuilder.CreateIndex("ix_execution_session_artifacts_artifact_id", "execution_session_artifacts", "artifact_id");
        migrationBuilder.CreateIndex("ux_execution_session_artifacts_identity", "execution_session_artifacts", new[] { "session_id", "artifact_type", "artifact_id" }, unique: true);
        migrationBuilder.CreateIndex("ix_execution_session_timeline_session_occurred", "execution_session_timeline", new[] { "session_id", "occurred_at_utc" });
        migrationBuilder.CreateIndex("ix_execution_session_audit_session_occurred", "execution_session_audit", new[] { "session_id", "occurred_at_utc" });
        migrationBuilder.CreateIndex("ix_execution_session_outbox_pending", "execution_session_outbox", new[] { "processed_at_utc", "occurred_at_utc" });
        migrationBuilder.CreateIndex("ix_execution_idempotency_expires", "execution_idempotency_records", "expires_at_utc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "execution_idempotency_records");
        migrationBuilder.DropTable(name: "execution_session_audit");
        migrationBuilder.DropTable(name: "execution_session_outbox");
        migrationBuilder.DropTable(name: "execution_session_timeline");
        migrationBuilder.DropTable(name: "execution_session_artifacts");
        migrationBuilder.DropTable(name: "execution_sessions");
    }
}
