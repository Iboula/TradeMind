using MediatR;
using TradeMind.AI.Context.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.Context.Application;

public sealed record BuildMarketContextQuery : IRequest<MarketContextBuildResult>
{
    public BuildMarketContextQuery(
        string userId,
        string sessionId,
        ConnectorId? connectorId,
        Instrument instrument,
        Timeframe timeframe,
        string? conversationId = null,
        string? correlationId = null,
        string? tenantId = null,
        string? workspaceId = null,
        ExternalAccountReference? account = null,
        string? knowledgeQuery = null,
        TimeSpan? maximumMarketAge = null,
        int contextVersion = MarketContext.CurrentVersion,
        IReadOnlyCollection<ContextProviderCategory>? requestedCategories = null)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(timeframe);
        if (maximumMarketAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMarketAge));
        }

        UserId = userId;
        SessionId = sessionId;
        ConnectorId = connectorId;
        Instrument = instrument;
        Timeframe = timeframe;
        ConversationId = NormalizeOptional(conversationId);
        CorrelationId = NormalizeOptional(correlationId) ?? Guid.NewGuid().ToString("N");
        TenantId = NormalizeOptional(tenantId);
        WorkspaceId = NormalizeOptional(workspaceId);
        Account = account;
        KnowledgeQuery = NormalizeOptional(knowledgeQuery) ?? instrument.Symbol;
        MaximumMarketAge = maximumMarketAge;
        ContextVersion = contextVersion;
        RequestedCategories = Array.AsReadOnly(requestedCategories?.Distinct().ToArray() ?? []);
    }

    public string UserId { get; }
    public string SessionId { get; }
    public ConnectorId? ConnectorId { get; }
    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public string? ConversationId { get; }
    public string CorrelationId { get; }
    public string? TenantId { get; }
    public string? WorkspaceId { get; }
    public ExternalAccountReference? Account { get; }
    public string KnowledgeQuery { get; }

    public string KnowledgeSearchQuery => string.Join(
        ' ',
        new[] { Instrument.Symbol, Timeframe.Code, KnowledgeQuery }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase));
    public TimeSpan? MaximumMarketAge { get; }
    public int ContextVersion { get; }
    public IReadOnlyList<ContextProviderCategory> RequestedCategories { get; }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public abstract record MarketContextBuildResult
{
    protected MarketContextBuildResult(
        MarketContextBuildStatus status,
        MarketContext? context,
        IReadOnlyCollection<ContextSourceTrace> traces,
        IReadOnlyCollection<ContextBuildWarning> warnings,
        IReadOnlyCollection<ContextBuildError> errors,
        ContextQuality quality,
        string correlationId,
        TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        Status = status;
        Context = context;
        Traces = Array.AsReadOnly(traces.ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
        Errors = Array.AsReadOnly(errors.ToArray());
        Quality = quality;
        CorrelationId = correlationId;
        Duration = duration;
        ExecutedProviders = Array.AsReadOnly(
            Traces
                .Where(trace => trace.Status != ContextProviderExecutionStatus.Skipped)
                .Select(trace => trace.ProviderId)
                .ToArray());
        SkippedProviders = Array.AsReadOnly(
            Traces
                .Where(trace => trace.Status == ContextProviderExecutionStatus.Skipped)
                .Select(trace => trace.ProviderId)
                .ToArray());
    }

    public MarketContextBuildStatus Status { get; }
    public MarketContext? Context { get; }
    public IReadOnlyList<ContextSourceTrace> Traces { get; }
    public IReadOnlyList<ContextBuildWarning> Warnings { get; }
    public IReadOnlyList<ContextBuildError> Errors { get; }
    public ContextQuality Quality { get; }
    public string CorrelationId { get; }
    public TimeSpan Duration { get; }
    public IReadOnlyList<ContextProviderId> ExecutedProviders { get; }
    public IReadOnlyList<ContextProviderId> SkippedProviders { get; }
}

public sealed record SucceededMarketContextBuildResult : MarketContextBuildResult
{
    public SucceededMarketContextBuildResult(
        MarketContext context,
        string correlationId,
        TimeSpan duration)
        : base(
            MarketContextBuildStatus.Succeeded,
            context,
            context.Traces,
            context.Warnings,
            [],
            context.Quality,
            correlationId,
            duration)
    {
    }
}

public sealed record PartiallySucceededMarketContextBuildResult : MarketContextBuildResult
{
    public PartiallySucceededMarketContextBuildResult(
        MarketContext context,
        IReadOnlyCollection<ContextBuildError> errors,
        string correlationId,
        TimeSpan duration)
        : base(
            MarketContextBuildStatus.PartiallySucceeded,
            context,
            context.Traces,
            context.Warnings,
            errors,
            context.Quality,
            correlationId,
            duration)
    {
    }
}

public sealed record FailedMarketContextBuildResult : MarketContextBuildResult
{
    public FailedMarketContextBuildResult(
        IReadOnlyCollection<ContextSourceTrace> traces,
        IReadOnlyCollection<ContextBuildWarning> warnings,
        IReadOnlyCollection<ContextBuildError> errors,
        ContextQuality quality,
        string correlationId,
        TimeSpan duration)
        : base(
            MarketContextBuildStatus.Failed,
            null,
            traces,
            warnings,
            errors,
            quality,
            correlationId,
            duration)
    {
    }
}

public sealed class BuildMarketContextQueryHandler(IMarketContextBuilder builder)
    : IRequestHandler<BuildMarketContextQuery, MarketContextBuildResult>
{
    public Task<MarketContextBuildResult> Handle(
        BuildMarketContextQuery request,
        CancellationToken cancellationToken)
    {
        return builder.BuildAsync(request, cancellationToken);
    }
}

public interface IMarketContextBuilder
{
    Task<MarketContextBuildResult> BuildAsync(
        BuildMarketContextQuery request,
        CancellationToken cancellationToken);
}

public sealed class ApplicationAssemblyMarker;
