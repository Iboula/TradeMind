using TradeMind.AI.Context.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.ExpertAgents.Domain;

public sealed record AgentCapabilities
{
    public AgentCapabilities(
        IReadOnlyCollection<Instrument>? supportedInstruments = null,
        IReadOnlyCollection<Timeframe>? supportedTimeframes = null,
        IReadOnlyCollection<ContextProviderCategory>? requiredContextCategories = null,
        IReadOnlyCollection<ContextProviderCategory>? optionalContextCategories = null,
        IReadOnlyCollection<AgentAnalysisMode>? supportedAnalysisModes = null,
        IReadOnlyCollection<AgentOutputType>? supportedOutputTypes = null,
        bool supportsMultiTimeframe = false,
        bool supportsMultiInstrument = false,
        bool supportsUserQuestion = true,
        bool requiresKnowledge = false,
        bool requiresMemory = false,
        bool requiresTraderProfile = false,
        bool requiresWorkspace = false,
        TimeSpan? recommendedTimeout = null,
        int? maximumContextCharacters = null,
        TimeSpan? maximumContextAge = null)
    {
        if (recommendedTimeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(recommendedTimeout));
        }

        if (maximumContextCharacters is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumContextCharacters));
        }

        if (maximumContextAge is { } age && age <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumContextAge));
        }

        SupportedInstruments = Array.AsReadOnly(
            (supportedInstruments ?? [])
                .Distinct()
                .OrderBy(instrument => instrument.Symbol, StringComparer.Ordinal)
                .ToArray());
        SupportedTimeframes = Array.AsReadOnly(
            (supportedTimeframes ?? [])
                .Distinct()
                .OrderBy(timeframe => timeframe.Code, StringComparer.Ordinal)
                .ToArray());
        RequiredContextCategories = CopyCategories(requiredContextCategories);
        OptionalContextCategories = CopyCategories(optionalContextCategories);
        SupportedAnalysisModes = Array.AsReadOnly(
            (supportedAnalysisModes ?? [])
                .Distinct()
                .OrderBy(mode => mode)
                .ToArray());
        SupportedOutputTypes = Array.AsReadOnly(
            (supportedOutputTypes ?? [AgentOutputType.StructuredAnalysis])
                .Distinct()
                .OrderBy(output => output)
                .ToArray());
        SupportsMultiTimeframe = supportsMultiTimeframe;
        SupportsMultiInstrument = supportsMultiInstrument;
        SupportsUserQuestion = supportsUserQuestion;
        RequiresKnowledge = requiresKnowledge || RequiredContextCategories.Contains(ContextProviderCategory.Knowledge);
        RequiresMemory = requiresMemory || RequiredContextCategories.Contains(ContextProviderCategory.Memory);
        RequiresTraderProfile = requiresTraderProfile || RequiredContextCategories.Contains(ContextProviderCategory.TraderProfile);
        RequiresWorkspace = requiresWorkspace || RequiredContextCategories.Contains(ContextProviderCategory.Workspace);
        RecommendedTimeout = recommendedTimeout;
        MaximumContextCharacters = maximumContextCharacters;
        MaximumContextAge = maximumContextAge;
    }

    public IReadOnlyList<Instrument> SupportedInstruments { get; }
    public IReadOnlyList<Timeframe> SupportedTimeframes { get; }
    public IReadOnlyList<ContextProviderCategory> RequiredContextCategories { get; }
    public IReadOnlyList<ContextProviderCategory> OptionalContextCategories { get; }
    public IReadOnlyList<AgentAnalysisMode> SupportedAnalysisModes { get; }
    public IReadOnlyList<AgentOutputType> SupportedOutputTypes { get; }
    public bool SupportsMultiTimeframe { get; }
    public bool SupportsMultiInstrument { get; }
    public bool SupportsUserQuestion { get; }
    public bool RequiresKnowledge { get; }
    public bool RequiresMemory { get; }
    public bool RequiresTraderProfile { get; }
    public bool RequiresWorkspace { get; }
    public TimeSpan? RecommendedTimeout { get; }
    public int? MaximumContextCharacters { get; }
    public TimeSpan? MaximumContextAge { get; }

    public bool Supports(Instrument instrument) =>
        SupportedInstruments.Count == 0 || SupportedInstruments.Contains(instrument);

    public bool Supports(Timeframe timeframe) =>
        SupportedTimeframes.Count == 0 || SupportedTimeframes.Contains(timeframe);

    public bool Supports(AgentAnalysisMode mode) =>
        SupportedAnalysisModes.Count == 0 || SupportedAnalysisModes.Contains(mode);

    private static IReadOnlyList<ContextProviderCategory> CopyCategories(
        IReadOnlyCollection<ContextProviderCategory>? categories) =>
        Array.AsReadOnly((categories ?? [])
            .Distinct()
            .OrderBy(category => category)
            .ToArray());
}
