using System.Collections.ObjectModel;

namespace TradeMind.Market.Abstractions;

internal static class MarketCollections
{
    public static IReadOnlyList<T> CopyList<T>(IEnumerable<T>? values) =>
        Array.AsReadOnly(values?.ToArray() ?? []);

    public static IReadOnlyDictionary<string, string> CopyDictionary(
        IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            var normalizedKey = MarketValueObject.Normalize(key, nameof(values));
            var normalizedValue = MarketValueObject.Normalize(value, nameof(values));
            if (!copy.TryAdd(normalizedKey, normalizedValue))
            {
                throw new ArgumentException("Dictionary contains duplicate normalized keys.", nameof(values));
            }
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }
}
