using System.Collections.ObjectModel;

namespace TradeMind.ExecutionSessions.Domain;

public static class ExecutionSessionMetadata
{
    public const int MaximumEntries = 64;
    public const int MaximumKeyLength = 128;
    public const int MaximumValueLength = 2048;

    public static IReadOnlyDictionary<string, string> Copy(
        IReadOnlyDictionary<string, string>? metadata)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        if (metadata is not null)
        {
            foreach (var pair in metadata.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
                ArgumentNullException.ThrowIfNull(pair.Value);
                var key = pair.Key.Trim();
                if (key.Length > MaximumKeyLength || pair.Value.Length > MaximumValueLength)
                {
                    throw new ArgumentException("Execution session metadata exceeds configured limits.", nameof(metadata));
                }

                copy[key] = pair.Value;
            }
        }

        if (copy.Count > MaximumEntries)
        {
            throw new ArgumentException("Execution session metadata exceeds the maximum entry count.", nameof(metadata));
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }
}
