using System.Collections.Concurrent;
using System.Diagnostics;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability.Activities;

public static class TradeMindActivitySource
{
    private static readonly ConcurrentDictionary<string, ActivitySource> Sources = new(StringComparer.Ordinal);

    public static ActivitySource Get(string name) => Sources.GetOrAdd(name, static sourceName => new ActivitySource(sourceName));

    public static IReadOnlyList<ActivitySource> All =>
        TelemetryActivityNames.All.Select(Get).ToArray();
}
