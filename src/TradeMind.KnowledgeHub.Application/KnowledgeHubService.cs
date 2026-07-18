using System.Security.Cryptography;
using TradeMind.KnowledgeHub.Domain;

namespace TradeMind.KnowledgeHub.Application;

public sealed class KnowledgeHubService(
    ITextExtractor extractor,
    IFragmenter fragmenter,
    IEmbeddingGenerator embeddingGenerator,
    IKnowledgeSourceRepository repository)
{
    public async Task<Guid> IngestAsync(IngestKnowledgeSourceRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);

        using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        if (await repository.ExistsByHashAsync(hash, cancellationToken))
        {
            throw new InvalidOperationException("This source has already been ingested.");
        }

        var type = Path.GetExtension(request.FileName).Equals(".md", StringComparison.OrdinalIgnoreCase)
            ? KnowledgeSourceType.Markdown
            : KnowledgeSourceType.Text;

        var source = new KnowledgeSource(request.Title, type, hash);
        await repository.AddAsync(source, cancellationToken);
        source.StartProcessing();

        try
        {
            buffer.Position = 0;
            var text = await extractor.ExtractAsync(buffer, request.FileName, cancellationToken);
            var fragments = fragmenter.Fragment(text);

            for (var index = 0; index < fragments.Count; index++)
            {
                var content = fragments[index];
                var embedding = await embeddingGenerator.GenerateAsync(content, cancellationToken);
                source.AddFragment(index, content, CountTokens(content), embedding);
            }

            source.MarkReady();
            await repository.SaveChangesAsync(cancellationToken);
            return source.Id;
        }
        catch (Exception exception)
        {
            source.MarkFailed(exception.Message);
            await repository.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public Task<KnowledgeSource?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        repository.GetAsync(id, cancellationToken);

    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var safeLimit = Math.Clamp(limit, 1, 20);
        var embedding = await embeddingGenerator.GenerateAsync(query, cancellationToken);
        return await repository.SearchAsync(embedding, safeLimit, cancellationToken);
    }

    private static int CountTokens(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
