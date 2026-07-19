using System.Collections.ObjectModel;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Tests;

public sealed class AIToolValueObjectTests
{
    [Fact]
    public void AIToolId_ShouldAcceptValidIdentifier()
    {
        Assert.Equal("calculate-position-size", new AIToolId("calculate-position-size").Value);
    }

    [Fact]
    public void AIToolId_ShouldRejectEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(() => new AIToolId(string.Empty));
    }

    [Fact]
    public void AIToolId_ShouldRejectSpaces()
    {
        Assert.Throws<ArgumentException>(() => new AIToolId("add numbers"));
    }

    [Fact]
    public void AIToolId_ShouldRejectInvalidFormat()
    {
        Assert.Throws<ArgumentException>(() => new AIToolId("Add_Numbers"));
        Assert.Throws<ArgumentException>(() => new AIToolId("add--numbers"));
    }

    [Fact]
    public void Definition_ShouldRequireName()
    {
        Assert.Throws<ArgumentException>(() => new AIToolDefinition(
            new AIToolId("tool"),
            " ",
            "Description",
            "1.0"));
    }

    [Fact]
    public void Definition_ShouldRequireDescription()
    {
        Assert.Throws<ArgumentException>(() => new AIToolDefinition(
            new AIToolId("tool"),
            "Tool",
            " ",
            "1.0"));
    }

    [Fact]
    public void Definition_ShouldRejectDuplicateParameters()
    {
        var parameter = StringParameter("value");
        Assert.Throws<ArgumentException>(() => AIToolTestData.Definition(parameters: [parameter, StringParameter("value")]));
    }

    [Fact]
    public void Definition_ShouldRejectInvalidTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AIToolTestData.Definition(timeout: TimeSpan.Zero));
    }

    [Fact]
    public void Definition_ShouldKeepPermissionsImmutable()
    {
        var input = new List<string> { "tool.use" };
        var definition = AIToolTestData.Definition(permissions: input);
        input.Add("tool.admin");

        Assert.Equal(["tool.use"], definition.RequiredPermissions);
        Assert.Throws<NotSupportedException>(() => ((ICollection<string>)definition.RequiredPermissions).Add("other"));
    }

    [Fact]
    public void Definition_ShouldKeepMetadataImmutable()
    {
        var input = new Dictionary<string, string> { ["owner"] = "ai" };
        var definition = new AIToolDefinition(
            new AIToolId("tool"),
            "Tool",
            "Description",
            "1.0",
            metadata: input);
        input["owner"] = "changed";

        Assert.Equal("ai", definition.Metadata["owner"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)definition.Metadata).Add("new", "value"));
    }

    [Fact]
    public void Parameter_ShouldRejectInvalidMaxLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StringParameter("value", maxLength: 0));
    }

    [Fact]
    public void Parameter_ShouldRejectIncoherentBounds()
    {
        Assert.Throws<ArgumentException>(() => new AIToolParameterDefinition(
            "value",
            AIToolParameterType.Decimal,
            true,
            "Value",
            minimum: 10,
            maximum: 1));
    }

    [Fact]
    public void Parameter_ShouldRequireCompatibleDefaultValue()
    {
        Assert.Throws<ArgumentException>(() => new AIToolParameterDefinition(
            "value",
            AIToolParameterType.Integer,
            true,
            "Value",
            defaultValue: AIToolTestData.Json("not-an-integer")));
    }

    private static AIToolParameterDefinition StringParameter(string name, int? maxLength = null) =>
        new(name, AIToolParameterType.String, true, "Value", maxLength: maxLength);
}
