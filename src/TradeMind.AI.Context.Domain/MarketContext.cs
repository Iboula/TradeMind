using TradeMind.Market.Abstractions;

namespace TradeMind.AI.Context.Domain;

public sealed record MarketContext
{
    public const int CurrentVersion = 1;

    public MarketContext(
        MarketContextId id,
        int version,
        string userId,
        string sessionId,
        Instrument instrument,
        Timeframe timeframe,
        DateTimeOffset builtAtUtc,
        MarketContextBuildStatus status,
        MarketSnapshot marketSnapshot,
        TraderProfileContext? traderProfile,
        WorkspaceContext? workspace,
        MemoryContext? memory,
        KnowledgeContext? knowledge,
        EconomicCalendarContext? economicCalendar,
        NewsContext? news,
        IReadOnlyCollection<ContextSourceTrace> traces,
        IReadOnlyCollection<ContextBuildWarning> warnings,
        ContextQuality quality)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(timeframe);
        ArgumentNullException.ThrowIfNull(marketSnapshot);
        ArgumentNullException.ThrowIfNull(traces);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(quality);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        if (builtAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Market context build date must be UTC.", nameof(builtAtUtc));
        }

        if (status == MarketContextBuildStatus.Failed)
        {
            throw new ArgumentException("A materialized market context cannot have Failed status.", nameof(status));
        }

        Id = id;
        Version = version;
        UserId = userId.Trim();
        SessionId = sessionId.Trim();
        Instrument = instrument;
        Timeframe = timeframe;
        BuiltAtUtc = builtAtUtc;
        Status = status;
        MarketSnapshot = marketSnapshot;
        TraderProfile = traderProfile;
        Workspace = workspace;
        Memory = memory;
        Knowledge = knowledge;
        EconomicCalendar = economicCalendar;
        News = news;
        Traces = ContextCollections.CopyList(traces);
        Warnings = ContextCollections.CopyList(warnings);
        Quality = quality;
    }

    public MarketContextId Id { get; }
    public int Version { get; }
    public string UserId { get; }
    public string SessionId { get; }
    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public DateTimeOffset BuiltAtUtc { get; }
    public MarketContextBuildStatus Status { get; }
    public MarketSnapshot MarketSnapshot { get; }
    public TraderProfileContext? TraderProfile { get; }
    public WorkspaceContext? Workspace { get; }
    public MemoryContext? Memory { get; }
    public KnowledgeContext? Knowledge { get; }
    public EconomicCalendarContext? EconomicCalendar { get; }
    public NewsContext? News { get; }
    public IReadOnlyList<ContextSourceTrace> Traces { get; }
    public IReadOnlyList<ContextBuildWarning> Warnings { get; }
    public ContextQuality Quality { get; }
}
