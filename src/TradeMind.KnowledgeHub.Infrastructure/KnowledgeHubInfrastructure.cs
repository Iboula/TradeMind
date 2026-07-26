using System.Collections.Concurrent;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector.EntityFrameworkCore;
using TradeMind.KnowledgeHub.Application;
using TradeMind.KnowledgeHub.Domain;

namespace TradeMind.KnowledgeHub.Infrastructure;

public sealed class PlainTextExtractor : ITextExtractor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".md"
    };

    public async Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new NotSupportedException($"The '{extension}' format is not supported yet.");
        }

        using var reader = new StreamReader(content, Encoding.UTF8, true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(text)
            ? throw new InvalidOperationException("The source contains no extractable text.")
            : text;
    }
}

public sealed class SlidingWindowFragmenter(int maximumWords = 180, int overlapWords = 30) : IFragmenter
{
    public IReadOnlyList<string> Fragment(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return [];
        }

        var fragments = new List<string>();
        var step = Math.Max(1, maximumWords - overlapWords);
        for (var start = 0; start < words.Length; start += step)
        {
            fragments.Add(string.Join(' ', words.Skip(start).Take(maximumWords)));
            if (start + maximumWords >= words.Length)
            {
                break;
            }
        }

        return fragments;
    }
}

public sealed class DeterministicEmbeddingGenerator : IEmbeddingGenerator
{
    public int Dimensions => 64;

    public Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var vector = new float[Dimensions];
        var tokens = text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            var hash = StringComparer.Ordinal.GetHashCode(token);
            var index = (hash & int.MaxValue) % Dimensions;
            vector[index] += (hash & 1) == 0 ? 1f : -1f;
        }

        var norm = MathF.Sqrt(vector.Sum(value => value * value));
        if (norm > 0)
        {
            for (var index = 0; index < vector.Length; index++)
            {
                vector[index] /= norm;
            }
        }

        return Task.FromResult(vector);
    }
}

public sealed class InMemoryKnowledgeSourceRepository : IKnowledgeSourceRepository
{
    private readonly ConcurrentDictionary<Guid, KnowledgeSource> _sources = new();

    public Task<bool> ExistsByHashAsync(string hash, CancellationToken cancellationToken) =>
        Task.FromResult(_sources.Values.Any(source => source.ContentHash == hash));

    public Task AddAsync(KnowledgeSource source, CancellationToken cancellationToken)
    {
        if (!_sources.TryAdd(source.Id, source))
        {
            throw new InvalidOperationException("The source could not be added.");
        }

        return Task.CompletedTask;
    }

    public Task<KnowledgeSource?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _sources.TryGetValue(id, out var source);
        return Task.FromResult(source);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        float[] embedding,
        int limit,
        CancellationToken cancellationToken)
    {
        var results = _sources.Values
            .Where(source => source.Status == ProcessingStatus.Ready)
            .SelectMany(source => source.Fragments.Select(fragment => new KnowledgeSearchResult(
                source.Id,
                source.Title,
                fragment.Id,
                fragment.Sequence,
                fragment.Content,
                CosineSimilarity(embedding, fragment.Embedding))))
            .OrderByDescending(result => result.Score)
            .Take(limit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<KnowledgeSearchResult>>(results);
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length != right.Length)
        {
            return 0;
        }

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;
        for (var index = 0; index < left.Length; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }

        return leftNorm == 0 || rightNorm == 0 ? 0 : dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddKnowledgeHub(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("KnowledgeHub")
            ?? throw new InvalidOperationException("Connection string 'KnowledgeHub' is required.");

        services.AddDbContext<KnowledgeHubDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(KnowledgeHubDbContext).Assembly.FullName);
                npgsql.UseVector();
            }));

        services.AddSingleton<ITextExtractor, PlainTextExtractor>();
        services.AddSingleton<IFragmenter>(_ => new SlidingWindowFragmenter());
        services.TryAddSingleton<IEmbeddingGenerator, DeterministicEmbeddingGenerator>();
        services.AddScoped<IKnowledgeSourceRepository, PostgreSqlKnowledgeSourceRepository>();
        services.AddScoped<KnowledgeHubService>();
        return services;
    }

    public static IServiceCollection AddKnowledgeHubAIEmbeddings(this IServiceCollection services)
    {
        services.AddSingleton<IEmbeddingGenerator, AIEmbeddingGeneratorAdapter>();
        return services;
    }
}
