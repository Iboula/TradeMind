namespace TradeMind.ExecutionSessions.Domain;

public enum ExecutionSessionStatus
{
    Created,
    Running,
    Completed,
    Failed,
    Cancelled,
    Expired
}
