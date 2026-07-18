using TradeMind.Modules.KnowledgeHub.Domain;
using Xunit;

namespace TradeMind.Modules.KnowledgeHub.UnitTests;

public sealed class KnowledgeSourceTests
{
    [Fact]
    public void ReplaceFragments_RejectsEmptyCollection()
    {
        var source = KnowledgeSource.Create("sample.txt", "text/plain", DateTimeOffset.UtcNow);
        source.SetExtractedText("content");
        Assert.Throws<InvalidOperationException>(() => source.ReplaceFragments([]));
    }

    [Fact]
    public void MarkIndexed_ChangesStatusAfterEmbeddingsAreAssigned()
    {
        var source = KnowledgeSource.Create("sample.txt", "text/plain", DateTimeOffset.UtcNow);
        source.SetExtractedText("content");
        source.ReplaceFragments(["content"]);
        source.Fragments.Single().SetEmbedding([1f, 0f]);
        source.MarkIndexed();
        Assert.Equal(KnowledgeSourceStatus.Indexed, source.Status);
    }
}
