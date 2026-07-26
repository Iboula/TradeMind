using System.Globalization;
using System.Text.Json;

namespace TradeMind.AI.Tools;

internal static class AIToolValueConverter
{
    public static object? Convert(AIToolParameterDefinition parameter, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            if (!parameter.IsNullable)
            {
                throw new FormatException("Null is not allowed.");
            }

            return null;
        }

        return parameter.Type switch
        {
            AIToolParameterType.String => ConvertString(value),
            AIToolParameterType.Integer => ConvertInteger(value),
            AIToolParameterType.Decimal => ConvertDecimal(value),
            AIToolParameterType.Boolean => ConvertBoolean(value),
            AIToolParameterType.DateTime => ConvertDateTime(value),
            AIToolParameterType.Json => value.Clone(),
            _ => throw new FormatException("Unsupported tool parameter type.")
        };
    }

    public static bool AreEqual(object? left, object? right)
    {
        if (left is JsonElement leftJson && right is JsonElement rightJson)
        {
            return JsonElement.DeepEquals(leftJson, rightJson);
        }

        return Equals(left, right);
    }

    private static string ConvertString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : throw new FormatException("A JSON string is required.");

    private static long ConvertInteger(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var numericValue))
        {
            return numericValue;
        }

        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numericValue))
        {
            return numericValue;
        }

        throw new FormatException("An invariant integer is required.");
    }

    private static decimal ConvertDecimal(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var numericValue))
        {
            return numericValue;
        }

        if (value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out numericValue))
        {
            return numericValue;
        }

        throw new FormatException("An invariant decimal is required.");
    }

    private static bool ConvertBoolean(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        if (value.ValueKind == JsonValueKind.String
            && bool.TryParse(value.GetString(), out var booleanValue))
        {
            return booleanValue;
        }

        throw new FormatException("A boolean is required.");
    }

    private static DateTimeOffset ConvertDateTime(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dateTimeValue))
        {
            return dateTimeValue;
        }

        throw new FormatException("An invariant ISO-8601 date is required.");
    }
}
