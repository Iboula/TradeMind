namespace TradeMind.ExecutionSessions.Domain;

public enum ExecutionSessionStage
{
    MarketContext = 10,
    ExpertDispatch = 20,
    ExpertAnalysis = 30,
    Consensus = 40,
    TradingDecision = 50,
    RiskEvaluation = 60,
    TradingPlan = 70,
    TradingWorkspace = 80,
    TradingAssistant = 90,
    PaperTrading = 100,
    Completed = 110
}
