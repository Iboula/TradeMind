using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TradeMind.AI.TradingAssistant.Tests;

public sealed class TradingAssistantTests
{
    private readonly DeterministicTradingAssistantIntentClassifier _classifier = new();

    [Theory]
    [InlineData("What is the risk?", TradingAssistantIntent.Risk)]
    [InlineData("Quel est le risque?", TradingAssistantIntent.Risk)]
    [InlineData("Show the entry and stop", TradingAssistantIntent.Plan)]
    [InlineData("Explique la décision", TradingAssistantIntent.Decision)]
    [InlineData("What should I do next?", TradingAssistantIntent.NextActions)]
    [InlineData("Show warnings and errors", TradingAssistantIntent.Issues)]
    [InlineData("Why is this setup blocked?", TradingAssistantIntent.Explain)]
    [InlineData("Give me a summary", TradingAssistantIntent.Summary)]
    public void Classify_WhenQuestionProvided_ReturnsExpectedIntent(
        string question,
        TradingAssistantIntent expected)
    {
        var result = _classifier.Classify(question);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Classify_WhenQuestionIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => _classifier.Classify(" "));
    }

    [Fact]
    public void AddTradingAssistant_RegistersPublicContracts()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTradingAssistant();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<DeterministicTradingAssistantIntentClassifier>(
            provider.GetRequiredService<ITradingAssistantIntentClassifier>());
        Assert.IsType<DeterministicTradingAssistant>(
            provider.GetRequiredService<ITradingAssistant>());
    }
}
