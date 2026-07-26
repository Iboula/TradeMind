using Microsoft.Extensions.Options;
using System.Globalization;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Tests;

public sealed class AIToolArgumentValidatorTests
{
    [Fact]
    public void Validator_ShouldRejectMissingRequiredParameter()
    {
        Assert.Throws<AIToolValidationException>(() => Validate(
            Parameter(AIToolParameterType.String),
            new Dictionary<string, System.Text.Json.JsonElement>()));
    }

    [Fact]
    public void Validator_ShouldApplyDefaultValue()
    {
        var parameter = Parameter(AIToolParameterType.String, required: false, defaultValue: AIToolTestData.Json("default"));
        Assert.Equal(
            "default",
            Validate(parameter, new Dictionary<string, System.Text.Json.JsonElement>()).GetRequiredString("value"));
    }

    [Fact]
    public void Validator_ShouldRejectUnknownParameter()
    {
        var exception = Assert.Throws<AIToolValidationException>(() =>
            Validate(Parameter(AIToolParameterType.String), new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["unknown"] = AIToolTestData.Json("value")
            }));
        Assert.Equal("unknown", exception.SafeParameterName);
    }

    [Fact]
    public void Validator_ShouldRejectIncorrectType()
    {
        Assert.Throws<AIToolValidationException>(() =>
            Validate(Parameter(AIToolParameterType.Integer), Arguments("not-a-number")));
    }

    [Fact]
    public void Validator_ShouldSupportString()
    {
        Assert.Equal("text", Validate(Parameter(AIToolParameterType.String), Arguments("text")).GetRequiredString("value"));
    }

    [Fact]
    public void Validator_ShouldSupportInteger()
    {
        Assert.Equal(42, Validate(Parameter(AIToolParameterType.Integer), Arguments("42")).GetRequiredInteger("value"));
    }

    [Fact]
    public void Validator_ShouldSupportDecimalUsingInvariantCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(12.5m, Validate(Parameter(AIToolParameterType.Decimal), Arguments("12.5")).GetRequiredDecimal("value"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void Validator_ShouldSupportBoolean()
    {
        Assert.True(Validate(Parameter(AIToolParameterType.Boolean), Arguments(true)).GetRequiredBoolean("value"));
    }

    [Fact]
    public void Validator_ShouldSupportDateTime()
    {
        var value = Validate(Parameter(AIToolParameterType.DateTime), Arguments("2026-01-01T12:30:00-05:00"))
            .GetRequiredDateTime("value");
        Assert.Equal(TimeSpan.Zero, value.Offset);
        Assert.Equal(17, value.Hour);
    }

    [Fact]
    public void Validator_ShouldSupportJson()
    {
        var value = Validate(Parameter(AIToolParameterType.Json), Arguments(new { nested = true })).GetRequiredJson("value");
        Assert.True(value.GetProperty("nested").GetBoolean());
    }

    [Fact]
    public void Validator_ShouldRespectMaxLength()
    {
        Assert.Throws<AIToolValidationException>(() =>
            Validate(Parameter(AIToolParameterType.String, maxLength: 3), Arguments("four")));
    }

    [Fact]
    public void Validator_ShouldRespectMinimum()
    {
        Assert.Throws<AIToolValidationException>(() =>
            Validate(Parameter(AIToolParameterType.Decimal, minimum: 10), Arguments(9)));
    }

    [Fact]
    public void Validator_ShouldRespectMaximum()
    {
        Assert.Throws<AIToolValidationException>(() =>
            Validate(Parameter(AIToolParameterType.Decimal, maximum: 10), Arguments(11)));
    }

    [Fact]
    public void Validator_ShouldRespectAllowedValues()
    {
        var parameter = Parameter(
            AIToolParameterType.String,
            allowedValues: [AIToolTestData.Json("buy"), AIToolTestData.Json("sell")]);
        Assert.Throws<AIToolValidationException>(() => Validate(parameter, Arguments("hold")));
    }

    [Fact]
    public void Validator_ShouldNotRevealSensitiveValue()
    {
        const string secretValue = "private-value";
        var parameter = Parameter(AIToolParameterType.Integer, isSensitive: true);
        var exception = Assert.Throws<AIToolValidationException>(() => Validate(parameter, Arguments(secretValue)));

        Assert.DoesNotContain(secretValue, exception.ToString(), StringComparison.Ordinal);
        Assert.Null(exception.SafeParameterName);
    }

    [Fact]
    public void Validator_ShouldProduceImmutableArguments()
    {
        var result = Validate(Parameter(AIToolParameterType.String), Arguments("value"));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, object?>)result.Values).Add("other", "value"));
    }

    private static AIToolArguments Validate(
        AIToolParameterDefinition parameter,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> arguments) =>
        new AIToolArgumentValidator(Options.Create(new AIToolEngineOptions()))
            .Validate(AIToolTestData.Definition(parameters: [parameter]), arguments, "correlation");

    private static Dictionary<string, System.Text.Json.JsonElement> Arguments<T>(T value) =>
        new() { ["value"] = AIToolTestData.Json(value) };

    private static AIToolParameterDefinition Parameter(
        AIToolParameterType type,
        bool required = true,
        System.Text.Json.JsonElement? defaultValue = null,
        int? maxLength = null,
        decimal? minimum = null,
        decimal? maximum = null,
        IReadOnlyList<System.Text.Json.JsonElement>? allowedValues = null,
        bool isSensitive = false) =>
        new(
            "value",
            type,
            required,
            "Value",
            defaultValue,
            maxLength,
            minimum,
            maximum,
            allowedValues,
            isSensitive);
}
