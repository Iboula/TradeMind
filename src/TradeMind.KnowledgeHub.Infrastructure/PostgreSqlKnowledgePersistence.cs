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
        var queryVector = new Vector(embedding);

        var results = await (
            from fragment in dbContext.KnowledgeFragments.AsNoTracking()
            join source in dbContext.KnowledgeSources.AsNoTracking()
                on fragment.KnowledgeSourceId equals source.Id
            where source.Status == ProcessingStatus.Ready
            orderby EF.Functions.CosineDistance(fragment.Embedding, queryVector)
            select new KnowledgeSearchResult(
                source.Id,
                source.Title,
                fragment.Id,
                fragment.Sequence,
                fragment.Content,
                1d - EF.Functions.CosineDistance(fragment.Embedding, queryVector)))
            .Take(limit)
            .ToListAsync(cancellationToken);

        return results;
    }
}