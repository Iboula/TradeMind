using System.Text.Json;

namespace TradeMind.ExecutionSessions.Infrastructure.Persistence;

internal static class ExecutionSessionSerialization
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(IReadOnlyDictionary<string, string> values) =>
        JsonSerializer.Serialize(values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), Options);

    public static IReadOnlyDictionary<string, string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, Options)
            ?? new Dictionary<string, string>();
    }
}
