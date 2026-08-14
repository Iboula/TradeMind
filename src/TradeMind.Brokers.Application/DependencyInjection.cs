using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Application.LiveSafety;
using TradeMind.Brokers.Application.Options;
using TradeMind.Brokers.Domain;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.Application;

public static class BrokersApplicationDependencyInjection
{
    public static IServiceCollection AddTradeMindBrokersApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<BrokerOptions>().Bind(configuration.GetSection("TradeMind:Brokers")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<BrokerOptions>, BrokerOptionsValidator>();
        services.AddOptions<LiveSafetyOptions>().Bind(configuration.GetSection(LiveSafetyOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<LiveSafetyOptions>, LiveSafetyOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.TryAddSingleton<ITradeMindMetrics, NoOpTradeMindMetrics>();
        services.TryAddSingleton<ITradeMindTelemetry, NoOpTradeMindTelemetry>();
        services.TryAddSingleton<ICriticalAuditWriter, NoOpCriticalAuditWriter>();
        services.TryAddSingleton<ILiveTradingSafetyGate, LiveTradingSafetyGate>();
        services.TryAddSingleton<IRiskGuardPolicy, NoConfiguredRiskGuardPolicy>();
        services.TryAddSingleton<IKillSwitchStore, InMemoryKillSwitchStore>();
        services.TryAddSingleton<IExecutionQuarantineStore, InMemoryExecutionQuarantineStore>();
        services.TryAddSingleton<IBrokerExecutionRecoveryReader, EmptyBrokerExecutionRecoveryReader>();
        services.TryAddSingleton<IBrokerExecutionRecoveryService, BrokerExecutionRecoveryService>();
        services.TryAddSingleton<IBrokerPositionOwnershipStore, InMemoryBrokerPositionOwnershipStore>();
        services.TryAddSingleton<IBrokerExecutionLock, UnavailableBrokerExecutionLock>();
        services.TryAddSingleton<ILiveSafetyReadiness, EmptyLiveSafetyReadiness>();
        services.TryAddSingleton<IOperatorActionService, InMemoryOperatorActionService>();
        services.TryAddSingleton<IBrokerReconciliationService, NoOpBrokerReconciliationService>();
        services.TryAddSingleton<IBrokerClock>(provider => new TimeProviderBrokerClock(provider.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<IBrokerConnectorRegistry, BrokerConnectorRegistry>();
        services.TryAddSingleton<IBrokerAuthorizationPolicy, BrokerAuthorizationPolicy>();
        services.TryAddSingleton<IBrokerExecutionSafetyState, InMemoryBrokerExecutionSafetyState>();
        services.TryAddSingleton<IBrokerIdempotencyStore, InMemoryBrokerIdempotencyStore>();
        services.TryAddSingleton<IBrokerExecutionRecordWriter, NoOpBrokerExecutionRecordWriter>();
        services.TryAddSingleton<IBrokerReconciliationRecordWriter, NoOpBrokerReconciliationRecordWriter>();
        services.TryAddSingleton<IBrokerAuditWriter, NoOpBrokerAuditWriter>();
        services.AddSingleton<BrokerExecutionValidator>();
        services.AddSingleton<IBrokerExecutionService, BrokerExecutionService>();
        services.AddSingleton<IBrokerExecutionLifecycleService, BrokerExecutionLifecycleService>();
        return services;
    }

    private sealed class NoOpTradeMindMetrics : ITradeMindMetrics
    {
        public void IncrementCounter(string name, long value, MetricDimensions dimensions) { }
        public void RecordDuration(string name, TimeSpan duration, MetricDimensions dimensions) { }
        public void SetActiveExecutionSessions(long value) { }
    }

    private sealed class NoOpTradeMindTelemetry : ITradeMindTelemetry
    {
        public ITradeMindActivity StartActivity(TelemetryOperation operation, TelemetryContext? context = null, IReadOnlyCollection<TelemetryLink>? links = null) => new Activity();
        public void EnrichCurrent(TelemetryContext context) { }

        private sealed class Activity : ITradeMindActivity
        {
            public bool IsRecording => false;
            public string? TraceId => null;
            public string? SpanId => null;
            public void SetTag(string name, string? value) { }
            public void SetTag(string name, long? value) { }
            public void SetOutcome(TelemetryOutcome outcome) { }
            public void RecordException(Exception exception) { }
            public void Dispose() { }
        }
    }

    private sealed class NoConfiguredRiskGuardPolicy : IRiskGuardPolicy
    {
        public RiskGuardDecision Evaluate(RiskGuardMeasurement measurement) => new(1, false, [new("RISK_POLICY_NOT_CONFIGURED", "A versioned risk guard policy must be explicitly configured before execution.")]);
    }

    private sealed class NoOpBrokerReconciliationService(IBrokerClock clock) : IBrokerReconciliationService
    {
        public Task<BrokerReconciliationReport> ReconcileAsync(BrokerExecutionContext context, BrokerConnectorId connectorId, BrokerAccountId accountId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = clock.UtcNow;
            return Task.FromResult(new BrokerReconciliationReport(new BrokerReconciliationId("not-configured"), connectorId, accountId, now, now, [], new BrokerError("RECONCILIATION_NOT_CONFIGURED", BrokerErrorCategory.UnsupportedCapability, "Broker reconciliation infrastructure is not configured.", false, true, null, new BrokerTraceReference(context.CorrelationId, null, context.ExecutionSessionId), now)));
        }
    }
}
