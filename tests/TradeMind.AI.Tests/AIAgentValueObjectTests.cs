using TradeMind.AI.Agents;

namespace TradeMind.AI.Tests;

public sealed class AIAgentValueObjectTests
{
    [Fact]
    public void AIAgentId_AcceptsValidIdentifier()
    {
        Assert.Equal("generic-assistant", new AIAgentId("generic-assistant").Value);
    }

    [Fact]
    public void AIAgentId_RejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new AIAgentId(string.Empty));
    }

    [Fact]
    public void AIAgentId_RejectsSpaces()
    {
        Assert.Throws<ArgumentException>(() => new AIAgentId("generic assistant"));
    }

    [Fact]
    public void AIAgentId_RejectsInvalidFormat()
    {
        Assert.Throws<ArgumentException>(() => new AIAgentId("Generic_Assistant"));
    }

    [Fact]
    public void AIAgentId_RejectsExcessiveLength()
    {
        Assert.Throws<ArgumentException>(() => new AIAgentId(new string('a', 65)));
    }

    [Fact]
    public void AIAgentVersion_AcceptsSemanticVersion()
    {
        Assert.Equal("1.0.0", AIAgentVersion.Parse("1.0.0").ToString());
    }

    [Fact]
    public void AIAgentVersion_UsesSemanticOrdering()
    {
        Assert.True(AIAgentVersion.Parse("1.10.0").CompareTo(AIAgentVersion.Parse("1.2.0")) > 0);
    }

    [Fact]
    public void AIAgentVersion_RejectsInvalidFormat()
    {
        Assert.Throws<ArgumentException>(() => AIAgentVersion.Parse("1.0"));
    }

    [Fact]
    public void Definition_RequiresName()
    {
        var definition = AIAgentTestData.Definition();
        Assert.Throws<ArgumentException>(() => new AIAgentDefinition(
            definition.Id,
            string.Empty,
            definition.Description,
            definition.Version,
            definition.Availability,
            definition.Capabilities,
            definition.Policy));
    }

    [Fact]
    public void Definition_RequiresDescription()
    {
        var definition = AIAgentTestData.Definition();
        Assert.Throws<ArgumentException>(() => new AIAgentDefinition(
            definition.Id,
            definition.Name,
            string.Empty,
            definition.Version,
            definition.Availability,
            definition.Capabilities,
            definition.Policy));
    }

    [Fact]
    public void Definition_RequiresVersion()
    {
        var definition = AIAgentTestData.Definition();
        Assert.Throws<ArgumentNullException>(() => new AIAgentDefinition(
            definition.Id,
            definition.Name,
            definition.Description,
            null!,
            definition.Availability,
            definition.Capabilities,
            definition.Policy));
    }

    [Fact]
    public void Definition_RejectsInvalidTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AIAgentPolicy(
            maximumExecutionDuration: TimeSpan.Zero));
    }

    [Fact]
    public void Definition_CollectionsAreImmutable()
    {
        var source = new List<string> { "execute" };
        var definition = AIAgentTestData.Definition(permissions: source);
        source.Add("admin");

        Assert.DoesNotContain("admin", definition.RequiredPermissions);
        Assert.Throws<NotSupportedException>(() => ((ICollection<string>)definition.RequiredPermissions).Add("write"));
    }

    [Fact]
    public void Definition_MetadataIsImmutableAndFiltered()
    {
        var source = new Dictionary<string, string> { ["public"] = "value", ["api-token"] = "sensitive" };
        var definition = AIAgentTestData.Definition(metadata: source);
        source["public"] = "changed";

        Assert.Equal("value", definition.Metadata["public"]);
        Assert.DoesNotContain("api-token", definition.Metadata.Keys);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)definition.Metadata).Add("other", "value"));
    }

    [Fact]
    public void Capabilities_AutonomousExecutionIsFalseByDefault()
    {
        Assert.False(new AIAgentCapabilities().SupportsAutonomousExecution);
    }

    [Fact]
    public void Capabilities_ToolCallingIsFalseByDefault()
    {
        Assert.False(new AIAgentCapabilities().SupportsToolCalling);
    }
}
