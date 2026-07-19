using Microsoft.Extensions.Options;
using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Domain;

namespace TradeMind.MarketConnectors.Application;

public sealed class MarketConnectorCoreOptions
{
    public int MaximumPayloadBytes { get; set; } = 1_048_576;

    public int SupportedSchemaVersion { get; set; } = 1;

    public int MaximumMetadataEntries { get; set; } = 64;

    public int MaximumMetadataKeyLength { get; set; } = 128;

    public int MaximumMetadataValueLength { get; set; } = 1024;

    public SnapshotFreshnessPolicy FreshnessPolicy { get; set; } = new(
        TimeSpan.FromSeconds(5),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5));

    public ConnectorHealthPolicy HealthPolicy { get; set; } = new(
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2));
}

public sealed class MarketConnectorCoreOptionsValidator : IValidateOptions<MarketConnectorCoreOptions>
{
    public ValidateOptionsResult Validate(string? name, MarketConnectorCoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        AddPositiveFailure(options.MaximumPayloadBytes, nameof(options.MaximumPayloadBytes), failures);
        AddPositiveFailure(options.SupportedSchemaVersion, nameof(options.SupportedSchemaVersion), failures);
        AddPositiveFailure(options.MaximumMetadataEntries, nameof(options.MaximumMetadataEntries), failures);
        AddPositiveFailure(options.MaximumMetadataKeyLength, nameof(options.MaximumMetadataKeyLength), failures);
        AddPositiveFailure(options.MaximumMetadataValueLength, nameof(options.MaximumMetadataValueLength), failures);

        if (options.FreshnessPolicy is null)
        {
            failures.Add("FreshnessPolicy is required.");
        }

        if (options.HealthPolicy is null)
        {
            failures.Add("HealthPolicy is required.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void AddPositiveFailure(int value, string name, ICollection<string> failures)
    {
        if (value <= 0)
        {
            failures.Add($"{name} must be greater than zero.");
        }
    }
}
