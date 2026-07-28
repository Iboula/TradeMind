namespace TradeMind.Observability.Abstractions;

public sealed record TelemetryOperation
{
    public TelemetryOperation(string name, string module, TelemetryStage stage, string? artifactType = null, int? artifactCount = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
            throw new ArgumentException("A telemetry operation name is required and must be at most 128 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(module) || module.Length > 64)
            throw new ArgumentException("A telemetry module is required and must be at most 64 characters.", nameof(module));
        if (artifactType is { Length: > 64 })
            throw new ArgumentException("ArtifactType must be at most 64 characters.", nameof(artifactType));
        if (artifactCount is < 0 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(artifactCount));
        Name = name;
        Module = module;
        Stage = stage;
        ArtifactType = artifactType;
        ArtifactCount = artifactCount;
    }

    public string Name { get; }
    public string Module { get; }
    public TelemetryStage Stage { get; }
    public string? ArtifactType { get; }
    public int? ArtifactCount { get; }
}
