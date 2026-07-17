using TradeMind.Modules.Knowledge.Domain;

namespace TradeMind.Modules.Knowledge.UnitTests;

public sealed class KnowledgeDocumentTests
{
    [Fact]
    public void Create_ValidInput_RaisesCreatedEvent()
    {
        var now = DateTimeOffset.UtcNow;

        var document = KnowledgeDocument.Create("Trading plan", "manual.pdf", now);

        Assert.Equal(KnowledgeDocumentStatus.Pending, document.Status);
        Assert.Contains(document.DomainEvents, domainEvent => domainEvent is KnowledgeDocumentCreated);
    }

    [Fact]
    public void ReplaceChunks_IgnoresBlankChunks_AndMarksDocumentIndexed()
    {
        var document = KnowledgeDocument.Create("Trading plan", "manual.pdf", DateTimeOffset.UtcNow);

        document.ReplaceChunks([" First ", " ", "Second"], DateTimeOffset.UtcNow);

        Assert.Equal(KnowledgeDocumentStatus.Indexed, document.Status);
        Assert.Equal(2, document.Chunks.Count);
        Assert.Equal("First", document.Chunks.First().Content);
        Assert.Contains(document.DomainEvents, domainEvent => domainEvent is KnowledgeDocumentIndexed);
    }
}
