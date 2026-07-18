using System.Security.Cryptography;
using System.Text;
using TradeMind.Modules.KnowledgeHub.Application;
using TradeMind.Modules.KnowledgeHub.Domain;

namespace TradeMind.Modules.KnowledgeHub.Infrastructure;

internal sealed class TxtKnowledgeImporter : IKnowledgeImporter
{
    private const int MaxBytes = 10 * 1024 * 1024;
    public async Task<ImportedKnowledge> ImportAsync(Stream stream, string fileName, string mediaType, CancellationToken ct)
    {
        if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Only .txt files are supported by the MVP.");
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        if (buffer.Length == 0 || buffer.Length > MaxBytes) throw new InvalidOperationException("The file must contain between 1 byte and 10 MB.");
        return new ImportedKnowledge(buffer.ToArray(), Path.GetFileName(fileName), string.IsNullOrWhiteSpace(mediaType) ? "text/plain" : mediaType);
    }
}

internal sealed class Utf8TextExtractor : ITextExtractor
{
    public Task<string> ExtractAsync(ImportedKnowledge knowledge, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var text = Encoding.UTF8.GetString(knowledge.Content).Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("No text could be extracted.");
        return Task.FromResult(text);
    }
}

internal sealed class SlidingWindowFragmenter : IFragmenter
{
    private const int Size = 800;
    private const int Overlap = 100;
    public IReadOnlyList<string> Fragment(string text)
    {
        var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var fragments = new List<string>();
        for (var start = 0; start < normalized.Length; start += Size - Overlap)
        {
            var length = Math.Min(Size, normalized.Length - start);
            fragments.Add(normalized.Substring(start, length));
            if (start + length >= normalized.Length) break;
        }
        return fragments;
    }
}

internal sealed class FakeEmbeddingGenerator : IEmbeddingGenerator
{
    public Task<float[]> GenerateAsync(string text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var bytes = SHA512.HashData(Encoding.UTF8.GetBytes(text));
        var values = bytes.Select(b => (b / 127.5f) - 1f).ToArray();
        var magnitude = MathF.Sqrt(values.Sum(x => x * x));
        for (var i = 0; i < values.Length; i++) values[i] /= magnitude;
        return Task.FromResult(values);
    }
}

internal sealed class KnowledgeIndexer : IKnowledgeIndexer
{
    public Task IndexAsync(KnowledgeSource source, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (source.Fragments.Any(x => x.Embedding is null)) throw new InvalidOperationException("Every fragment must have an embedding before indexing.");
        return Task.CompletedTask;
    }
}
