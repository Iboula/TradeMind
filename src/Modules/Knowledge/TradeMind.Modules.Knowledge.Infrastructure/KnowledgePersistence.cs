using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using TradeMind.Modules.Knowledge.Application;
using TradeMind.Modules.Knowledge.Domain;

namespace TradeMind.Modules.Knowledge.Infrastructure;

internal sealed class KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options)
    : DbContext(options), IKnowledgeUnitOfWork
{
    public DbSet<KnowledgeSourceEntity> Sources => Set<KnowledgeSourceEntity>();
    public DbSet<KnowledgeFragmentEntity> Fragments => Set<KnowledgeFragmentEntity>();
    public DbSet<EmbeddingEntity> Embeddings => Set<EmbeddingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<KnowledgeSourceEntity>(builder =>
        {
            builder.ToTable("knowledge_sources");
            builder.HasKey(entity => entity.Id);
            builder.Property(entity => entity.Name).HasMaxLength(512).IsRequired();
            builder.Property(entity => entity.MediaType).HasMaxLength(128).IsRequired();
            builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            builder.HasMany(entity => entity.Fragments)
                .WithOne(entity => entity.Source)
                .HasForeignKey(entity => entity.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeFragmentEntity>(builder =>
        {
            builder.ToTable("knowledge_fragments");
            builder.HasKey(entity => entity.Id);
            builder.Property(entity => entity.Content).IsRequired();
            builder.HasIndex(entity => new { entity.SourceId, entity.Position }).IsUnique();
            builder.HasOne(entity => entity.Embedding)
                .WithOne(entity => entity.Fragment)
                .HasForeignKey<EmbeddingEntity>(entity => entity.FragmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmbeddingEntity>(builder =>
        {
            builder.ToTable("embeddings");
            builder.HasKey(entity => entity.FragmentId);
            builder.Property(entity => entity.Vector)
                .HasColumnType($"vector({FakeEmbeddingGenerator.VectorDimensions})")
                .IsRequired();
            builder.HasIndex(entity => entity.Vector)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
        });
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await base.SaveChangesAsync(cancellationToken);
}

internal sealed class KnowledgeSourceRepository(KnowledgeDbContext context) : IKnowledgeSourceRepository
{
    public async Task AddAsync(KnowledgeSource source, CancellationToken cancellationToken)
    {
        var entity = new KnowledgeSourceEntity
        {
            Id = source.Id,
            Name = source.Name,
            MediaType = source.MediaType,
            Status = source.Status,
            CreatedOnUtc = source.CreatedOnUtc,
            IndexedOnUtc = source.IndexedOnUtc,
            Fragments = source.Fragments.Select(fragment => new KnowledgeFragmentEntity
            {
                Id = fragment.Id,
                SourceId = fragment.SourceId,
                Position = fragment.Position,
                Content = fragment.Content,
                Embedding = new EmbeddingEntity
                {
                    FragmentId = fragment.Id,
                    Vector = new Vector(fragment.Embedding.Vector)
                }
            }).ToList()
        };

        await context.Sources.AddAsync(entity, cancellationToken);
    }

    public async Task<KnowledgeSource?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await context.Sources
            .AsNoTracking()
            .Include(source => source.Fragments)
            .ThenInclude(fragment => fragment.Embedding)
            .SingleOrDefaultAsync(source => source.Id == id, cancellationToken);

        return entity is null
            ? null
            : KnowledgeSource.Restore(
                entity.Id,
                entity.Name,
                entity.MediaType,
                entity.Status,
                entity.CreatedOnUtc,
                entity.IndexedOnUtc,
                entity.Fragments.Select(fragment => KnowledgeFragment.Restore(
                    fragment.Id,
                    fragment.SourceId,
                    fragment.Position,
                    fragment.Content,
                    fragment.Embedding.Vector.ToArray())));
    }
}

internal sealed class PgvectorKnowledgeSearcher(
    KnowledgeDbContext context,
    IEmbeddingGenerator embeddingGenerator) : IKnowledgeSearcher
{
    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var queryVector = new Vector(await embeddingGenerator.GenerateAsync(query, cancellationToken));

        return await context.Fragments
            .AsNoTracking()
            .Where(fragment => fragment.Embedding != null)
            .OrderBy(fragment => fragment.Embedding.Vector.CosineDistance(queryVector))
            .Take(limit)
            .Select(fragment => new KnowledgeSearchResult(
                fragment.SourceId,
                fragment.Id,
                fragment.Source.Name,
                fragment.Content,
                1d - fragment.Embedding.Vector.CosineDistance(queryVector)))
            .ToListAsync(cancellationToken);
    }
}

internal sealed class KnowledgeSourceEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public KnowledgeSourceStatus Status { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }
    public DateTimeOffset? IndexedOnUtc { get; set; }
    public List<KnowledgeFragmentEntity> Fragments { get; set; } = [];
}

internal sealed class KnowledgeFragmentEntity
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public int Position { get; set; }
    public string Content { get; set; } = string.Empty;
    public KnowledgeSourceEntity Source { get; set; } = null!;
    public EmbeddingEntity Embedding { get; set; } = null!;
}

internal sealed class EmbeddingEntity
{
    public Guid FragmentId { get; set; }
    public Vector Vector { get; set; } = null!;
    public KnowledgeFragmentEntity Fragment { get; set; } = null!;
}
