using TradeMind.KnowledgeHub.Domain;

namespace TradeMind.KnowledgeHub.Tests.Domain;

public sealed class KnowledgeSourceTests
{
    [Fact]
    public void Constructor_ShouldInitializePendingSource()
    {
        var source = new KnowledgeSource("ICT notes", KnowledgeSourceType.Text, new string('A', 64));

        Assert.NotEqual(Guid.Empty, source.Id);
        Assert.Equal("ICT notes", source.Title);
        Assert.Equal(ProcessingStatus.Pending, source.Status);
        Assert.Empty(source.Fragments);
    }

    [Fact]
    public void AddFragment_ShouldRequireProcessingState()
    {
        var source = new KnowledgeSource("ICT notes", KnowledgeSourceType.Text, new string('A', 64));

        var action = () => source.AddFragment(0, "Liquidity sweep", 2, new float[64]);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void MarkReady_ShouldRequireAtLeastOneFragment()
    {
        var source = new KnowledgeSource("ICT notes", KnowledgeSourceType.Text, new string('A', 64));
        source.StartProcessing();

        Assert.Throws<InvalidOperationException>(source.MarkReady);
    }

    [Fact]
    public void Source_ShouldBecomeReadyAfterFragmentIsAdded()
    {
        var source = new KnowledgeSource("ICT notes", KnowledgeSourceType.Text, new string('A', 64));
        source.StartProcessing();
        source.AddFragment(0, "Liquidity sweep", 2, new float[64]);

        source.MarkReady();

        Assert.Equal(ProcessingStatus.Ready, source.Status);
        Assert.Single(source.Fragments);
        Assert.Null(source.FailureReason);
    }
}