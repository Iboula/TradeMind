using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeMind.MarketConnectors.Infrastructure.Migrations;

[DbContext(typeof(MarketConnectorsDbContext))]
[Migration("20260719210000_AddMarketConnectorCore")]
public partial class AddMarketConnectorCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "market_snapshot_records",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                connector_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                account_reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                instrument = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                timeframe = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                freshness = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                content_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                is_complete = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_market_snapshot_records", x => x.id);
                table.CheckConstraint(
                    "ck_market_snapshot_records_content_hash",
                    "char_length(content_hash) = 64");
            });

        migrationBuilder.CreateTable(
            name: "market_snapshot_payloads",
            columns: table => new
            {
                market_snapshot_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                document = table.Column<string>(type: "jsonb", nullable: false),
                schema_version = table.Column<int>(type: "integer", nullable: false),
                payload_size_bytes = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_market_snapshot_payloads", x => x.market_snapshot_record_id);
                table.CheckConstraint(
                    "ck_market_snapshot_payloads_schema_version",
                    "schema_version > 0");
                table.CheckConstraint(
                    "ck_market_snapshot_payloads_size",
                    "payload_size_bytes >= 0");
                table.ForeignKey(
                    name: "FK_market_snapshot_payloads_market_snapshot_records_market_snapshot_record_id",
                    column: x => x.market_snapshot_record_id,
                    principalTable: "market_snapshot_records",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: PostgreSqlMarketSnapshotRepository.IdempotenceConstraintName,
            table: "market_snapshot_records",
            columns: new[] { "connector_id", "snapshot_id" },
            unique: true);

        migrationBuilder.Sql(
            "CREATE INDEX ix_market_snapshot_records_latest ON market_snapshot_records "
            + "(connector_id, account_reference, instrument, timeframe, captured_at DESC);");
        migrationBuilder.Sql(
            "CREATE INDEX ix_market_snapshot_records_health ON market_snapshot_records "
            + "(connector_id, received_at DESC);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "market_snapshot_payloads");
        migrationBuilder.DropTable(name: "market_snapshot_records");
    }
}
