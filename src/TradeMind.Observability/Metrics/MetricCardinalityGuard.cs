using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability.Metrics;

public static class MetricCardinalityGuard
{
    private static readonly IReadOnlySet<string> AllowedDimensions = new HashSet<string>(StringComparer.Ordinal)
    {
        "module", "operation", "stage", "outcome", "status_class", "authentication_method", "actor_type", "environment", "service_version"
        , "connector", "mode", "error_category", "asset_class"
    };

    public static void ValidateDimensions(IReadOnlyDictionary<string, string?> dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        foreach (var dimension in dimensions)
        {
            if (!AllowedDimensions.Contains(dimension.Key))
                throw new InvalidOperationException($"Metric dimension '{dimension.Key}' is not allowed because it may create unbounded cardinality.");
            if (dimension.Value is { Length: > 64 })
                throw new InvalidOperationException($"Metric dimension '{dimension.Key}' exceeds the safe length limit.");
        }
    }

    public static IReadOnlyDictionary<string, string?> ToDictionary(MetricDimensions dimensions) =>
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["module"] = dimensions.Module,
            ["operation"] = dimensions.Operation,
            ["stage"] = dimensions.Stage,
            ["outcome"] = dimensions.Outcome,
            ["status_class"] = dimensions.StatusClass,
            ["authentication_method"] = dimensions.AuthenticationMethod,
            ["actor_type"] = dimensions.ActorType,
            ["environment"] = dimensions.Environment,
            ["service_version"] = dimensions.ServiceVersion
            ,
            ["connector"] = dimensions.Connector
            ,
            ["mode"] = dimensions.Mode
            ,
            ["error_category"] = dimensions.ErrorCategory
            ,
            ["asset_class"] = dimensions.AssetClass
        };
}
