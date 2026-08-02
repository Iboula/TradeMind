using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Abstractions;

public sealed record BrokerAuthorizationDecision(bool IsAllowed, string Code, string? SafeReason)
{
    public static BrokerAuthorizationDecision Allow() => new(true, "AUTHORIZED", null);
    public static BrokerAuthorizationDecision Deny(string code, string reason) => new(false, code, reason);
}

public interface IBrokerAuthorizationPolicy
{
    BrokerAuthorizationDecision Evaluate(BrokerExecutionContext context, BrokerExecutionMode mode, string operation);
}
