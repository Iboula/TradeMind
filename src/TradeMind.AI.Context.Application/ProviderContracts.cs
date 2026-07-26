using TradeMind.AI.Context.Domain;

namespace TradeMind.AI.Context.Application;

public sealed record ContextProviderDescriptor
{
    public ContextProviderDescriptor(
        ContextProviderId id,
        ContextProviderCategory category,
        ContextRequirement requirement,
        int priority,
        TimeSpan timeout,
        IReadOnlyCollection<ContextProviderId>? dependencies = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Provider timeout must be positive.");
        }

        Id = id;
        Category = category;
        Requirement = requirement;
        Priority = priority;
        Timeout = timeout;
        Dependencies = Array.AsReadOnly(
            dependencies?.Distinct().OrderBy(value => value.Value, StringComparer.Ordinal).ToArray() ?? []);
    }

    public ContextProviderId Id { get; }
    public ContextProviderCategory Category { get; }
    public ContextRequirement Requirement { get; }
    public int Priority { get; }
    public TimeSpan Timeout { get; }
    public IReadOnlyList<ContextProviderId> Dependencies { get; }
}

public interface IContextProvider
{
    ContextProviderDescriptor Descriptor { get; }

    Task<ContextProviderResult> ProvideAsync(
        ContextProviderRequest request,
        CancellationToken cancellationToken);
}

public sealed record ContextProviderRequest
{
    public ContextProviderRequest(
        BuildMarketContextQuery query,
        IReadOnlyDictionary<ContextProviderId, ContextProviderResult> dependencies)
    {
        Query = query ?? throw new ArgumentNullException(nameof(query));
        ArgumentNullException.ThrowIfNull(dependencies);
        Dependencies = new System.Collections.ObjectModel.ReadOnlyDictionary<ContextProviderId, ContextProviderResult>(
            new Dictionary<ContextProviderId, ContextProviderResult>(dependencies));
    }

    public BuildMarketContextQuery Query { get; }
    public IReadOnlyDictionary<ContextProviderId, ContextProviderResult> Dependencies { get; }
}

public sealed record ContextProviderResult
{
    private ContextProviderResult(
        ContextProviderExecutionStatus status,
        ContextData? data,
        DateTimeOffset? sourceTimestampUtc,
        int itemCount,
        string providerVersion,
        IReadOnlyCollection<ContextSourceReference>? references,
        ContextBuildError? error)
    {
        if (itemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemCount));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(providerVersion);
        Status = status;
        Data = data;
        SourceTimestampUtc = sourceTimestampUtc;
        ItemCount = itemCount;
        ProviderVersion = providerVersion.Trim();
        References = Array.AsReadOnly(references?.ToArray() ?? []);
        Error = error;
    }

    public ContextProviderExecutionStatus Status { get; }
    public ContextData? Data { get; }
    public DateTimeOffset? SourceTimestampUtc { get; }
    public int ItemCount { get; }
    public string ProviderVersion { get; }
    public IReadOnlyList<ContextSourceReference> References { get; }
    public ContextBuildError? Error { get; }

    public static ContextProviderResult Succeeded(
        ContextData data,
        DateTimeOffset? sourceTimestampUtc,
        int itemCount,
        string providerVersion,
        IReadOnlyCollection<ContextSourceReference>? references = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new ContextProviderResult(
            ContextProviderExecutionStatus.Succeeded,
            data,
            sourceTimestampUtc,
            itemCount,
            providerVersion,
            references,
            null);
    }

    public static ContextProviderResult Unavailable(
        ContextProviderId providerId,
        string message,
        string providerVersion = "1.0") => new(
            ContextProviderExecutionStatus.Unavailable,
            null,
            null,
            0,
            providerVersion,
            null,
            new ContextBuildError(ContextBuildErrorCode.ProviderUnavailable, message, providerId));

    public static ContextProviderResult NotConfigured(
        ContextProviderId providerId,
        string message,
        string providerVersion = "1.0") => new(
            ContextProviderExecutionStatus.NotConfigured,
            null,
            null,
            0,
            providerVersion,
            null,
            new ContextBuildError(ContextBuildErrorCode.ProviderUnavailable, message, providerId));

    public static ContextProviderResult Failed(
        ContextProviderId providerId,
        ContextBuildErrorCode code,
        string message,
        string providerVersion = "1.0") => new(
            ContextProviderExecutionStatus.Failed,
            null,
            null,
            0,
            providerVersion,
            null,
            new ContextBuildError(code, message, providerId));

    internal static ContextProviderResult TimedOut(
        ContextProviderId providerId,
        bool global,
        string providerVersion = "1.0") => new(
            ContextProviderExecutionStatus.TimedOut,
            null,
            null,
            0,
            providerVersion,
            null,
            new ContextBuildError(
                global ? ContextBuildErrorCode.GlobalTimeout : ContextBuildErrorCode.ProviderTimeout,
                global ? "The global context build timeout elapsed." : "The provider timeout elapsed.",
                providerId));

    internal static ContextProviderResult Skipped(
        ContextProviderId providerId,
        string message,
        string providerVersion = "1.0") => new(
            ContextProviderExecutionStatus.Skipped,
            null,
            null,
            0,
            providerVersion,
            null,
            new ContextBuildError(ContextBuildErrorCode.ProviderUnavailable, message, providerId));
}

public interface ITraderProfileContextSource
{
    Task<TraderProfileContext?> GetAsync(
        string userId,
        CancellationToken cancellationToken);
}

public interface IWorkspaceContextSource
{
    Task<WorkspaceContext?> GetAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken);
}

public interface INewsContextSource
{
    Task<NewsContext?> GetAsync(
        string instrument,
        CancellationToken cancellationToken);
}

public interface IEconomicCalendarContextSource
{
    Task<EconomicCalendarContext?> GetAsync(
        string instrument,
        DateTimeOffset referenceTimeUtc,
        CancellationToken cancellationToken);
}
