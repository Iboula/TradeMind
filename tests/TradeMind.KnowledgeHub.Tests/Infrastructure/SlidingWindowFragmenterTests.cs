using TradeMind.KnowledgeHub.Infrastructure;

namespace TradeMind.KnowledgeHub.Tests.Infrastructure;

public sealed class SlidingWindowFragmenterTests
{
    [Fact]
    public void Fragment_ShouldPreserveOverlap()
    {
        var fragmenter = new SlidingWindowFragmenter(maximumWords: 4, overlapWords: 1);

        var fragments = fragmenter.Fragment("one two three four five six seven");

        Assert.Equal(2, fragments.Count);
        Assert.Equal("one two three four", fragments[0]);
        Assert.Equal("four five six seven", fragments[1]);
    }

    [Fact]
    public void Fragment_ShouldReturnEmptyCollectionForWhitespace()
    {
        var fragmenter = new SlidingWindowFragmenter();

        var fragments = fragmenter.Fragment("   ");

        Assert.Empty(fragments);
    }
}