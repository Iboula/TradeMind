namespace TradeMind.ExecutionSessions.Domain;

public sealed record ExecutionSessionArtifactId
{
    public ExecutionSessionArtifactId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > 256) throw new ArgumentException("Artifact id cannot exceed 256 characters.", nameof(value));
        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record ExecutionSessionArtifactReference
{
    public ExecutionSessionArtifactReference(
        ExecutionSessionArtifactType artifactType,
        ExecutionSessionArtifactId artifactId,
        ExecutionSessionStage stage,
        int schemaVersion,
        DateTimeOffset createdAtUtc,
        string contentHash,
        string? storageReference = null,
        bool isReplayable = true,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (createdAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Artifact timestamp must be UTC.", nameof(createdAtUtc));
        if (contentHash.Trim().Length > 128) throw new ArgumentException("Content hash is too long.", nameof(contentHash));
        ArtifactType = artifactType;
        ArtifactId = artifactId;
        Stage = stage;
        SchemaVersion = schemaVersion;
        CreatedAtUtc = createdAtUtc;
        ContentHash = contentHash.Trim().ToLowerInvariant();
        StorageReference = string.IsNullOrWhiteSpace(storageReference) ? null : storageReference.Trim();
        IsReplayable = isReplayable;
        Metadata = ExecutionSessionMetadata.Copy(metadata);
    }

    public ExecutionSessionArtifactType ArtifactType { get; }
    public ExecutionSessionArtifactId ArtifactId { get; }
    public ExecutionSessionStage Stage { get; }
    public int SchemaVersion { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public string ContentHash { get; }
    public string? StorageReference { get; }
    public bool IsReplayable { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
