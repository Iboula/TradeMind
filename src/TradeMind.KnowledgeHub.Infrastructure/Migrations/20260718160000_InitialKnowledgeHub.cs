using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace TradeMind.KnowledgeHub.Infrastructure.Migrations;

[DbContext(typeof(KnowledgeHubDbContext))]
[Migration("20260718160000_InitialKnowledgeHub")]
public partial class InitialKnowledgeHub : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:vector", ",,");

        migrationBuilder.CreateTable(
            name: "knowledge_sources",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                ImportedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_knowledge_sources", x => x.Id));

        migrationBuilder.CreateTable(
            name: "knowledge_fragments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                KnowledgeSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                Sequence = table.Column<int>(type: "integer", nullable: false),
                Content = table.Column<string>(type: "text", nullable: false),
                TokenCount = table.Column<int>(type: "integer", nullable: false),
                Embedding = table.Column<Vector>(type: "vector(64)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_knowledge_fragments", x => x.Id);
                table.ForeignKey(
                    name: "FK_knowledge_fragments_knowledge_sources_KnowledgeSourceId",
                    column: x => x.KnowledgeSourceId,
                    principalTable: "knowledge_sources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_knowledge_sources_ContentHash",
            table: "knowledge_sources",
            column: "ContentHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_knowledge_fragments_KnowledgeSourceId_Sequence",
            table: "knowledge_fragments",
            columns: new[] { "KnowledgeSourceId", "Sequence" },
            unique: true);

        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_knowledge_fragments_embedding ON knowledge_fragments USING hnsw (\"Embedding\" vector_cosine_ops);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "knowledge_fragments");
        migrationBuilder.DropTable(name: "knowledge_sources");
    }
}