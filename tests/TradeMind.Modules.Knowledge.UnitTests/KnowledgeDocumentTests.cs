using TradeMind.Modules.Knowledge.Domain;

namespace TradeMind.Modules.Knowledge.UnitTests;

public sealed class KnowledgeSourceTests
{
    [Fact]
    public void Import_ValidInput_RaisesImportedEvent()
    {
        var now = DateTimeOffset.UtcNow;

        var source = KnowledgeSource.Import("Trading plan", "text/plain", now);

        Assert.Equal(KnowledgeSourceStatus.Imported, source.Status);
        Assert.Contains(source.DomainEvents, domainEvent => domainEvent is KnowledgeSourceImported);
    }

    [Fact]
    public void ReplaceFragments_AssignsEmbeddings_AndMarksSourceIndexed()
    {
        var source = KnowledgeSource.Import("Trading plan", "text/plain", DateTimeOffset.UtcNow);

        source.ReplaceFragments(
            ["First", "Second"],
            [new float[] { 1f, 0f }, new float[] { 0f, 1f }],
            DateTimeOffset.UtcNow);

        Assert.Equal(KnowledgeSourceStatus.Indexed, source.Status);
        Assert.Equal(2, source.Fragments.Count);
        Assert.All(source.Fragments, fragment => Assert.NotEmpty(fragment.Embedding.Vector));
        Assert.Contains(source.DomainEvents, domainEvent => domainEvent is KnowledgeSourceIndexed);
    }

    [Fact]
    public void ReplaceFragments_WithDifferentEmbeddingCount_Throws()
    {
        var source = KnowledgeSource.Import("Trading plan", "text/plain", DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => source.ReplaceFragments(
            ["First", "Second"],
            [new float[] { 1f, 0f }],
            DateTimeOffset.UtcNow));
    }
}
