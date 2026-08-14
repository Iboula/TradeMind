using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.Application.LiveSafety;

public sealed class LiveSafetyOptionsValidator : IValidateOptions<LiveSafetyOptions>
{
    public ValidateOptionsResult Validate(string? name, LiveSafetyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        if (options.MaximumHeartbeatAge <= TimeSpan.Zero) errors.Add("TradeMind:Brokers:LiveSafety:MaximumHeartbeatAge must be positive.");
        if (options.MaximumReconciliationAge <= TimeSpan.Zero) errors.Add("TradeMind:Brokers:LiveSafety:MaximumReconciliationAge must be positive.");
        if (options.MaximumConcurrentExecutions is < 1 or > 1024) errors.Add("TradeMind:Brokers:LiveSafety:MaximumConcurrentExecutions must be between 1 and 1024.");
        if (options.EnvironmentName.Equals("Production", StringComparison.OrdinalIgnoreCase) && options.AllowLive && !options.Enabled)
            errors.Add("Live safety cannot be disabled while live execution is configured in Production.");
        if (options.AllowLive && (!options.RequireDualConfirmation || !options.RequireDistributedLock || !options.RequireCleanReconciliation || !options.RequireFreshHeartbeat || !options.RequireNoOrphans))
            errors.Add("Live execution requires all independent safety controls to remain enabled.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

public sealed class LiveTradingSafetyGate(
    IOptions<LiveSafetyOptions> options,
    TimeProvider timeProvider,
    ITradeMindMetrics metrics) : ILiveTradingSafetyGate
{
    public ValueTask<LiveTradingSafetyDecision> EvaluateAsync(LiveTradingSafetyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var value = options.Value;
        var causes = new List<LiveSafetyCause>();

        if (request.KillSwitchState == KillSwitchState.EmergencyStopped)
            causes.Add(new("EMERGENCY_STOP_ACTIVE", LiveSafetyCauseCategory.KillSwitch, "The execution scope is emergency-stopped."));
        else if (request.KillSwitchState == KillSwitchState.Enabled || !request.KillSwitchDisabled)
            causes.Add(new("KILL_SWITCH_ACTIVE", LiveSafetyCauseCategory.KillSwitch, "A kill switch is active."));

        if (request.MaintenanceMode)
            causes.Add(new("MAINTENANCE_MODE", LiveSafetyCauseCategory.Maintenance, "Execution is blocked during maintenance."));
        if (request.ExecutionQuarantined)
            causes.Add(new("EXECUTION_QUARANTINED", LiveSafetyCauseCategory.Quarantine, "The execution requires explicit reconciliation."));
        if (!request.AllowLive || !value.AllowLive)
            causes.Add(new("LIVE_DISABLED", LiveSafetyCauseCategory.Configuration, "Live execution is disabled by configuration."));
        if (!request.LiveSafetyEnabled || !value.Enabled)
            causes.Add(new("LIVE_SAFETY_DISABLED", LiveSafetyCauseCategory.Configuration, "The Live Safety Gate is disabled."));
        if (request.AccountEnvironment != TradeMind.Brokers.Domain.BrokerEnvironment.Production)
            causes.Add(new("ACCOUNT_NOT_LIVE", LiveSafetyCauseCategory.Account, "The account environment is not Production."));
        if (!request.AccountVerifiedLive)
            causes.Add(new("ACCOUNT_NOT_VERIFIED", LiveSafetyCauseCategory.Account, "The account has not been explicitly verified as Live."));
        if (!request.BrokerLiveCapable)
            causes.Add(new("BROKER_NOT_LIVE_CAPABLE", LiveSafetyCauseCategory.Broker, "The broker is not marked as Live-capable."));
        if (!request.TenantAuthorized)
            causes.Add(new("TENANT_NOT_AUTHORIZED", LiveSafetyCauseCategory.Tenant, "The tenant is not authorized for Live execution."));
        if (!request.ActorAuthorized)
            causes.Add(new("ACTOR_NOT_AUTHORIZED", LiveSafetyCauseCategory.Actor, "The actor lacks the Live execution permission."));
        if (!request.RiskApproved)
            causes.Add(new("RISK_NOT_APPROVED", LiveSafetyCauseCategory.Risk, "Risk assessment is not Approved."));
        if (!request.PlanExecutable)
            causes.Add(new("PLAN_NOT_EXECUTABLE", LiveSafetyCauseCategory.TradingPlan, "The TradingPlan is not an ExecutableCandidate."));
        if (!request.WorkspaceClear)
            causes.Add(new("WORKSPACE_BLOCKED", LiveSafetyCauseCategory.Workspace, "The TradingWorkspace is blocked or incomplete."));
        if (!request.ExecutionSessionActive)
            causes.Add(new("EXECUTION_SESSION_INACTIVE", LiveSafetyCauseCategory.ExecutionSession, "The ExecutionSession is not active."));
        if (!request.ReconciliationClean || (value.RequireCleanReconciliation && !request.ReconciliationClean))
            causes.Add(new("RECONCILIATION_REQUIRED", LiveSafetyCauseCategory.Reconciliation, "Reconciliation is not clean."));
        if (request.HasOrphanPositions || (value.RequireNoOrphans && request.HasOrphanPositions))
            causes.Add(new("ORPHAN_POSITION", LiveSafetyCauseCategory.Reconciliation, "An orphan position requires manual review."));
        if (!request.HeartbeatFresh || (value.RequireFreshHeartbeat && !request.HeartbeatFresh))
            causes.Add(new("HEARTBEAT_STALE", LiveSafetyCauseCategory.Heartbeat, "The broker heartbeat is not fresh."));
        if (!request.BrokerHealthy)
            causes.Add(new("BROKER_UNHEALTHY", LiveSafetyCauseCategory.Broker, "Broker health is not Healthy."));
        if (!request.IdempotencyAvailable || !request.IdempotencyKeyAvailable)
            causes.Add(new("IDEMPOTENCY_UNAVAILABLE", LiveSafetyCauseCategory.Idempotency, "Durable idempotency is unavailable."));
        if (!request.DistributedLockAvailable || (value.RequireDistributedLock && !request.DistributedLockAvailable))
            causes.Add(new("DISTRIBUTED_LOCK_UNAVAILABLE", LiveSafetyCauseCategory.Lock, "The distributed execution lock is unavailable."));
        if (!request.DrawdownWithinLimit)
            causes.Add(new("DRAWDOWN_LIMIT", LiveSafetyCauseCategory.Drawdown, "The drawdown guard rejected the execution."));
        if (!request.ExposureWithinLimit)
            causes.Add(new("EXPOSURE_LIMIT", LiveSafetyCauseCategory.Exposure, "The exposure guard rejected the execution."));
        if (!request.ActivationWindowValid)
            causes.Add(new("ACTIVATION_WINDOW_INVALID", LiveSafetyCauseCategory.TradingWindow, "The Live activation window is not valid."));
        if (value.RequireDualConfirmation && !request.DualConfirmationComplete)
            causes.Add(new("DUAL_CONFIRMATION_REQUIRED", LiveSafetyCauseCategory.Confirmation, "Application and operator confirmations are not both present."));
        else if (!request.OperatorConfirmed)
            causes.Add(new("OPERATOR_CONFIRMATION_REQUIRED", LiveSafetyCauseCategory.Confirmation, "Explicit operator confirmation is required."));

        LiveSafetyDecisionStatus status;
        if (causes.Any(cause => cause.Category == LiveSafetyCauseCategory.KillSwitch && request.KillSwitchState == KillSwitchState.EmergencyStopped))
            status = LiveSafetyDecisionStatus.EmergencyStop;
        else if (causes.Any(cause => cause.Code == "LIVE_DISABLED" || cause.Code == "LIVE_SAFETY_DISABLED" || cause.Code == "ACCOUNT_NOT_LIVE"))
            status = LiveSafetyDecisionStatus.Denied;
        else if (causes.Any(cause => cause.Code == "EXECUTION_QUARANTINED" || cause.Code == "RECONCILIATION_REQUIRED" || cause.Code == "ORPHAN_POSITION"))
            status = LiveSafetyDecisionStatus.RequiresReconciliation;
        else if (causes.Any(cause => cause.Category == LiveSafetyCauseCategory.Confirmation))
            status = LiveSafetyDecisionStatus.RequiresConfirmation;
        else if (causes.Count > 0)
            status = LiveSafetyDecisionStatus.Blocked;
        else
            status = LiveSafetyDecisionStatus.Allowed;

        var decision = new LiveTradingSafetyDecision(status, causes, timeProvider.GetUtcNow(), 1);
        if (status != LiveSafetyDecisionStatus.Allowed)
        {
            metrics.IncrementCounter(status == LiveSafetyDecisionStatus.Denied ? TelemetryMetricNames.LiveGateDenied : TelemetryMetricNames.LiveGateBlocked, 1, new MetricDimensions(Module: "Brokers", Operation: "LiveGate", Stage: "Safety", Outcome: status.ToString(), Environment: request.ConfigurationEnvironment));
        }
        return ValueTask.FromResult(decision);
    }
}

public sealed class DefaultRiskGuardPolicy(VersionedRiskGuardPolicy policy, ITradeMindMetrics? metrics = null) : IRiskGuardPolicy
{
    private readonly VersionedRiskGuardPolicy policy = policy ?? throw new ArgumentNullException(nameof(policy));

    public RiskGuardDecision Evaluate(RiskGuardMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        var value = measurement.Validate();
        var limits = policy.Limits;
        var rejections = new List<RiskGuardRejection>();
        Check(value.DailyLoss > limits.MaxDailyLoss, "MAX_DAILY_LOSS", "The daily loss limit was exceeded.");
        Check(value.WeeklyLoss > limits.MaxWeeklyLoss, "MAX_WEEKLY_LOSS", "The weekly loss limit was exceeded.");
        Check(value.Drawdown > limits.MaxDrawdown, "MAX_DRAWDOWN", "The drawdown limit was exceeded.");
        Check(value.TradesToday > limits.MaxTradesPerDay, "MAX_TRADES_PER_DAY", "The daily trade limit was exceeded.");
        Check(value.ConsecutiveLosses > limits.MaxConsecutiveLosses, "MAX_CONSECUTIVE_LOSSES", "The consecutive loss limit was exceeded.");
        Check(value.ConcurrentPositions > limits.MaxConcurrentPositions, "MAX_CONCURRENT_POSITIONS", "The concurrent position limit was exceeded.");
        Check(value.InstrumentExposure > limits.MaxInstrumentExposure, "MAX_INSTRUMENT_EXPOSURE", "The instrument exposure limit was exceeded.");
        Check(value.PortfolioExposure > limits.MaxPortfolioExposure, "MAX_PORTFOLIO_EXPOSURE", "The portfolio exposure limit was exceeded.");
        Check(value.Notional > limits.MaxNotional, "MAX_NOTIONAL", "The notional limit was exceeded.");
        Check(value.OrderQuantity > limits.MaxOrderQuantity, "MAX_ORDER_QUANTITY", "The order quantity limit was exceeded.");
        var decision = new RiskGuardDecision(policy.Version, rejections.Count == 0, rejections);
        if (rejections.Count > 0)
            metrics?.IncrementCounter(TelemetryMetricNames.RiskGuardRejections, rejections.Count, new MetricDimensions(Module: "Brokers", Operation: "RiskGuard", Stage: "Safety", Outcome: "Rejected"));
        return decision;

        void Check(bool rejected, string code, string message)
        {
            if (rejected) rejections.Add(new RiskGuardRejection(code, message));
        }
    }
}

public sealed class InMemoryKillSwitchStore(TimeProvider timeProvider, ICriticalAuditWriter auditWriter, ITradeMindMetrics? metrics = null) : IKillSwitchStore
{
    private readonly ConcurrentDictionary<string, KillSwitchSnapshot> states = new(StringComparer.Ordinal);

    public Task<KillSwitchSnapshot> GetAsync(KillSwitchKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(states.GetOrAdd(key.ToString(), _ => Disabled(key)));
    }

    public Task<KillSwitchSnapshot> DisableAsync(KillSwitchKey key, string actorId, string reason, CancellationToken cancellationToken = default) => ChangeAsync(key, KillSwitchState.Disabled, actorId, reason, cancellationToken, false);
    public Task<KillSwitchSnapshot> EnableAsync(KillSwitchKey key, string actorId, string reason, CancellationToken cancellationToken = default) => ChangeAsync(key, KillSwitchState.Enabled, actorId, reason, cancellationToken, false);
    public Task<KillSwitchSnapshot> EmergencyStopAsync(KillSwitchKey key, string actorId, string reason, CancellationToken cancellationToken = default) => ChangeAsync(key, KillSwitchState.EmergencyStopped, actorId, reason, cancellationToken, false);

    public Task<KillSwitchSnapshot> ReactivateEmergencyStoppedAsync(KillSwitchKey key, string actorId, string reason, bool explicitlyAudited, CancellationToken cancellationToken = default)
    {
        if (!explicitlyAudited) throw new InvalidOperationException("Emergency stop reactivation requires an explicit audit acknowledgement.");
        return ChangeAsync(key, KillSwitchState.Disabled, actorId, reason, cancellationToken, true);
    }

    private async Task<KillSwitchSnapshot> ChangeAsync(KillSwitchKey key, KillSwitchState state, string actorId, string reason, CancellationToken cancellationToken, bool allowEmergencyReset)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        cancellationToken.ThrowIfCancellationRequested();
        var current = await GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (current.State == KillSwitchState.EmergencyStopped && !allowEmergencyReset)
            throw new InvalidOperationException("An emergency-stopped kill switch cannot be reactivated automatically.");
        var next = new KillSwitchSnapshot(key, state, actorId.Trim(), timeProvider.GetUtcNow(), reason.Trim());
        states[key.ToString()] = next;
        if (state == KillSwitchState.EmergencyStopped)
            metrics?.IncrementCounter(TelemetryMetricNames.EmergencyStops, 1, new MetricDimensions(Module: "Brokers", Operation: "KillSwitch", Stage: "Safety", Outcome: state.ToString()));
        await auditWriter.WriteAsync(new CriticalAuditEntry("KillSwitch", state.ToString(), key.Value, key.Scope == KillSwitchScope.Broker ? key.Value : null, key.Scope == KillSwitchScope.Account ? key.Value : null, null, actorId, next.ChangedAtUtc, new Dictionary<string, string> { ["scope"] = key.Scope.ToString(), ["reason"] = next.Reason }), cancellationToken).ConfigureAwait(false);
        return next;
    }

    private KillSwitchSnapshot Disabled(KillSwitchKey key) => new(key, KillSwitchState.Disabled, null, timeProvider.GetUtcNow(), "Default safe state.");
}

public sealed class InMemoryExecutionQuarantineStore(TimeProvider timeProvider, ICriticalAuditWriter auditWriter, ITradeMindMetrics? metrics = null) : IExecutionQuarantineStore
{
    private readonly ConcurrentDictionary<string, ExecutionQuarantineRecord> records = new(StringComparer.Ordinal);

    public Task<ExecutionQuarantineRecord?> GetAsync(string executionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        records.TryGetValue(executionId, out var record);
        return Task.FromResult(record);
    }

    public async Task<ExecutionQuarantineRecord> QuarantineAsync(ExecutionQuarantineRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        var stored = records.AddOrUpdate(record.ExecutionId, record, (_, existing) => existing.State == ExecutionQuarantineState.Quarantined ? existing : record);
        metrics?.IncrementCounter(TelemetryMetricNames.QuarantinedExecutions, 1, new MetricDimensions(Module: "Brokers", Operation: "Quarantine", Stage: "Safety", Outcome: stored.Reason.ToString()));
        await auditWriter.WriteAsync(new CriticalAuditEntry("ExecutionQuarantine", stored.State.ToString(), stored.TenantId, stored.BrokerId, stored.AccountId, stored.ExecutionId, stored.ChangedBy, stored.ChangedAtUtc, new Dictionary<string, string> { ["reason"] = stored.Reason.ToString() }), cancellationToken).ConfigureAwait(false);
        return stored;
    }

    public async Task<ExecutionQuarantineRecord> ReleaseAsync(string executionId, string actorId, string reason, CancellationToken cancellationToken = default)
    {
        if (!records.TryGetValue(executionId, out var current)) throw new KeyNotFoundException($"Execution '{executionId}' is not quarantined.");
        var released = new ExecutionQuarantineRecord(current.ExecutionId, current.TenantId, current.BrokerId, current.AccountId, current.Instrument, current.Reason, ExecutionQuarantineState.ExplicitlyReleased, timeProvider.GetUtcNow(), actorId, reason);
        records[executionId] = released;
        await auditWriter.WriteAsync(new CriticalAuditEntry("ExecutionQuarantineRelease", released.State.ToString(), released.TenantId, released.BrokerId, released.AccountId, released.ExecutionId, released.ChangedBy, released.ChangedAtUtc, new Dictionary<string, string> { ["reason"] = released.Explanation }), cancellationToken).ConfigureAwait(false);
        return released;
    }
}

public sealed class EmptyBrokerExecutionRecoveryReader : IBrokerExecutionRecoveryReader
{
    public Task<BrokerRecoverySnapshot> ReadAsync(string tenantId, string brokerId, string accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new BrokerRecoverySnapshot([], [], [], null));
    }
}

public sealed class BrokerExecutionRecoveryService(
    IBrokerExecutionRecoveryReader reader,
    TimeProvider timeProvider,
    ITradeMindMetrics metrics,
    ICriticalAuditWriter auditWriter) : IBrokerExecutionRecoveryService
{
    public async Task<RecoveryPlan> RecoverAsync(string tenantId, string brokerId, string accountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        var snapshot = await reader.ReadAsync(tenantId, brokerId, accountId, cancellationToken).ConfigureAwait(false);
        var mismatches = new List<RecoveryMismatch>();
        var ordersByExecution = snapshot.BrokerOrders.Select(order => order.ExecutionId.Value).ToHashSet(StringComparer.Ordinal);
        mismatches.AddRange(snapshot.NonTerminalExecutions.Where(execution => !ordersByExecution.Contains(execution.ExecutionId)).Select(execution => new RecoveryMismatch(RecoveryMismatchType.NonTerminalExecutionMissingAtBroker, execution.ExecutionId, "A non-terminal TradeMind execution has no broker order.")));
        mismatches.AddRange(snapshot.BrokerPositions.Where(position => string.IsNullOrWhiteSpace(position.ExecutionId)).Select(position => new RecoveryMismatch(RecoveryMismatchType.UnknownExternalPosition, position.PositionId, "The broker position has no TradeMind execution ownership.")));
        if (snapshot.LastReconciliationAtUtc is null)
            mismatches.Add(new RecoveryMismatch(RecoveryMismatchType.StaleReconciliation, "reconciliation", "No reconciliation timestamp is available."));
        var plan = new RecoveryPlan(tenantId, brokerId, accountId, timeProvider.GetUtcNow(), mismatches);
        metrics.IncrementCounter(TelemetryMetricNames.RecoveryRuns, 1, new MetricDimensions(Module: "Brokers", Operation: "Recovery", Stage: "Safety", Outcome: plan.RequiresManualReview ? "Mismatch" : "Clean"));
        if (plan.RequiresManualReview)
            metrics.IncrementCounter(TelemetryMetricNames.RecoveryMismatches, plan.Mismatches.Count, new MetricDimensions(Module: "Brokers", Operation: "Recovery", Stage: "Safety", Outcome: "Mismatch"));
        await auditWriter.WriteAsync(new CriticalAuditEntry("Recovery", plan.RequiresManualReview ? "Mismatch" : "Clean", tenantId, brokerId, accountId, null, null, plan.CreatedAtUtc, new Dictionary<string, string> { ["mismatch_count"] = plan.Mismatches.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), ["read_only"] = "true" }), cancellationToken).ConfigureAwait(false);
        return plan;
    }
}

public sealed class InMemoryBrokerPositionOwnershipStore : IBrokerPositionOwnershipStore
{
    private readonly ConcurrentDictionary<string, BrokerPositionOwnership> records = new(StringComparer.Ordinal);

    public Task<BrokerPositionOwnership?> GetAsync(string positionId, string tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(records.TryGetValue(tenantId + ":" + positionId, out var ownership) ? ownership : null);
    }

    public Task SaveAsync(BrokerPositionOwnership ownership, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        cancellationToken.ThrowIfCancellationRequested();
        records[ownership.TenantId + ":" + ownership.PositionId] = ownership;
        return Task.CompletedTask;
    }
}

public sealed class NoOpCriticalAuditWriter : ICriticalAuditWriter
{
    public Task WriteAsync(CriticalAuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class EmptyLiveSafetyReadiness : ILiveSafetyReadiness
{
    public LiveSafetyReadinessSnapshot Snapshot { get; } = new(true, true, true, true, true, true, true, true, true, true);
}

public sealed class UnavailableBrokerExecutionLock : IBrokerExecutionLock
{
    public Task<BrokerExecutionLockResult> AcquireAsync(BrokerExecutionLockScope scope, string ownerId, BrokerExecutionLockOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BrokerExecutionLockResult.Unavailable("A PostgreSQL execution lock is not configured."));
    }
}

public sealed class InMemoryOperatorActionService(
    IKillSwitchStore killSwitchStore,
    TimeProvider timeProvider,
    ICriticalAuditWriter auditWriter) : IOperatorActionService
{
    private readonly ConcurrentDictionary<string, OperatorActionResult> results = new(StringComparer.Ordinal);

    public async Task<OperatorActionResult> ApplyAsync(OperatorActionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (results.TryGetValue(request.ActionId, out var replay)) return replay;
        if (request.Target.Contains("tenant=", StringComparison.OrdinalIgnoreCase) && !request.Target.Contains("tenant=" + request.TenantId, StringComparison.OrdinalIgnoreCase))
            return await RecordAsync(request, false, "CROSS_TENANT_TARGET", "The operator action is outside the current tenant.", cancellationToken).ConfigureAwait(false);
        if (request.RequiredPermission.Length == 0)
            return await RecordAsync(request, false, "PERMISSION_REQUIRED", "An explicit permission is required.", cancellationToken).ConfigureAwait(false);

        if (request.Action == OperatorActionType.EmergencyStop)
            await killSwitchStore.EmergencyStopAsync(new KillSwitchKey(KillSwitchScope.Tenant, request.TenantId), request.ActorId, request.Reason, cancellationToken).ConfigureAwait(false);

        return await RecordAsync(request, true, "APPLIED", "The operator action was accepted and audited.", cancellationToken).ConfigureAwait(false);
    }

    private async Task<OperatorActionResult> RecordAsync(OperatorActionRequest request, bool applied, string code, string message, CancellationToken cancellationToken)
    {
        var result = new OperatorActionResult(request.ActionId, applied, code, message, timeProvider.GetUtcNow());
        results.TryAdd(request.ActionId, result);
        await auditWriter.WriteAsync(new CriticalAuditEntry("OperatorAction", code, request.TenantId, null, null, null, request.ActorId, result.AppliedAtUtc, new Dictionary<string, string> { ["action"] = request.Action.ToString(), ["target"] = request.Target }), cancellationToken).ConfigureAwait(false);
        return result;
    }
}
