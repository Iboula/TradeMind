using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Pgvector;
using TradeMind.KnowledgeHub.Application;
using TradeMind.KnowledgeHub.Domain;

namespace TradeMind.KnowledgeHub.Infrastructure;

public sealed class KnowledgeHubDbContext(DbContextOptions<KnowledgeHubDbContext> options) : DbContext(options)
{
    public DbSet<KnowledgeSource> KnowledgeSources => Set<KnowledgeSource>();
    public DbSet<KnowledgeFragment> KnowledgeFragments => Set<KnowledgeFragment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<KnowledgeSource>(entity =>
        {
            entity.ToTable("knowledge_sources");
            entity.HasKey(source => source.Id);
            entity.Property(source => source.Title).HasMaxLength(500).IsRequired();
            entity.Property(source => source.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(source => source.ContentHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(source => source.ContentHash).IsUnique();
            entity.Property(source => source.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(source => source.ImportedAtUtc).IsRequired();
            entity.Property(source => source.FailureReason).HasMaxLength(2000);

            entity.HasMany(source => source.Fragments)
                .WithOne()
                .HasForeignKey(fragment => fragment.KnowledgeSourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(source => source.Fragments)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        var embeddingComparer = new ValueComparer<float[]>(
            (left, right) => left != null && right != null && left.SequenceEqual(right),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            value => value.ToArray());

        modelBuilder.Entity<KnowledgeFragment>(entity =>
        {
            entity.ToTable("knowledge_fragments");
            entity.HasKey(fragment => fragment.Id);
            entity.Property(fragment => fragment.Content).IsRequired();
            entity.Property(fragment => fragment.TokenCount).IsRequired();
            entity.HasIndex(fragment => new { fragment.KnowledgeSourceId, fragment.Sequence }).IsUnique();

            var embeddingProperty = entity.Property(fragment => fragment.Embedding)
                .HasConversion(
                    value => new Vector(value),
                    value => value.ToArray())
                .HasColumnType("vector(64)")
                .IsRequired();

            embeddingProperty.Metadata.SetValueComparer(embeddingComparer);
        });
    }
}

public sealed class PostgreSqlKnowledgeSourceRepository(KnowledgeHubDbContext dbContext)
    : IKnowledgeSourceRepository
{
    public Task<bool> ExistsByHashAsync(string hash, CancellationToken cancellationToken) =>
        dbContext.KnowledgeSources.AnyAsync(source => source.ContentHash == hash, cancellationToken);

    public async Task AddAsync(KnowledgeSource source, CancellationToken cancellationToken) =>
        await dbContext.KnowledgeSources.AddAsync(source, cancellationToken);

    public Task<KnowledgeSource?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.KnowledgeSources
            .AsNoTracking()
            .Include(source => source.Fragments)
            .SingleOrDefaultAsync(source => source.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        float[] embedding,
        int limit,
        CancellationToken cancellationToken)
    {
        var vectorLiteral = $"[{string.Join(',', embedding.Select(value => value.ToString("R", CultureInfo.InvariantCulture)))}]";
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT s.id,
                       s.title,
                       f.id,
                       f.sequence,
                       f.content,
                       1 - (f.embedding <=> CAST(@embedding AS vector)) AS score
                FROM knowledge_fragments f
                INNER JOIN knowledge_sources s ON s.id = f.knowledge_source_id
                WHERE s.status = 'Ready'
                ORDER BY f.embedding <=> CAST(@embedding AS vector)
                LIMIT @limit;
                """;

            var embeddingParameter = command.CreateParameter();
            embeddingParameter.ParameterName = "embedding";
            embeddingParameter.Value = vectorLiteral;
            command.Parameters.Add(embeddingParameter);

            var limitParameter = command.CreateParameter();
            limitParameter.ParameterName = "limit";
            limitParameter.Value = limit;
            command.Parameters.Add(limitParameter);

            var results = new List<KnowledgeSearchResult>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new KnowledgeSearchResult(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetGuid(2),
                    reader.GetInt32(3),
                    reader.GetString(4),
                    reader.GetDouble(5)));
            }

            return results;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}