using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TradeMind.Modules.KnowledgeHub.Infrastructure.Migrations;

[DbContext(typeof(KnowledgeHubDbContext))]
[Migration("202607170001_InitializeKnowledgeHub")]
internal sealed class InitializeKnowledgeHub : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS knowledge_sources (
                id uuid PRIMARY KEY,
                file_name varchar(512) NOT NULL,
                media_type varchar(128) NOT NULL,
                status varchar(32) NOT NULL,
                created_at_utc timestamptz NOT NULL
            );

            CREATE TABLE IF NOT EXISTS knowledge_fragments (
                id uuid PRIMARY KEY,
                source_id uuid NOT NULL REFERENCES knowledge_sources(id) ON DELETE CASCADE,
                position integer NOT NULL,
                content text NOT NULL,
                CONSTRAINT uq_knowledge_fragments_source_position UNIQUE (source_id, position)
            );

            CREATE TABLE IF NOT EXISTS embeddings (
                id uuid PRIMARY KEY,
                fragment_id uuid NOT NULL UNIQUE REFERENCES knowledge_fragments(id) ON DELETE CASCADE,
                values vector(64) NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_embeddings_values_cosine
                ON embeddings USING hnsw (values vector_cosine_ops);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS embeddings; DROP TABLE IF EXISTS knowledge_fragments; DROP TABLE IF EXISTS knowledge_sources;");
    }
}
