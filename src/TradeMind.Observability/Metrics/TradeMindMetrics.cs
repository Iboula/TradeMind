using System.Diagnostics;
using System.Diagnostics.Metrics;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability.Metrics;

public sealed class TradeMindMetrics : ITradeMindMetrics
{
    private readonly Counter<long> _apiRequests = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.ApiRequests, "requests");
    private readonly Counter<long> _apiErrors = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.ApiErrors, "errors");
    private readonly Counter<long> _authenticationAttempts = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.AuthenticationAttempts, "attempts");
    private readonly Counter<long> _authenticationFailures = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.AuthenticationFailures, "failures");
    private readonly Counter<long> _authorizationDenials = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.AuthorizationDenials, "denials");
    private readonly Counter<long> _rateLimitRejections = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.RateLimitRejections, "rejections");
    private readonly Counter<long> _executionSessionsStarted = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.ExecutionSessionsStarted, "sessions");
    private readonly Counter<long> _executionSessionsCompleted = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.ExecutionSessionsCompleted, "sessions");
    private readonly Counter<long> _executionSessionsFailed = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.ExecutionSessionsFailed, "sessions");
    private readonly Counter<long> _executionSessionsCancelled = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.ExecutionSessionsCancelled, "sessions");
    private readonly Counter<long> _executionSessionsReplay = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.ExecutionSessionsReplayRequests, "requests");
    private readonly Counter<long> _pipelineOperations = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.PipelineOperations, "operations");
    private readonly Counter<long> _pipelineFailures = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.PipelineFailures, "failures");
    private readonly Counter<long> _paperTradingSimulations = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.PaperTradingSimulations, "simulations");
    private readonly Counter<long> _paperTradingFailures = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.PaperTradingFailures, "failures");
    private readonly Counter<long> _outboxMessagesCreated = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.OutboxMessagesCreated, "messages");
    private readonly Counter<long> _outboxMessagesProcessed = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.OutboxMessagesProcessed, "messages");
    private readonly Counter<long> _outboxFailures = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.OutboxFailures, "failures");
    private readonly Counter<long> _idempotencyHits = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.IdempotencyHits, "hits");
    private readonly Counter<long> _idempotencyConflicts = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.IdempotencyConflicts, "conflicts");
    private readonly Histogram<double> _apiDuration = TradeMindMeter.Meter.CreateHistogram<double>(TelemetryMetricNames.ApiRequestDuration, "s");
    private readonly Histogram<double> _pipelineDuration = TradeMindMeter.Meter.CreateHistogram<double>(TelemetryMetricNames.PipelineOperationDuration, "s");
    private readonly Histogram<double> _sessionDuration = TradeMindMeter.Meter.CreateHistogram<double>(TelemetryMetricNames.ExecutionSessionDuration, "s");
    private readonly Histogram<double> _paperTradingDuration = TradeMindMeter.Meter.CreateHistogram<double>(TelemetryMetricNames.PaperTradingDuration, "s");
    private readonly Histogram<double> _replayDuration = TradeMindMeter.Meter.CreateHistogram<double>(TelemetryMetricNames.ReplayManifestDuration, "s");
    private readonly Histogram<double> _databaseDuration = TradeMindMeter.Meter.CreateHistogram<double>(TelemetryMetricNames.DatabaseOperationDuration, "s");
    private readonly Histogram<double> _outboxDuration = TradeMindMeter.Meter.CreateHistogram<double>(TelemetryMetricNames.OutboxProcessingDuration, "s");
    private readonly Counter<long> _brokerOperations = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.BrokerOperations, "operations");
    private readonly Counter<long> _brokerFailures = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.BrokerFailures, "failures");
    private readonly Counter<long> _brokerOrdersSubmitted = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.BrokerOrdersSubmitted, "orders");
    private readonly Counter<long> _brokerOrdersRejected = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.BrokerOrdersRejected, "orders");
    private readonly Counter<long> _brokerOrdersFilled = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.BrokerOrdersFilled, "orders");
    private readonly Counter<long> _brokerPositionsOpened = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.BrokerPositionsOpened, "positions");
    private readonly Counter<long> _brokerPositionsClosed = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.BrokerPositionsClosed, "positions");
    private readonly Counter<long> _brokerReconciliationRuns = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.BrokerReconciliationRuns, "runs");
    private readonly Counter<long> _brokerReconciliationMismatches = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.BrokerReconciliationMismatches, "mismatches");
    private readonly Counter<long> _brokerIdempotencyHits = TradeMindMeter.Meter.CreateCounter<long>(TelemetryMetricNames.BrokerIdempotencyHits, "hits");
    private readonly Histogram<double> _brokerOperationDuration = TradeMindMeter.Meter.CreateHistogram<double>(TelemetryMetricNames.BrokerOperationDuration, "s");
    private long _activeExecutionSessions;

    public TradeMindMetrics()
    {
        TradeMindMeter.Meter.CreateObservableGauge(TelemetryMetricNames.ActiveExecutionSessions, () => Math.Clamp(Interlocked.Read(ref _activeExecutionSessions), 0, 100000), "sessions");
    }

    public void IncrementCounter(string name, long value, MetricDimensions dimensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dimensions);
        var tags = CreateTags(dimensions);
        GetCounter(name).Add(value, tags);
    }

    public void RecordDuration(string name, TimeSpan duration, MetricDimensions dimensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dimensions);
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));
        GetHistogram(name).Record(duration.TotalSeconds, CreateTags(dimensions));
    }

    public void SetActiveExecutionSessions(long value) => Interlocked.Exchange(ref _activeExecutionSessions, Math.Clamp(value, 0, 100000));

    private static TagList CreateTags(MetricDimensions dimensions)
    {
        var dictionary = MetricCardinalityGuard.ToDictionary(dimensions);
        MetricCardinalityGuard.ValidateDimensions(dictionary);
        var tags = new TagList();
        foreach (var pair in dictionary.Where(pair => pair.Value is not null))
            tags.Add(pair.Key, pair.Value);
        return tags;
    }

    private Counter<long> GetCounter(string name) => name switch
    {
        TelemetryMetricNames.ApiRequests => _apiRequests,
        TelemetryMetricNames.ApiErrors => _apiErrors,
        TelemetryMetricNames.AuthenticationAttempts => _authenticationAttempts,
        TelemetryMetricNames.AuthenticationFailures => _authenticationFailures,
        TelemetryMetricNames.AuthorizationDenials => _authorizationDenials,
        TelemetryMetricNames.RateLimitRejections => _rateLimitRejections,
        TelemetryMetricNames.ExecutionSessionsStarted => _executionSessionsStarted,
        TelemetryMetricNames.ExecutionSessionsCompleted => _executionSessionsCompleted,
        TelemetryMetricNames.ExecutionSessionsFailed => _executionSessionsFailed,
        TelemetryMetricNames.ExecutionSessionsCancelled => _executionSessionsCancelled,
        TelemetryMetricNames.ExecutionSessionsReplayRequests => _executionSessionsReplay,
        TelemetryMetricNames.PipelineOperations => _pipelineOperations,
        TelemetryMetricNames.PipelineFailures => _pipelineFailures,
        TelemetryMetricNames.PaperTradingSimulations => _paperTradingSimulations,
        TelemetryMetricNames.PaperTradingFailures => _paperTradingFailures,
        TelemetryMetricNames.OutboxMessagesCreated => _outboxMessagesCreated,
        TelemetryMetricNames.OutboxMessagesProcessed => _outboxMessagesProcessed,
        TelemetryMetricNames.OutboxFailures => _outboxFailures,
        TelemetryMetricNames.IdempotencyHits => _idempotencyHits,
        TelemetryMetricNames.IdempotencyConflicts => _idempotencyConflicts,
        TelemetryMetricNames.BrokerOperations => _brokerOperations,
        TelemetryMetricNames.BrokerFailures => _brokerFailures,
        TelemetryMetricNames.BrokerOrdersSubmitted => _brokerOrdersSubmitted,
        TelemetryMetricNames.BrokerOrdersRejected => _brokerOrdersRejected,
        TelemetryMetricNames.BrokerOrdersFilled => _brokerOrdersFilled,
        TelemetryMetricNames.BrokerPositionsOpened => _brokerPositionsOpened,
        TelemetryMetricNames.BrokerPositionsClosed => _brokerPositionsClosed,
        TelemetryMetricNames.BrokerReconciliationRuns => _brokerReconciliationRuns,
        TelemetryMetricNames.BrokerReconciliationMismatches => _brokerReconciliationMismatches,
        TelemetryMetricNames.BrokerIdempotencyHits => _brokerIdempotencyHits,
        _ => throw new ArgumentException($"Unknown TradeMind counter '{name}'.", nameof(name))
    };

    private Histogram<double> GetHistogram(string name) => name switch
    {
        TelemetryMetricNames.ApiRequestDuration => _apiDuration,
        TelemetryMetricNames.PipelineOperationDuration => _pipelineDuration,
        TelemetryMetricNames.ExecutionSessionDuration => _sessionDuration,
        TelemetryMetricNames.PaperTradingDuration => _paperTradingDuration,
        TelemetryMetricNames.ReplayManifestDuration => _replayDuration,
        TelemetryMetricNames.DatabaseOperationDuration => _databaseDuration,
        TelemetryMetricNames.OutboxProcessingDuration => _outboxDuration,
        TelemetryMetricNames.BrokerOperationDuration => _brokerOperationDuration,
        _ => throw new ArgumentException($"Unknown TradeMind histogram '{name}'.", nameof(name))
    };
}
