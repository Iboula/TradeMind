using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Execution;

public static class BrokerErrorMapper
{
    public static BrokerError FromException(Exception exception, BrokerExecutionContext context, string? executionSessionId, DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var category = exception switch
        {
            OperationCanceledException => BrokerErrorCategory.Cancelled,
            TimeoutException => BrokerErrorCategory.Timeout,
            UnauthorizedAccessException => BrokerErrorCategory.Authorization,
            _ => BrokerErrorCategory.TransportFailure
        };
        return new BrokerError(category.ToString().ToUpperInvariant(), category, SafeMessage(category), category is BrokerErrorCategory.Timeout or BrokerErrorCategory.TransportFailure, category == BrokerErrorCategory.ReconciliationRequired, null, new BrokerTraceReference(context.CorrelationId, null, executionSessionId), occurredAtUtc);
    }

    private static string SafeMessage(BrokerErrorCategory category) => category switch
    {
        BrokerErrorCategory.Cancelled => "The broker operation was cancelled.",
        BrokerErrorCategory.Timeout => "The broker operation timed out.",
        BrokerErrorCategory.Authorization => "The broker operation was not authorized.",
        BrokerErrorCategory.TransportFailure => "The broker connector failed to complete the operation.",
        _ => "The broker operation failed."
    };
}
