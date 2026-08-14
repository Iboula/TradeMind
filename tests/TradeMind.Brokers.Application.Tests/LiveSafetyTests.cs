using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application;
using TradeMind.Brokers.Application.LiveSafety;
using TradeMind.Brokers.Domain;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.Application.Tests;

public sealed class LiveSafetyTests
{
    [Fact]
    public async Task Live_is_denied_by_default_and_never_returns_a_single_trade_boolean()
    {
        var decision = await Gate(new LiveSafetyOptions()).EvaluateAsync(CompleteRequest(allowLive: false));
        Assert.Equal(LiveSafetyDecisionStatus.Denied, decision.Status);
        Assert.Contains(decision.Causes, cause => cause.Code == "LIVE_DISABLED");
        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public async Task All_independent_conditions_are_required_for_an_allowed_decision()
    {
        var decision = await Gate(new LiveSafetyOptions { Enabled = true, AllowLive = true }).EvaluateAsync(CompleteRequest(allowLive: true));
        Assert.Equal(LiveSafetyDecisionStatus.Allowed, decision.Status);
        Assert.Empty(decision.Causes);
    }

    [Fact]
    public async Task Confirmation_and_reconciliation_have_distinct_structured_outcomes()
    {
        var confirmationDecision = await Gate(new LiveSafetyOptions { Enabled = true, AllowLive = true }).EvaluateAsync(CompleteGateRequest(dualConfirmationComplete: false));
        var reconciliationDecision = await Gate(new LiveSafetyOptions { Enabled = true, AllowLive = true }).EvaluateAsync(CompleteGateRequest(reconciliationClean: false, hasOrphanPositions: true));
        Assert.Equal(LiveSafetyDecisionStatus.RequiresConfirmation, confirmationDecision.Status);
        Assert.Equal(LiveSafetyDecisionStatus.RequiresReconciliation, reconciliationDecision.Status);
    }

    [Fact]
    public async Task Safety_decision_cause_order_is_deterministic_and_defensive()
    {
        var request = CompleteRequest(false);
        var decision = await Gate(new LiveSafetyOptions()).EvaluateAsync(request);
        Assert.Equal(decision.Causes.OrderBy(item => item.Code, StringComparer.Ordinal).Select(item => item.Code), decision.Causes.Select(item => item.Code));
        var causes = Assert.IsAssignableFrom<IList<LiveSafetyCause>>(decision.Causes);
        Assert.Throws<NotSupportedException>(() => causes.Add(new LiveSafetyCause("X", LiveSafetyCauseCategory.Configuration, "x")));
    }

    [Theory]
    [InlineData("development")]
    [InlineData("test")]
    public async Task Non_production_accounts_are_denied(string environment)
    {
        var decision = await Gate(new LiveSafetyOptions { Enabled = true, AllowLive = true }).EvaluateAsync(CompleteRequest(true, Enum.Parse<BrokerEnvironment>(environment, true)));
        Assert.Equal(LiveSafetyDecisionStatus.Denied, decision.Status);
        Assert.Contains(decision.Causes, cause => cause.Code == "ACCOUNT_NOT_LIVE");
    }

    [Fact]
    public async Task Emergency_stop_is_terminal_until_explicit_reactivation()
    {
        var audit = new CapturingAudit();
        var store = new InMemoryKillSwitchStore(TimeProvider.System, audit);
        var key = new KillSwitchKey(KillSwitchScope.Global, "all");
        await store.EmergencyStopAsync(key, "operator", "incident", CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.EnableAsync(key, "system", "automatic resume", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReactivateEmergencyStoppedAsync(key, "operator", "missing audit", false, CancellationToken.None));
        var reset = await store.ReactivateEmergencyStoppedAsync(key, "operator", "reviewed", true, CancellationToken.None);
        Assert.Equal(KillSwitchState.Disabled, reset.State);
        Assert.Equal(2, audit.Entries.Count);
    }

    [Fact]
    public async Task Quarantine_is_idempotent_and_requires_explicit_release()
    {
        var audit = new CapturingAudit();
        var store = new InMemoryExecutionQuarantineStore(TimeProvider.System, audit);
        var record = new ExecutionQuarantineRecord("execution", "tenant", "broker", "account", "EURUSD", ExecutionQuarantineReason.AmbiguousTimeout, ExecutionQuarantineState.Quarantined, DateTimeOffset.UtcNow, "system", "outcome unknown");
        Assert.Equal(record, await store.QuarantineAsync(record, CancellationToken.None));
        Assert.Equal(record, await store.QuarantineAsync(new ExecutionQuarantineRecord(record.ExecutionId, record.TenantId, record.BrokerId, record.AccountId, record.Instrument, record.Reason, record.State, record.ChangedAtUtc, record.ChangedBy, "different"), CancellationToken.None));
        var released = await store.ReleaseAsync(record.ExecutionId, "operator", "reconciled", CancellationToken.None);
        Assert.Equal(ExecutionQuarantineState.ExplicitlyReleased, released.State);
    }

    [Fact]
    public void Versioned_risk_guards_are_deterministic_and_equal_boundaries_are_allowed()
    {
        var policy = new DefaultRiskGuardPolicy(new VersionedRiskGuardPolicy(7, new RiskGuardLimits(100, 200, 300, 4, 3, 2, 1000, 2000, 5000, 10)));
        var atBoundary = policy.Evaluate(new RiskGuardMeasurement(100, 200, 300, 4, 3, 2, 1000, 2000, 5000, 10));
        var over = policy.Evaluate(new RiskGuardMeasurement(101, 200, 300, 4, 3, 2, 1000, 2000, 5000, 10));
        Assert.True(atBoundary.Approved);
        Assert.False(over.Approved);
        Assert.Equal("MAX_DAILY_LOSS", Assert.Single(over.Rejections).Code);
    }

    [Fact]
    public async Task Recovery_is_read_only_and_detects_unknown_external_positions()
    {
        var audit = new CapturingAudit();
        var reader = new FakeRecoveryReader(new BrokerRecoverySnapshot([], [], [new BrokerRecoveryPosition("position", "tenant", "broker", "account", "EURUSD", null, null, null, "UnknownExternal")], DateTimeOffset.UtcNow));
        var service = new BrokerExecutionRecoveryService(reader, TimeProvider.System, new NoOpMetrics(), audit);
        var plan = await service.RecoverAsync("tenant", "broker", "account", CancellationToken.None);
        Assert.True(plan.RequiresManualReview);
        Assert.Contains(plan.Mismatches, item => item.Type == RecoveryMismatchType.UnknownExternalPosition);
        Assert.True(reader.Reads == 1);
    }

    [Fact]
    public async Task Operator_actions_are_tenant_scoped_and_idempotent()
    {
        var audit = new CapturingAudit();
        var killSwitches = new InMemoryKillSwitchStore(TimeProvider.System, audit);
        var actions = new InMemoryOperatorActionService(killSwitches, TimeProvider.System, audit);
        var request = new OperatorActionRequest("action-1", OperatorActionType.Acknowledge, "tenant-a", "operator", "TradeMind.Brokers.ManageSafety", "tenant=tenant-a", "reviewed");
        Assert.True((await actions.ApplyAsync(request, CancellationToken.None)).Applied);
        Assert.Equal("APPLIED", (await actions.ApplyAsync(request, CancellationToken.None)).Code);
        var crossTenant = await actions.ApplyAsync(new OperatorActionRequest("action-2", request.Action, request.TenantId, request.ActorId, request.RequiredPermission, "tenant=tenant-b", request.Reason), CancellationToken.None);
        Assert.False(crossTenant.Applied);
        Assert.Equal("CROSS_TENANT_TARGET", crossTenant.Code);
    }

    [Fact]
    public async Task Cancellation_is_propagated_before_evaluation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Gate(new LiveSafetyOptions()).EvaluateAsync(CompleteRequest(false), cancellation.Token).AsTask());
    }

    [Fact]
    public void Dependency_injection_validates_production_live_configuration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TradeMind:Brokers:LiveSafety:Enabled"] = "false",
            ["TradeMind:Brokers:LiveSafety:AllowLive"] = "true",
            ["TradeMind:Brokers:LiveSafety:EnvironmentName"] = "Production"
        }).Build();
        var services = new ServiceCollection().AddTradeMindBrokersApplication(configuration).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => { _ = services.GetRequiredService<IOptions<LiveSafetyOptions>>().Value; });
    }

    [Fact]
    public void Application_registrations_do_not_capture_scoped_services()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddTradeMindBrokersApplication(configuration);
        Assert.DoesNotContain(services, descriptor => descriptor.Lifetime == ServiceLifetime.Singleton && descriptor.ServiceType.Name.Contains("Scoped", StringComparison.Ordinal));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Assert.NotNull(provider.GetRequiredService<ILiveTradingSafetyGate>());
    }

    private static LiveTradingSafetyGate Gate(LiveSafetyOptions options) => new(Microsoft.Extensions.Options.Options.Create(options), TimeProvider.System, new NoOpMetrics());

    private static LiveTradingSafetyRequest CompleteRequest(bool allowLive, BrokerEnvironment environment = BrokerEnvironment.Production) => new(
        tenantId: "tenant", brokerId: "broker", accountId: "account", instrument: "EURUSD", executionSessionId: "session",
        allowLive: allowLive, liveSafetyEnabled: true, accountEnvironment: environment, brokerLiveCapable: true,
        accountVerifiedLive: true, tenantAuthorized: true, actorAuthorized: true, riskApproved: true, planExecutable: true,
        workspaceClear: true, reconciliationClean: true, hasOrphanPositions: false, heartbeatFresh: true, brokerHealthy: true,
        idempotencyAvailable: true, distributedLockAvailable: true, operatorConfirmed: true, activationWindowValid: true,
        killSwitchDisabled: true, dualConfirmationComplete: true, drawdownWithinLimit: true, exposureWithinLimit: true,
        configurationEnvironment: "Production");

    private static LiveTradingSafetyRequest CompleteGateRequest(bool reconciliationClean = true, bool hasOrphanPositions = false, bool dualConfirmationComplete = true) => new(
        tenantId: "tenant", brokerId: "broker", accountId: "account", instrument: "EURUSD", executionSessionId: "session",
        allowLive: true, liveSafetyEnabled: true, accountEnvironment: BrokerEnvironment.Production, brokerLiveCapable: true,
        accountVerifiedLive: true, tenantAuthorized: true, actorAuthorized: true, riskApproved: true, planExecutable: true,
        workspaceClear: true, reconciliationClean: reconciliationClean, hasOrphanPositions: hasOrphanPositions, heartbeatFresh: true,
        brokerHealthy: true, idempotencyAvailable: true, distributedLockAvailable: true, operatorConfirmed: true,
        activationWindowValid: true, killSwitchDisabled: true, dualConfirmationComplete: dualConfirmationComplete,
        drawdownWithinLimit: true, exposureWithinLimit: true, configurationEnvironment: "Production");

    private sealed class CapturingAudit : ICriticalAuditWriter
    {
        public List<CriticalAuditEntry> Entries { get; } = [];
        public Task WriteAsync(CriticalAuditEntry entry, CancellationToken cancellationToken = default) { Entries.Add(entry); return Task.CompletedTask; }
    }

    private sealed class FakeRecoveryReader(BrokerRecoverySnapshot snapshot) : IBrokerExecutionRecoveryReader
    {
        public int Reads { get; private set; }
        public Task<BrokerRecoverySnapshot> ReadAsync(string tenantId, string brokerId, string accountId, CancellationToken cancellationToken = default) { Reads++; return Task.FromResult(snapshot); }
    }

    private sealed class NoOpMetrics : ITradeMindMetrics
    {
        public void IncrementCounter(string name, long value, MetricDimensions dimensions) { }
        public void RecordDuration(string name, TimeSpan duration, MetricDimensions dimensions) { }
        public void SetActiveExecutionSessions(long value) { }
    }
}
