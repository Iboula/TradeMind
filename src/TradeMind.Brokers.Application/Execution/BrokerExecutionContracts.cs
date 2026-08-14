using TradeMind.AI.RiskEngine.Domain;
using TradeMind.AI.TradingPlans.Domain;
using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Execution;

public sealed record BrokerOrderExecutionCommand(
    BrokerOrderRequest Order,
    TradingPlanResult? TradingPlan,
    RiskAssessmentResult? RiskAssessment,
    BrokerExecutionMode RequestedMode = BrokerExecutionMode.Simulation);

public enum BrokerExecutionResultStatus
{
    Accepted,
    Replayed,
    Rejected,
    Failed,
    Conflict,
    InProgress,
    ExecutionUnknown
}

public sealed record BrokerExecutionResult(
    BrokerExecutionId ExecutionId,
    BrokerExecutionResultStatus Status,
    BrokerOrder? Order,
    BrokerExecution? Execution,
    BrokerError? Error,
    string Operation,
    DateTimeOffset CompletedAtUtc,
    bool IsLive = false)
{
    public bool Succeeded => Status is BrokerExecutionResultStatus.Accepted or BrokerExecutionResultStatus.Replayed;
}

public sealed record BrokerEligibilityResult(bool IsEligible, BrokerError? Error)
{
    public static BrokerEligibilityResult Success() => new(true, null);
    public static BrokerEligibilityResult Reject(BrokerError error) => new(false, error);
}
