namespace TradeMind.Observability.Abstractions;

public static class TelemetryActivityNames
{
    public const string Source = "TradeMind";
    public const string Api = "TradeMind.Api";
    public const string Identity = "TradeMind.Identity";
    public const string ExecutionSessions = "TradeMind.ExecutionSessions";
    public const string Persistence = "TradeMind.Persistence";
    public const string MarketConnectors = "TradeMind.MarketConnectors";
    public const string MarketContext = "TradeMind.MarketContext";
    public const string Experts = "TradeMind.Experts";
    public const string Consensus = "TradeMind.Consensus";
    public const string TradingDecisions = "TradeMind.TradingDecisions";
    public const string Risk = "TradeMind.Risk";
    public const string TradingPlans = "TradeMind.TradingPlans";
    public const string TradingWorkspace = "TradeMind.TradingWorkspace";
    public const string TradingAssistant = "TradeMind.TradingAssistant";
    public const string PaperTrading = "TradeMind.PaperTrading";
    public const string Outbox = "TradeMind.Outbox";
    public const string Replay = "TradeMind.Replay";

    public static IReadOnlyList<string> All { get; } =
    [Api, Identity, ExecutionSessions, Persistence, MarketConnectors, MarketContext, Experts, Consensus,
        TradingDecisions, Risk, TradingPlans, TradingWorkspace, TradingAssistant, PaperTrading, Outbox, Replay];
}
