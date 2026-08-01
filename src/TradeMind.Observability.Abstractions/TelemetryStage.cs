namespace TradeMind.Observability.Abstractions;

public enum TelemetryStage
{
    Api,
    Authentication,
    Authorization,
    MarketContext,
    ExpertDispatch,
    ExpertAnalysis,
    Consensus,
    TradingDecision,
    Risk,
    TradingPlan,
    TradingWorkspace,
    TradingAssistant,
    PaperTrading,
    ExecutionSession,
    Persistence,
    Outbox,
    Replay,
    Database,
    Health,
    Broker
}
