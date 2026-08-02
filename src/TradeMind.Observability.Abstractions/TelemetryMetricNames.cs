namespace TradeMind.Observability.Abstractions;

public static class TelemetryMetricNames
{
    public const string ApiRequests = "trademind.api.requests";
    public const string ApiErrors = "trademind.api.errors";
    public const string AuthenticationAttempts = "trademind.authentication.attempts";
    public const string AuthenticationFailures = "trademind.authentication.failures";
    public const string AuthorizationDenials = "trademind.authorization.denials";
    public const string RateLimitRejections = "trademind.rate_limit.rejections";
    public const string ExecutionSessionsStarted = "trademind.execution_sessions.started";
    public const string ExecutionSessionsCompleted = "trademind.execution_sessions.completed";
    public const string ExecutionSessionsFailed = "trademind.execution_sessions.failed";
    public const string ExecutionSessionsCancelled = "trademind.execution_sessions.cancelled";
    public const string ExecutionSessionsReplayRequests = "trademind.execution_sessions.replay_requests";
    public const string PipelineOperations = "trademind.pipeline.operations";
    public const string PipelineFailures = "trademind.pipeline.failures";
    public const string PaperTradingSimulations = "trademind.paper_trading.simulations";
    public const string PaperTradingFailures = "trademind.paper_trading.failures";
    public const string OutboxMessagesCreated = "trademind.outbox.messages_created";
    public const string OutboxMessagesProcessed = "trademind.outbox.messages_processed";
    public const string OutboxFailures = "trademind.outbox.failures";
    public const string IdempotencyHits = "trademind.idempotency.hits";
    public const string IdempotencyConflicts = "trademind.idempotency.conflicts";
    public const string ApiRequestDuration = "trademind.api.request.duration";
    public const string PipelineOperationDuration = "trademind.pipeline.operation.duration";
    public const string ExecutionSessionDuration = "trademind.execution_session.duration";
    public const string PaperTradingDuration = "trademind.paper_trading.duration";
    public const string ReplayManifestDuration = "trademind.replay_manifest.duration";
    public const string DatabaseOperationDuration = "trademind.database.operation.duration";
    public const string OutboxProcessingDuration = "trademind.outbox.processing.duration";
    public const string ActiveExecutionSessions = "trademind.execution_sessions.active";
    public const string BrokerOperations = "trademind.brokers.operations";
    public const string BrokerFailures = "trademind.brokers.failures";
    public const string BrokerOperationDuration = "trademind.brokers.operation.duration";
    public const string BrokerOrdersSubmitted = "trademind.brokers.orders_submitted";
    public const string BrokerOrdersRejected = "trademind.brokers.orders_rejected";
    public const string BrokerOrdersFilled = "trademind.brokers.orders_filled";
    public const string BrokerPositionsOpened = "trademind.brokers.positions_opened";
    public const string BrokerPositionsClosed = "trademind.brokers.positions_closed";
    public const string BrokerReconciliationRuns = "trademind.brokers.reconciliation_runs";
    public const string BrokerReconciliationMismatches = "trademind.brokers.reconciliation_mismatches";
    public const string BrokerIdempotencyHits = "trademind.brokers.idempotency_hits";
}
