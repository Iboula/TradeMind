using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

namespace TradeMind.Modules.Knowledge.Infrastructure.Migrations;

[DbContext(typeof(KnowledgeDbContext))]
[Migration("202607170001_InitialKnowledgeHub")]
internal sealed class InitialKnowledgeHub : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

        migrationBuilder.CreateTable(
            name: "knowledge_sources",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                MediaType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CreatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IndexedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_knowledge_sources", x => x.Id));

        migrationBuilder.CreateTable(
            name: "knowledge_fragments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Content = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_knowledge_fragments", x => x.Id);
                table.ForeignKey(
                    name: "FK_knowledge_fragments_knowledge_sources_SourceId",
                    column: x => x.SourceId,
                    principalTable: "knowledge_sources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "embeddings",
            columns: table => new
            {
                FragmentId = table.Column<Guid>(type: "uuid", nullable: false),
                Vector = table.Column<Vector>(type: "vector(64)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_embeddings", x => x.FragmentId);
                table.ForeignKey(
                    name: "FK_embeddings_knowledge_fragments_FragmentId",
                    column: x => x.FragmentId,
                    principalTable: "knowledge_fragments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_knowledge_fragments_SourceId_Position",
            table: "knowledge_fragments",
            columns: new[] { "SourceId", "Position" },
            unique: true);

        migrationBuilder.Sql(
            "CREATE INDEX IX_embeddings_Vector ON embeddings USING hnsw (\"Vector\" vector_cosine_ops);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "embeddings");
        migrationBuilder.DropTable(name: "knowledge_fragments");
        migrationBuilder.DropTable(name: "knowledge_sources");
    }
}
