using System.Security.Cryptography;
using System.Text;
using TradeMind.Modules.Knowledge.Application;
using TradeMind.Modules.Knowledge.Domain;

namespace TradeMind.Modules.Knowledge.Infrastructure;

internal sealed class TxtKnowledgeImporter(
    IEnumerable<ITextExtractor> extractors,
    IFragmenter fragmenter,
    IEmbeddingGenerator embeddingGenerator,
    IKnowledgeSourceRepository repository,
    IKnowledgeIndexer indexer,
    IKnowledgeUnitOfWork unitOfWork,
    IKnowledgeClock clock) : IKnowledgeImporter
{
    public async Task<KnowledgeSource> ImportAsync(
        ImportKnowledgeSourceCommand command,
        CancellationToken cancellationToken)
    {
        var extractor = extractors.FirstOrDefault(candidate => candidate.Supports(command.MediaType))
            ?? throw new NotSupportedException($"Media type '{command.MediaType}' is not supported.");

        var source = KnowledgeSource.Import(command.Name, command.MediaType, clock.UtcNow);
        var extracted = await extractor.ExtractAsync(command.Content, cancellationToken);
        var normalized = Normalize(extracted);
        var fragments = fragmenter.Fragment(normalized);
        var embeddings = new List<float[]>(fragments.Count);

        foreach (var fragment in fragments)
        {
            embeddings.Add(await embeddingGenerator.GenerateAsync(fragment, cancellationToken));
        }

        source.ReplaceFragments(fragments, embeddings, clock.UtcNow);
        await repository.AddAsync(source, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await indexer.IndexAsync(source, cancellationToken);
        return source;
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

internal sealed class PlainTextExtractor : ITextExtractor
{
    public bool Supports(string mediaType) =>
        string.Equals(mediaType, "text/plain", StringComparison.OrdinalIgnoreCase);

    public async Task<string> ExtractAsync(Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var reader = new StreamReader(content, Encoding.UTF8, true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidDataException("The knowledge source contains no text.");
        }

        return text;
    }
}

internal sealed class SlidingWindowFragmenter : IFragmenter
{
    private const int MaximumCharacters = 800;
    private const int OverlapCharacters = 120;

    public IReadOnlyList<string> Fragment(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var fragments = new List<string>();
        var offset = 0;

        while (offset < text.Length)
        {
            var length = Math.Min(MaximumCharacters, text.Length - offset);
            var fragment = text.Substring(offset, length).Trim();
            if (fragment.Length > 0)
            {
                fragments.Add(fragment);
            }

            if (offset + length >= text.Length)
            {
                break;
            }

            offset += MaximumCharacters - OverlapCharacters;
        }

        return fragments;
    }
}

internal sealed class FakeEmbeddingGenerator : IEmbeddingGenerator
{
    public const int VectorDimensions = 64;
    public int Dimensions => VectorDimensions;

    public Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        cancellationToken.ThrowIfCancellationRequested();

        var vector = new float[VectorDimensions];
        var tokens = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = BitConverter.ToUInt16(hash, 0) % VectorDimensions;
            var sign = (hash[2] & 1) == 0 ? 1f : -1f;
            vector[index] += sign;
        }

        var magnitude = MathF.Sqrt(vector.Sum(static value => value * value));
        if (magnitude > 0)
        {
            for (var index = 0; index < vector.Length; index++)
            {
                vector[index] /= magnitude;
            }
        }

        return Task.FromResult(vector);
    }
}

internal sealed class DatabaseKnowledgeIndexer : IKnowledgeIndexer
{
    public Task IndexAsync(KnowledgeSource source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

internal sealed class SystemKnowledgeClock : IKnowledgeClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
