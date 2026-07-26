using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.ExecutionSessions.Application.Abstractions;

public interface IExecutionSessionIdFactory
{
    ExecutionSessionId Create(string correlationId, string instrument, string timeframe, DateTimeOffset startedAtUtc);
}
