using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradeMind.Modules.KnowledgeHub.Application;
using TradeMind.Modules.KnowledgeHub.Domain;

namespace TradeMind.Modules.KnowledgeHub.Infrastructure;

internal sealed class KnowledgeSourceRow { public Guid Id { get; set; } public string FileName { get; set; } = ""; public string MediaType { get; set; } = ""; public string Status { get; set; } = ""; public DateTimeOffset CreatedAtUtc { get; set; } public List<KnowledgeFragmentRow> Fragments { get; set; } = []; }
internal sealed class KnowledgeFragmentRow { public Guid Id { get; set; } public Guid SourceId { get; set; } public int Position { get; set; } public string Content { get; set; } = ""; public KnowledgeSourceRow Source { get; set; } = null!; public EmbeddingRow Embedding { get; set; } = null!; }
internal sealed class EmbeddingRow { public Guid Id { get; set; } public Guid FragmentId { get; set; } public float[] Values { get; set; } = []; public KnowledgeFragmentRow Fragment { get; set; } = null!; }

internal sealed class KnowledgeHubDbContext(DbContextOptions<KnowledgeHubDbContext> options) : DbContext(options), IKnowledgeHubUnitOfWork
{
    public DbSet<KnowledgeSourceRow> Sources => Set<KnowledgeSourceRow>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KnowledgeSourceRow>(b => { b.ToTable("knowledge_sources"); b.HasKey(x => x.Id); b.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(512); b.Property(x => x.MediaType).HasColumnName("media_type").HasMaxLength(128); b.Property(x => x.Status).HasColumnName("status").HasMaxLength(32); b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc"); });
        modelBuilder.Entity<KnowledgeFragmentRow>(b => { b.ToTable("knowledge_fragments"); b.HasKey(x => x.Id); b.Property(x => x.SourceId).HasColumnName("source_id"); b.Property(x => x.Position).HasColumnName("position"); b.Property(x => x.Content).HasColumnName("content"); b.HasOne(x => x.Source).WithMany(x => x.Fragments).HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<EmbeddingRow>(b => { b.ToTable("embeddings"); b.HasKey(x => x.Id); b.Property(x => x.FragmentId).HasColumnName("fragment_id"); b.Property(x => x.Values).HasColumnName("values").HasColumnType("real[]"); b.HasOne(x => x.Fragment).WithOne(x => x.Embedding).HasForeignKey<EmbeddingRow>(x => x.FragmentId).OnDelete(DeleteBehavior.Cascade); });
    }
    Task IKnowledgeHubUnitOfWork.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);
}

internal sealed class KnowledgeSourceRepository(KnowledgeHubDbContext db) : IKnowledgeSourceRepository
{
    public Task AddAsync(KnowledgeSource source, CancellationToken ct)
    {
        db.Add(new KnowledgeSourceRow { Id = source.Id, FileName = source.FileName, MediaType = source.MediaType, Status = source.Status.ToString(), CreatedAtUtc = source.CreatedAtUtc, Fragments = source.Fragments.Select(f => new KnowledgeFragmentRow { Id = f.Id, SourceId = source.Id, Position = f.Position, Content = f.Content, Embedding = new EmbeddingRow { Id = f.Embedding!.Id, FragmentId = f.Id, Values = f.Embedding.Values } }).ToList() });
        return Task.CompletedTask;
    }

    public async Task<KnowledgeSourceDto?> GetAsync(Guid id, CancellationToken ct) => await db.Sources.AsNoTracking().Where(x => x.Id == id).Select(x => new KnowledgeSourceDto(x.Id, x.FileName, x.MediaType, x.Status, x.Fragments.Count, x.CreatedAtUtc)).SingleOrDefaultAsync(ct);
}

internal sealed class KnowledgeSearcher(NpgsqlDataSource dataSource, IEmbeddingGenerator generator) : IKnowledgeSearcher
{
    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        var vector = await generator.GenerateAsync(query, ct);
        const string sql = """
        SELECT s.id AS SourceId, f.id AS FragmentId, s.file_name AS FileName, f.content AS Content,
               1 - sqrt((SELECT sum(power(a-b, 2)) FROM unnest(e.values, @Vector) AS t(a,b))) AS Score
        FROM embeddings e JOIN knowledge_fragments f ON f.id=e.fragment_id JOIN knowledge_sources s ON s.id=f.source_id
        ORDER BY sqrt((SELECT sum(power(a-b, 2)) FROM unnest(e.values, @Vector) AS t(a,b))) LIMIT @Limit;
        """;
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<KnowledgeSearchResult>(new CommandDefinition(sql, new { Vector = vector, Limit = limit }, cancellationToken: ct));
        return rows.AsList();
    }
}
