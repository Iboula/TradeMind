using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.LiveSafety;

public enum LiveSafetyDecisionStatus
{
    Allowed,
    Denied,
    Blocked,
    RequiresConfirmation,
    RequiresReconciliation,
    EmergencyStop
}

public enum LiveSafetyCauseCategory
{
    Configuration,
    Account,
    Tenant,
    Broker,
    Actor,
    Risk,
    ExecutionSession,
    TradingPlan,
    Workspace,
    Heartbeat,
    Idempotency,
    Reconciliation,
    Drawdown,
    Exposure,
    KillSwitch,
    Confirmation,
    TradingWindow,
    Maintenance,
    Quarantine,
    Lock
}

public enum KillSwitchScope
{
    Global,
    Tenant,
    Broker,
    Account,
    Instrument,
    ExecutionSession
}

public enum KillSwitchState
{
    Enabled,
    Disabled,
    EmergencyStopped
}

public enum ExecutionQuarantineReason
{
    AmbiguousTimeout,
    BrokerStateMismatch,
    OrphanPosition,
    CleanupFailure,
    DuplicateFill,
    StaleReconciliation,
    ReconnectDuringSubmit,
    UnknownBrokerResponse
}

public enum ExecutionQuarantineState
{
    Quarantined,
    ExplicitlyReleased
}

public enum RecoveryMismatchType
{
    NonTerminalExecutionMissingAtBroker,
    BrokerOrderWithoutExecution,
    UnknownExternalPosition,
    PositionOwnershipMismatch,
    StaleReconciliation
}

public enum OperatorActionType
{
    Resume,
    Reconcile,
    Quarantine,
    Acknowledge,
    EmergencyStop,
    RequestManualClose,
    MarkExternallyResolved
}

public sealed record LiveSafetyCause
{
    public LiveSafetyCause(string code, LiveSafetyCauseCategory category, string message, bool blocking = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Category = category;
        Message = message.Trim();
        Blocking = blocking;
    }

    public string Code { get; }
    public LiveSafetyCauseCategory Category { get; }
    public string Message { get; }
    public bool Blocking { get; }
}

public sealed record LiveTradingSafetyRequest
{
    public LiveTradingSafetyRequest(
        string tenantId,
        string brokerId,
        string accountId,
        string instrument,
        string executionSessionId,
        bool allowLive,
        bool liveSafetyEnabled,
        BrokerEnvironment accountEnvironment,
        bool brokerLiveCapable,
        bool accountVerifiedLive,
        bool tenantAuthorized,
        bool actorAuthorized,
        bool riskApproved,
        bool planExecutable,
        bool workspaceClear,
        bool reconciliationClean,
        bool hasOrphanPositions,
        bool heartbeatFresh,
        bool brokerHealthy,
        bool idempotencyAvailable,
        bool distributedLockAvailable,
        bool operatorConfirmed,
        bool activationWindowValid,
        bool killSwitchDisabled,
        bool dualConfirmationComplete,
        bool drawdownWithinLimit,
        bool exposureWithinLimit,
        bool maintenanceMode = false,
        bool executionQuarantined = false,
        bool executionSessionActive = true,
        bool idempotencyKeyAvailable = true,
        KillSwitchState killSwitchState = KillSwitchState.Disabled,
        string configurationEnvironment = "Development")
    {
        TenantId = Required(tenantId, nameof(tenantId));
        BrokerId = Required(brokerId, nameof(brokerId));
        AccountId = Required(accountId, nameof(accountId));
        Instrument = Required(instrument, nameof(instrument));
        ExecutionSessionId = Required(executionSessionId, nameof(executionSessionId));
        ConfigurationEnvironment = Required(configurationEnvironment, nameof(configurationEnvironment));
        AllowLive = allowLive;
        LiveSafetyEnabled = liveSafetyEnabled;
        AccountEnvironment = accountEnvironment;
        BrokerLiveCapable = brokerLiveCapable;
        AccountVerifiedLive = accountVerifiedLive;
        TenantAuthorized = tenantAuthorized;
        ActorAuthorized = actorAuthorized;
        RiskApproved = riskApproved;
        PlanExecutable = planExecutable;
        WorkspaceClear = workspaceClear;
        ReconciliationClean = reconciliationClean;
        HasOrphanPositions = hasOrphanPositions;
        HeartbeatFresh = heartbeatFresh;
        BrokerHealthy = brokerHealthy;
        IdempotencyAvailable = idempotencyAvailable;
        DistributedLockAvailable = distributedLockAvailable;
        OperatorConfirmed = operatorConfirmed;
        ActivationWindowValid = activationWindowValid;
        KillSwitchDisabled = killSwitchDisabled;
        DualConfirmationComplete = dualConfirmationComplete;
        DrawdownWithinLimit = drawdownWithinLimit;
        ExposureWithinLimit = exposureWithinLimit;
        MaintenanceMode = maintenanceMode;
        ExecutionQuarantined = executionQuarantined;
        ExecutionSessionActive = executionSessionActive;
        IdempotencyKeyAvailable = idempotencyKeyAvailable;
        KillSwitchState = killSwitchState;
    }

    public string TenantId { get; }
    public string BrokerId { get; }
    public string AccountId { get; }
    public string Instrument { get; }
    public string ExecutionSessionId { get; }
    public bool AllowLive { get; }
    public bool LiveSafetyEnabled { get; }
    public BrokerEnvironment AccountEnvironment { get; }
    public bool BrokerLiveCapable { get; }
    public bool AccountVerifiedLive { get; }
    public bool TenantAuthorized { get; }
    public bool ActorAuthorized { get; }
    public bool RiskApproved { get; }
    public bool PlanExecutable { get; }
    public bool WorkspaceClear { get; }
    public bool ReconciliationClean { get; }
    public bool HasOrphanPositions { get; }
    public bool HeartbeatFresh { get; }
    public bool BrokerHealthy { get; }
    public bool IdempotencyAvailable { get; }
    public bool DistributedLockAvailable { get; }
    public bool OperatorConfirmed { get; }
    public bool ActivationWindowValid { get; }
    public bool KillSwitchDisabled { get; }
    public bool DualConfirmationComplete { get; }
    public bool DrawdownWithinLimit { get; }
    public bool ExposureWithinLimit { get; }
    public bool MaintenanceMode { get; }
    public bool ExecutionQuarantined { get; }
    public bool ExecutionSessionActive { get; }
    public bool IdempotencyKeyAvailable { get; }
    public KillSwitchState KillSwitchState { get; }
    public string ConfigurationEnvironment { get; }

    private static string Required(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }
}

public sealed record LiveTradingSafetyDecision
{
    public LiveTradingSafetyDecision(
        LiveSafetyDecisionStatus status,
        IReadOnlyCollection<LiveSafetyCause> causes,
        DateTimeOffset evaluatedAtUtc,
        int policyVersion)
    {
        ArgumentNullException.ThrowIfNull(causes);
        if (evaluatedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(evaluatedAtUtc));
        if (policyVersion <= 0) throw new ArgumentOutOfRangeException(nameof(policyVersion));
        Status = status;
        Causes = Array.AsReadOnly(causes.OrderBy(item => item.Code, StringComparer.Ordinal).ToArray());
        EvaluatedAtUtc = evaluatedAtUtc;
        PolicyVersion = policyVersion;
    }

    public LiveSafetyDecisionStatus Status { get; }
    public IReadOnlyList<LiveSafetyCause> Causes { get; }
    public DateTimeOffset EvaluatedAtUtc { get; }
    public int PolicyVersion { get; }
    public bool IsAllowed => Status == LiveSafetyDecisionStatus.Allowed;
}

public interface ILiveTradingSafetyGate
{
    ValueTask<LiveTradingSafetyDecision> EvaluateAsync(LiveTradingSafetyRequest request, CancellationToken cancellationToken = default);
}

public sealed record LiveSafetyOptions
{
    public const string SectionName = "TradeMind:Brokers:LiveSafety";
    public bool Enabled { get; init; }
    public bool AllowLive { get; init; }
    public bool RequireDualConfirmation { get; init; } = true;
    public bool RequireDistributedLock { get; init; } = true;
    public bool RequireCleanReconciliation { get; init; } = true;
    public bool RequireFreshHeartbeat { get; init; } = true;
    public bool RequireNoOrphans { get; init; } = true;
    public TimeSpan MaximumHeartbeatAge { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaximumReconciliationAge { get; init; } = TimeSpan.FromMinutes(5);
    public int MaximumConcurrentExecutions { get; init; } = 1;
    public bool EmergencyStopOnCleanupFailure { get; init; } = true;
    public bool EmergencyStopOnUnknownExecution { get; init; } = true;
    public string EnvironmentName { get; init; } = "Development";
}

public sealed record KillSwitchKey
{
    public KillSwitchKey(KillSwitchScope scope, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Scope = scope;
        Value = value.Trim();
    }

    public KillSwitchScope Scope { get; }
    public string Value { get; }
    public override string ToString() => $"{Scope}:{Value}";
}

public sealed record KillSwitchSnapshot(KillSwitchKey Key, KillSwitchState State, string? ChangedBy, DateTimeOffset ChangedAtUtc, string Reason)
{
    public bool IsActive => State != KillSwitchState.Disabled;
}

public interface IKillSwitchStore
{
    Task<KillSwitchSnapshot> GetAsync(KillSwitchKey key, CancellationToken cancellationToken = default);
    Task<KillSwitchSnapshot> DisableAsync(KillSwitchKey key, string actorId, string reason, CancellationToken cancellationToken = default);
    Task<KillSwitchSnapshot> EnableAsync(KillSwitchKey key, string actorId, string reason, CancellationToken cancellationToken = default);
    Task<KillSwitchSnapshot> EmergencyStopAsync(KillSwitchKey key, string actorId, string reason, CancellationToken cancellationToken = default);
    Task<KillSwitchSnapshot> ReactivateEmergencyStoppedAsync(KillSwitchKey key, string actorId, string reason, bool explicitlyAudited, CancellationToken cancellationToken = default);
}

public sealed record RiskGuardLimits(
    decimal MaxDailyLoss,
    decimal MaxWeeklyLoss,
    decimal MaxDrawdown,
    int MaxTradesPerDay,
    int MaxConsecutiveLosses,
    int MaxConcurrentPositions,
    decimal MaxInstrumentExposure,
    decimal MaxPortfolioExposure,
    decimal MaxNotional,
    decimal MaxOrderQuantity)
{
    public RiskGuardLimits Validate()
    {
        if (MaxDailyLoss <= 0 || MaxWeeklyLoss <= 0 || MaxDrawdown <= 0 || MaxTradesPerDay <= 0 || MaxConsecutiveLosses <= 0 || MaxConcurrentPositions <= 0 || MaxInstrumentExposure <= 0 || MaxPortfolioExposure <= 0 || MaxNotional <= 0 || MaxOrderQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxDailyLoss), "Risk guard limits must be positive.");
        return this;
    }
}

public sealed record VersionedRiskGuardPolicy
{
    public VersionedRiskGuardPolicy(int version, RiskGuardLimits limits)
    {
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        Version = version;
        Limits = (limits ?? throw new ArgumentNullException(nameof(limits))).Validate();
    }

    public int Version { get; }
    public RiskGuardLimits Limits { get; }
}

public sealed record RiskGuardMeasurement(
    decimal DailyLoss,
    decimal WeeklyLoss,
    decimal Drawdown,
    int TradesToday,
    int ConsecutiveLosses,
    int ConcurrentPositions,
    decimal InstrumentExposure,
    decimal PortfolioExposure,
    decimal Notional,
    decimal OrderQuantity)
{
    public RiskGuardMeasurement Validate()
    {
        if (DailyLoss < 0 || WeeklyLoss < 0 || Drawdown < 0 || TradesToday < 0 || ConsecutiveLosses < 0 || ConcurrentPositions < 0 || InstrumentExposure < 0 || PortfolioExposure < 0 || Notional < 0 || OrderQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(DailyLoss), "Risk guard measurements cannot be negative.");
        return this;
    }
}

public sealed record RiskGuardRejection
{
    public RiskGuardRejection(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
    }

    public string Code { get; }
    public string Message { get; }
}

public sealed record RiskGuardDecision
{
    public RiskGuardDecision(int policyVersion, bool approved, IReadOnlyCollection<RiskGuardRejection> rejections)
    {
        if (policyVersion <= 0) throw new ArgumentOutOfRangeException(nameof(policyVersion));
        ArgumentNullException.ThrowIfNull(rejections);
        PolicyVersion = policyVersion;
        Approved = approved;
        Rejections = Array.AsReadOnly(rejections.OrderBy(item => item.Code, StringComparer.Ordinal).ToArray());
    }

    public int PolicyVersion { get; }
    public bool Approved { get; }
    public IReadOnlyList<RiskGuardRejection> Rejections { get; }
}

public interface IRiskGuardPolicy
{
    RiskGuardDecision Evaluate(RiskGuardMeasurement measurement);
}

public sealed record ExecutionQuarantineRecord
{
    public ExecutionQuarantineRecord(string executionId, string tenantId, string brokerId, string accountId, string instrument, ExecutionQuarantineReason reason, ExecutionQuarantineState state, DateTimeOffset changedAtUtc, string changedBy, string explanation)
    {
        ExecutionId = Required(executionId); TenantId = Required(tenantId); BrokerId = Required(brokerId); AccountId = Required(accountId); Instrument = Required(instrument);
        Reason = reason; State = state; ChangedAtUtc = EnsureUtc(changedAtUtc); ChangedBy = Required(changedBy); Explanation = Required(explanation);
    }

    public string ExecutionId { get; }
    public string TenantId { get; }
    public string BrokerId { get; }
    public string AccountId { get; }
    public string Instrument { get; }
    public ExecutionQuarantineReason Reason { get; }
    public ExecutionQuarantineState State { get; }
    public DateTimeOffset ChangedAtUtc { get; }
    public string ChangedBy { get; }
    public string Explanation { get; }
    private static string Required(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim(); }
    private static DateTimeOffset EnsureUtc(DateTimeOffset value) { if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC."); return value; }
}

public interface IExecutionQuarantineStore
{
    Task<ExecutionQuarantineRecord?> GetAsync(string executionId, CancellationToken cancellationToken = default);
    Task<ExecutionQuarantineRecord> QuarantineAsync(ExecutionQuarantineRecord record, CancellationToken cancellationToken = default);
    Task<ExecutionQuarantineRecord> ReleaseAsync(string executionId, string actorId, string reason, CancellationToken cancellationToken = default);
}

public sealed record BrokerRecoveryExecution(string ExecutionId, string TenantId, string BrokerId, string AccountId, string Instrument, string Status, DateTimeOffset UpdatedAtUtc);
public sealed record BrokerRecoveryPosition(string PositionId, string TenantId, string BrokerId, string AccountId, string Instrument, string? ExecutionId, string? TradingPlanId, string? RiskAssessmentId, string ReconciliationState);
public sealed record BrokerRecoverySnapshot
{
    public BrokerRecoverySnapshot(IReadOnlyCollection<BrokerRecoveryExecution> nonTerminalExecutions, IReadOnlyCollection<BrokerOrder> brokerOrders, IReadOnlyCollection<BrokerRecoveryPosition> brokerPositions, DateTimeOffset? lastReconciliationAtUtc)
    {
        NonTerminalExecutions = Array.AsReadOnly((nonTerminalExecutions ?? throw new ArgumentNullException(nameof(nonTerminalExecutions))).ToArray());
        BrokerOrders = Array.AsReadOnly((brokerOrders ?? throw new ArgumentNullException(nameof(brokerOrders))).ToArray());
        BrokerPositions = Array.AsReadOnly((brokerPositions ?? throw new ArgumentNullException(nameof(brokerPositions))).ToArray());
        if (lastReconciliationAtUtc.HasValue && lastReconciliationAtUtc.Value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(lastReconciliationAtUtc));
        LastReconciliationAtUtc = lastReconciliationAtUtc;
    }

    public IReadOnlyCollection<BrokerRecoveryExecution> NonTerminalExecutions { get; }
    public IReadOnlyCollection<BrokerOrder> BrokerOrders { get; }
    public IReadOnlyCollection<BrokerRecoveryPosition> BrokerPositions { get; }
    public DateTimeOffset? LastReconciliationAtUtc { get; }
}

public interface IBrokerExecutionRecoveryReader
{
    Task<BrokerRecoverySnapshot> ReadAsync(string tenantId, string brokerId, string accountId, CancellationToken cancellationToken = default);
}

public sealed record RecoveryMismatch(RecoveryMismatchType Type, string Reference, string Message);
public sealed record RecoveryPlan
{
    public RecoveryPlan(string tenantId, string brokerId, string accountId, DateTimeOffset createdAtUtc, IReadOnlyCollection<RecoveryMismatch> mismatches)
    {
        TenantId = Required(tenantId); BrokerId = Required(brokerId); AccountId = Required(accountId); CreatedAtUtc = EnsureUtc(createdAtUtc);
        Mismatches = Array.AsReadOnly((mismatches ?? throw new ArgumentNullException(nameof(mismatches))).OrderBy(item => item.Type).ThenBy(item => item.Reference, StringComparer.Ordinal).ToArray());
    }

    public string TenantId { get; }
    public string BrokerId { get; }
    public string AccountId { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public IReadOnlyList<RecoveryMismatch> Mismatches { get; }
    public bool RequiresManualReview => Mismatches.Count > 0;
    private static string Required(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim(); }
    private static DateTimeOffset EnsureUtc(DateTimeOffset value) { if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC."); return value; }
}

public interface IBrokerExecutionRecoveryService
{
    Task<RecoveryPlan> RecoverAsync(string tenantId, string brokerId, string accountId, CancellationToken cancellationToken = default);
}

public sealed record BrokerExecutionLockScope
{
    public BrokerExecutionLockScope(string tenantId, string brokerId, string accountId, string instrument)
    {
        TenantId = Required(tenantId); BrokerId = Required(brokerId); AccountId = Required(accountId); Instrument = Required(instrument);
    }

    public string TenantId { get; }
    public string BrokerId { get; }
    public string AccountId { get; }
    public string Instrument { get; }
    private static string Required(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim(); }
}

public sealed record BrokerExecutionLockOptions
{
    public BrokerExecutionLockOptions(TimeSpan acquisitionTimeout, TimeSpan pollInterval)
    {
        if (acquisitionTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(acquisitionTimeout));
        if (pollInterval <= TimeSpan.Zero || pollInterval > acquisitionTimeout) throw new ArgumentOutOfRangeException(nameof(pollInterval));
        AcquisitionTimeout = acquisitionTimeout; PollInterval = pollInterval;
    }

    public TimeSpan AcquisitionTimeout { get; }
    public TimeSpan PollInterval { get; }
}

public interface IBrokerExecutionLockLease : IAsyncDisposable
{
    string OwnerId { get; }
    BrokerExecutionLockScope Scope { get; }
}

public sealed record BrokerExecutionLockResult(bool Acquired, string? Reason, IBrokerExecutionLockLease? Lease)
{
    public static BrokerExecutionLockResult Unavailable(string reason) => new(false, reason, null);
    public static BrokerExecutionLockResult AcquiredBy(IBrokerExecutionLockLease lease) => new(true, null, lease);
}

public interface IBrokerExecutionLock
{
    Task<BrokerExecutionLockResult> AcquireAsync(BrokerExecutionLockScope scope, string ownerId, BrokerExecutionLockOptions options, CancellationToken cancellationToken = default);
}

public sealed record BrokerPositionOwnership
{
    public BrokerPositionOwnership(string positionId, string executionSessionId, string brokerExecutionId, string tradingPlanId, string riskAssessmentId, string tenantId, string brokerAccountId, string symbol, BrokerOrderSide direction, DateTimeOffset openedAtUtc, string source, string reconciliationState)
    {
        PositionId = Required(positionId); ExecutionSessionId = Required(executionSessionId); BrokerExecutionId = Required(brokerExecutionId); TradingPlanId = Required(tradingPlanId); RiskAssessmentId = Required(riskAssessmentId); TenantId = Required(tenantId); BrokerAccountId = Required(brokerAccountId); Symbol = Required(symbol); Direction = direction; OpenedAtUtc = EnsureUtc(openedAtUtc); Source = Required(source); ReconciliationState = Required(reconciliationState);
    }

    public string PositionId { get; }
    public string ExecutionSessionId { get; }
    public string BrokerExecutionId { get; }
    public string TradingPlanId { get; }
    public string RiskAssessmentId { get; }
    public string TenantId { get; }
    public string BrokerAccountId { get; }
    public string Symbol { get; }
    public BrokerOrderSide Direction { get; }
    public DateTimeOffset OpenedAtUtc { get; }
    public string Source { get; }
    public string ReconciliationState { get; }
    private static string Required(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim(); }
    private static DateTimeOffset EnsureUtc(DateTimeOffset value) { if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC."); return value; }
}

public interface IBrokerPositionOwnershipStore
{
    Task<BrokerPositionOwnership?> GetAsync(string positionId, string tenantId, CancellationToken cancellationToken = default);
    Task SaveAsync(BrokerPositionOwnership ownership, CancellationToken cancellationToken = default);
}

public sealed record CriticalAuditEntry
{
    public CriticalAuditEntry(string operation, string outcome, string tenantId, string? brokerId, string? accountId, string? executionId, string? actorId, DateTimeOffset timestampUtc, IReadOnlyDictionary<string, string> metadata)
    {
        Operation = Required(operation); Outcome = Required(outcome); TenantId = Required(tenantId); BrokerId = Optional(brokerId); AccountId = Optional(accountId); ExecutionId = Optional(executionId); ActorId = Optional(actorId); TimestampUtc = EnsureUtc(timestampUtc);
        Metadata = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata ?? throw new ArgumentNullException(nameof(metadata)), StringComparer.Ordinal).OrderBy(item => item.Key, StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
    }

    public string Operation { get; }
    public string Outcome { get; }
    public string TenantId { get; }
    public string? BrokerId { get; }
    public string? AccountId { get; }
    public string? ExecutionId { get; }
    public string? ActorId { get; }
    public DateTimeOffset TimestampUtc { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    private static string Required(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim(); }
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTimeOffset EnsureUtc(DateTimeOffset value) { if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC."); return value; }
}

public interface ICriticalAuditWriter
{
    Task WriteAsync(CriticalAuditEntry entry, CancellationToken cancellationToken = default);
}

public sealed record OperatorActionRequest
{
    public OperatorActionRequest(string actionId, OperatorActionType action, string tenantId, string actorId, string requiredPermission, string target, string reason)
    {
        ActionId = Required(actionId); Action = action; TenantId = Required(tenantId); ActorId = Required(actorId); RequiredPermission = Required(requiredPermission); Target = Required(target); Reason = Required(reason);
    }

    public string ActionId { get; }
    public OperatorActionType Action { get; }
    public string TenantId { get; }
    public string ActorId { get; }
    public string RequiredPermission { get; }
    public string Target { get; }
    public string Reason { get; }
    private static string Required(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim(); }
}

public sealed record OperatorActionResult
{
    public OperatorActionResult(string actionId, bool applied, string code, string message, DateTimeOffset appliedAtUtc)
    {
        ActionId = Required(actionId); Applied = applied; Code = Required(code); Message = Required(message); AppliedAtUtc = EnsureUtc(appliedAtUtc);
    }

    public string ActionId { get; }
    public bool Applied { get; }
    public string Code { get; }
    public string Message { get; }
    public DateTimeOffset AppliedAtUtc { get; }
    private static string Required(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim(); }
    private static DateTimeOffset EnsureUtc(DateTimeOffset value) { if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC."); return value; }
}

public interface IOperatorActionService
{
    Task<OperatorActionResult> ApplyAsync(OperatorActionRequest request, CancellationToken cancellationToken = default);
}

public sealed record LiveSafetyReadinessSnapshot(
    bool Persistence,
    bool Idempotency,
    bool ExecutionLock,
    bool Reconciliation,
    bool Broker,
    bool KillSwitch,
    bool Recovery,
    bool Orphans,
    bool Outbox,
    bool Identity)
{
    public bool ExecutionBlocked => !Persistence || !Idempotency || !ExecutionLock || !Reconciliation || !Broker || !KillSwitch || !Recovery || !Orphans || !Outbox || !Identity;
}

public interface ILiveSafetyReadiness
{
    LiveSafetyReadinessSnapshot Snapshot { get; }
}
