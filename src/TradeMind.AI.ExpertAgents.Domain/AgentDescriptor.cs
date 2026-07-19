using TradeMind.AI.Context.Domain;

namespace TradeMind.AI.ExpertAgents.Domain;

public sealed record AgentDescriptor
{
    public AgentDescriptor(
        AgentId id,
        string displayName,
        AgentVersion version,
        AgentSpecialty specialty,
        string description,
        AgentCapabilities capabilities,
        AgentActivationStatus activationStatus = AgentActivationStatus.Enabled,
        AgentMaturity maturity = AgentMaturity.Stable,
        AgentExecutionMode executionMode = AgentExecutionMode.Synchronous,
        IReadOnlyCollection<string>? tags = null,
        IReadOnlyCollection<int>? supportedContextVersions = null,
        int resultSchemaVersion = 1,
        IReadOnlyCollection<string>? requiredPermissions = null,
        IReadOnlyCollection<string>? allowedTenantIds = null,
        IReadOnlyCollection<string>? allowedUserIds = null,
        double minimumContextQuality = 0,
        bool allowStaleContext = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(specialty);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (resultSchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resultSchemaVersion));
        }

        if (double.IsNaN(minimumContextQuality)
            || double.IsInfinity(minimumContextQuality)
            || minimumContextQuality is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumContextQuality));
        }

        Id = id;
        DisplayName = displayName.Trim();
        Version = version;
        Specialty = specialty;
        Description = description.Trim();
        Capabilities = capabilities;
        ActivationStatus = activationStatus;
        Maturity = maturity;
        ExecutionMode = executionMode;
        Tags = AgentCollections.CopyStrings(tags, StringComparer.OrdinalIgnoreCase);
        SupportedContextVersions = Array.AsReadOnly(
            (supportedContextVersions ?? [MarketContext.CurrentVersion])
                .Distinct()
                .OrderBy(value => value)
                .ToArray());
        if (SupportedContextVersions.Any(value => value <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(supportedContextVersions));
        }

        ResultSchemaVersion = resultSchemaVersion;
        RequiredPermissions = AgentCollections.CopyStrings(requiredPermissions, StringComparer.OrdinalIgnoreCase);
        AllowedTenantIds = AgentCollections.CopyStrings(allowedTenantIds, StringComparer.Ordinal);
        AllowedUserIds = AgentCollections.CopyStrings(allowedUserIds, StringComparer.Ordinal);
        MinimumContextQuality = minimumContextQuality;
        AllowStaleContext = allowStaleContext;
    }

    public AgentId Id { get; }
    public string DisplayName { get; }
    public AgentVersion Version { get; }
    public AgentSpecialty Specialty { get; }
    public string Description { get; }
    public AgentCapabilities Capabilities { get; }
    public AgentActivationStatus ActivationStatus { get; }
    public AgentMaturity Maturity { get; }
    public AgentExecutionMode ExecutionMode { get; }
    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyList<int> SupportedContextVersions { get; }
    public int ResultSchemaVersion { get; }
    public IReadOnlyList<string> RequiredPermissions { get; }
    public IReadOnlyList<string> AllowedTenantIds { get; }
    public IReadOnlyList<string> AllowedUserIds { get; }
    public double MinimumContextQuality { get; }
    public bool AllowStaleContext { get; }
}
