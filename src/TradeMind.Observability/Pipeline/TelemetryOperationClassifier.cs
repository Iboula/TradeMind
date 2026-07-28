using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability;

public static class TelemetryOperationClassifier
{
    public static TelemetryOperation Classify(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        var name = requestType.Name;
        var namespaceName = requestType.Namespace ?? string.Empty;
        var (module, stage) = namespaceName switch
        {
            var value when value.Contains("Context", StringComparison.Ordinal) => ("MarketContext", TelemetryStage.MarketContext),
            var value when value.Contains("ExpertAgents.Dispatch", StringComparison.Ordinal) => ("ExpertDispatch", TelemetryStage.ExpertDispatch),
            var value when value.Contains("ExpertAgents.Consensus", StringComparison.Ordinal) => ("Consensus", TelemetryStage.Consensus),
            var value when value.Contains("ExpertAgents", StringComparison.Ordinal) => ("ExpertAnalysis", TelemetryStage.ExpertAnalysis),
            var value when value.Contains("TradingDecisions", StringComparison.Ordinal) => ("TradingDecision", TelemetryStage.TradingDecision),
            var value when value.Contains("RiskEngine", StringComparison.Ordinal) => ("Risk", TelemetryStage.Risk),
            var value when value.Contains("TradingPlans", StringComparison.Ordinal) => ("TradingPlan", TelemetryStage.TradingPlan),
            var value when value.Contains("TradingWorkspace", StringComparison.Ordinal) => ("TradingWorkspace", TelemetryStage.TradingWorkspace),
            var value when value.Contains("TradingAssistant", StringComparison.Ordinal) => ("TradingAssistant", TelemetryStage.TradingAssistant),
            var value when value.Contains("PaperTrading", StringComparison.Ordinal) => ("PaperTrading", TelemetryStage.PaperTrading),
            var value when value.Contains("ExecutionSessions", StringComparison.Ordinal) => ("ExecutionSessions", TelemetryStage.ExecutionSession),
            _ => ("Application", TelemetryStage.Api)
        };
        return new TelemetryOperation($"TradeMind.{module}.{name}", module, stage);
    }
}
