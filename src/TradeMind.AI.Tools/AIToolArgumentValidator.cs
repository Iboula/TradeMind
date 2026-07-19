using Microsoft.Extensions.Options;
using System.Text.Json;

namespace TradeMind.AI.Tools;

public sealed class AIToolArgumentValidator : IAIToolArgumentValidator
{
    private readonly AIToolEngineOptions _options;

    public AIToolArgumentValidator(IOptions<AIToolEngineOptions> options)
    {
        _options = options.Value;
    }

    public AIToolArguments Validate(
        AIToolDefinition definition,
        IReadOnlyDictionary<string, JsonElement> arguments,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(arguments);

        var parameters = definition.Parameters.ToDictionary(parameter => parameter.Name, StringComparer.Ordinal);
        if (_options.RejectUnknownArguments)
        {
            var unknownArgument = arguments.Keys.FirstOrDefault(name => !parameters.ContainsKey(name));
            if (unknownArgument is not null)
            {
                throw new AIToolValidationException(
                    definition.Id,
                    $"Unknown tool argument '{unknownArgument}'.",
                    correlationId,
                    unknownArgument);
            }
        }

        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var parameter in definition.Parameters)
        {
            var hasArgument = arguments.TryGetValue(parameter.Name, out var suppliedValue);
            JsonElement? value = hasArgument ? suppliedValue : parameter.DefaultValue;

            if (value is null)
            {
                if (parameter.Required)
                {
                    throw CreateParameterException(
                        definition.Id,
                        parameter,
                        parameter.IsSensitive
                            ? "A required sensitive tool argument is missing."
                            : $"Required tool argument '{parameter.Name}' is missing.",
                        correlationId);
                }

                continue;
            }

            object? converted;
            try
            {
                converted = AIToolValueConverter.Convert(parameter, value.Value);
            }
            catch (FormatException exception)
            {
                throw CreateParameterException(
                    definition.Id,
                    parameter,
                    parameter.IsSensitive
                        ? "A sensitive tool argument has an invalid type."
                        : $"Tool argument '{parameter.Name}' has an invalid type.",
                    correlationId,
                    exception);
            }

            ValidateConstraints(definition.Id, parameter, converted, correlationId);
            normalized[parameter.Name] = converted;
        }

        return new AIToolArguments(normalized);
    }

    private static void ValidateConstraints(
        AIToolId toolId,
        AIToolParameterDefinition parameter,
        object? value,
        string? correlationId)
    {
        if (value is string text && parameter.MaxLength is int maxLength && text.Length > maxLength)
        {
            throw CreateParameterException(
                toolId,
                parameter,
                parameter.IsSensitive
                    ? "A sensitive tool argument exceeds its maximum length."
                    : $"Tool argument '{parameter.Name}' exceeds MaxLength {maxLength}.",
                correlationId);
        }

        if (value is long integer)
        {
            ValidateNumericRange(toolId, parameter, integer, correlationId);
        }
        else if (value is decimal decimalValue)
        {
            ValidateNumericRange(toolId, parameter, decimalValue, correlationId);
        }

        if (parameter.AllowedValues.Count > 0)
        {
            var isAllowed = parameter.AllowedValues
                .Select(allowed => AIToolValueConverter.Convert(parameter, allowed))
                .Any(allowed => AIToolValueConverter.AreEqual(allowed, value));

            if (!isAllowed)
            {
                throw CreateParameterException(
                    toolId,
                    parameter,
                    parameter.IsSensitive
                        ? "A sensitive tool argument is not allowed."
                        : $"Tool argument '{parameter.Name}' is not an allowed value.",
                    correlationId);
            }
        }
    }

    private static void ValidateNumericRange(
        AIToolId toolId,
        AIToolParameterDefinition parameter,
        decimal value,
        string? correlationId)
    {
        if (parameter.Minimum is not null && value < parameter.Minimum)
        {
            throw CreateParameterException(
                toolId,
                parameter,
                parameter.IsSensitive
                    ? "A sensitive tool argument is below its minimum."
                    : $"Tool argument '{parameter.Name}' is below its minimum.",
                correlationId);
        }

        if (parameter.Maximum is not null && value > parameter.Maximum)
        {
            throw CreateParameterException(
                toolId,
                parameter,
                parameter.IsSensitive
                    ? "A sensitive tool argument exceeds its maximum."
                    : $"Tool argument '{parameter.Name}' exceeds its maximum.",
                correlationId);
        }
    }

    private static AIToolValidationException CreateParameterException(
        AIToolId toolId,
        AIToolParameterDefinition parameter,
        string message,
        string? correlationId,
        Exception? innerException = null) =>
        new(
            toolId,
            message,
            correlationId,
            parameter.IsSensitive ? null : parameter.Name,
            innerException);
}
