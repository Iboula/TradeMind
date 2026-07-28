using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TradeMind.Observability.Snapshots;

public sealed record TelemetrySnapshotStage
{
    public TelemetrySnapshotStage(string name, TimeSpan duration, int failureCount, string outcome)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("Stage name is required and bounded.", nameof(name));
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if (failureCount < 0) throw new ArgumentOutOfRangeException(nameof(failureCount));
        if (string.IsNullOrWhiteSpace(outcome) || outcome.Length > 32) throw new ArgumentException("Outcome is required and bounded.", nameof(outcome));
        Name = name;
        Duration = duration;
        FailureCount = failureCount;
        Outcome = outcome;
    }

    public string Name { get; }
    public TimeSpan Duration { get; }
    public int FailureCount { get; }
    public string Outcome { get; }
}

public sealed record TelemetrySnapshot
{
    public const int CurrentSchemaVersion = 1;
    public TelemetrySnapshot(string snapshotId, string executionSessionId, string organizationId, string tenantId, DateTimeOffset startedAtUtc,
        TimeSpan duration, int failureCount, IEnumerable<TelemetrySnapshotStage> stages, int schemaVersion = CurrentSchemaVersion)
    {
        SnapshotId = Required(snapshotId, nameof(snapshotId));
        ExecutionSessionId = Required(executionSessionId, nameof(executionSessionId));
        OrganizationId = Required(organizationId, nameof(organizationId));
        TenantId = Required(tenantId, nameof(tenantId));
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if (failureCount < 0) throw new ArgumentOutOfRangeException(nameof(failureCount));
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        StartedAtUtc = startedAtUtc;
        Duration = duration;
        FailureCount = failureCount;
        Stages = (stages ?? throw new ArgumentNullException(nameof(stages))).OrderBy(stage => stage.Name, StringComparer.Ordinal).ToImmutableArray();
        if (Stages.Length > 100) throw new ArgumentException("A telemetry snapshot cannot contain more than 100 stages.", nameof(stages));
        SchemaVersion = schemaVersion;
        Fingerprint = CalculateFingerprint();
    }

    public string SnapshotId { get; }
    public string ExecutionSessionId { get; }
    public string OrganizationId { get; }
    public string TenantId { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public TimeSpan Duration { get; }
    public int FailureCount { get; }
    public ImmutableArray<TelemetrySnapshotStage> Stages { get; }
    public int SchemaVersion { get; }
    public string Fingerprint { get; }

    private string CalculateFingerprint()
    {
        var canonical = JsonSerializer.Serialize(new
        {
            SchemaVersion,
            ExecutionSessionId,
            OrganizationId,
            TenantId,
            StartedAtUtc,
            DurationTicks = Duration.Ticks,
            FailureCount,
            Stages = Stages.Select(stage => new { stage.Name, DurationTicks = stage.Duration.Ticks, stage.FailureCount, stage.Outcome })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128
        ? value
        : throw new ArgumentException($"{name} is required and bounded.", name);
}

public sealed record TelemetrySnapshotComparison(TimeSpan DurationDelta, int FailureCountDelta, IReadOnlyList<string> MissingStages, bool SchemaCompatible);

public static class TelemetrySnapshotComparer
{
    public static TelemetrySnapshotComparison Compare(TelemetrySnapshot current, TelemetrySnapshot reference)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(reference);
        var names = current.Stages.Select(stage => stage.Name).ToHashSet(StringComparer.Ordinal);
        var missing = reference.Stages.Select(stage => stage.Name).Where(name => !names.Contains(name)).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        return new(current.Duration - reference.Duration, current.FailureCount - reference.FailureCount, missing, current.SchemaVersion == reference.SchemaVersion);
    }
}
