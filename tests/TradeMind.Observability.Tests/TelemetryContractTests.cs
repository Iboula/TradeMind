using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using TradeMind.Observability.Abstractions;
using TradeMind.Observability.Activities;
using TradeMind.Observability.Context;
using TradeMind.Observability.Health;
using TradeMind.Observability.Logging;
using TradeMind.Observability.Metrics;
using TradeMind.Observability.OpenTelemetry;
using TradeMind.Observability.Snapshots;

namespace TradeMind.Observability.Tests;

public sealed class TelemetryContractTests
{
    [Fact]
    public void Context_is_immutable_and_defensively_validated()
    {
        var context = TelemetryContext.System("Test", "TradeMind.Tests", "1");
        Assert.Equal("system", context.CorrelationId);
        Assert.Null(context.TenantId);
        Assert.Throws<ArgumentException>(() => new TelemetryContext(new string('x', 129), null, null, null, null, null, null, null, null, null, null, null, null, null, "core", "v1", "test", "service", "1"));
        Assert.Throws<ArgumentException>(() => new TelemetryContext("corr", new string('x', 257), null, null, null, null, null, null, null, null, null, null, null, null, "core", "v1", "test", "service", "1"));
    }

    [Fact]
    public void Context_accessor_flows_and_restores_across_async_scope()
    {
        var accessor = new TelemetryContextAccessor();
        var first = TelemetryContext.System("Test");
        var second = first with { CorrelationId = "nested" };
        using (accessor.Push(first))
        {
            Assert.Equal(first, accessor.Current);
            using (accessor.Push(second)) Assert.Equal(second, accessor.Current);
            Assert.Equal(first, accessor.Current);
        }
        Assert.Equal("system", accessor.Current.CorrelationId);
    }

    [Fact]
    public void Sanitizer_rejects_credentials_and_bounds_values()
    {
        Assert.False(ActivityTagSanitizer.IsAllowed("trademind.authorization.header"));
        Assert.False(ActivityTagSanitizer.IsAllowed("request.body"));
        Assert.True(ActivityTagSanitizer.IsAllowed(TelemetryTagNames.CorrelationId));
        Assert.Equal(256, ActivityTagSanitizer.Sanitize(new string('x', 400))!.Length);
    }

    [Fact]
    public void Semantic_names_are_stable()
    {
        Assert.Equal("TradeMind.Telemetry", TradeMindMeter.Name);
        Assert.Contains("TradeMind.Api", TelemetryActivityNames.All);
        Assert.Equal("trademind.api.request.duration", TelemetryMetricNames.ApiRequestDuration);
        Assert.Throws<ArgumentException>(() => new TelemetryOperation("", "Api", TelemetryStage.Api));
        Assert.Throws<ArgumentException>(() => new TelemetryOperation("operation", "", TelemetryStage.Api));
        Assert.Throws<ArgumentException>(() => new TelemetryOperation("operation", "Api", TelemetryStage.Api, new string('x', 65)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetryOperation("operation", "Api", TelemetryStage.Api, artifactCount: -1));
        Assert.Throws<ArgumentException>(() => new TelemetryLink("", null));
        Assert.Throws<ArgumentException>(() => new TelemetryLink("trace", new string('x', 33)));
        Assert.Throws<ArgumentException>(() => new TelemetryLink("trace", relationship: new string('x', 65)));
    }

    [Fact]
    public void Cardinality_guard_rejects_identity_dimensions()
    {
        Assert.Throws<InvalidOperationException>(() => MetricCardinalityGuard.ValidateDimensions(
            new Dictionary<string, string?> { ["tenant_id"] = "tenant-1" }));
        Assert.Throws<InvalidOperationException>(() => MetricCardinalityGuard.ValidateDimensions(
            new Dictionary<string, string?> { ["module"] = new string('x', 65) }));
        MetricCardinalityGuard.ValidateDimensions(new Dictionary<string, string?> { ["module"] = "Api", ["outcome"] = "Succeeded" });
    }

    [Fact]
    public void Activity_listener_observes_parent_child_and_safe_context_tags()
    {
        var observed = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => TelemetryActivityNames.All.Contains(source.Name, StringComparer.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = observed.Add
        };
        ActivitySource.AddActivityListener(listener);
        var telemetry = new TradeMindTelemetry();
        var context = TelemetryContext.System("Test") with { CorrelationId = "corr-1", TenantId = "tenant-1" };
        using (var activity = telemetry.StartActivity(new TelemetryOperation("TradeMind.Test.Operation", "Api", TelemetryStage.Api), context))
        {
            Assert.True(activity.IsRecording);
            Assert.NotNull(activity.TraceId);
            activity.SetOutcome(TelemetryOutcome.Succeeded);
        }
        Assert.Single(observed);
        Assert.Equal("TradeMind.Api", observed[0].Source.Name);
        Assert.Equal("corr-1", observed[0].GetTagItem(TelemetryTagNames.CorrelationId));
        Assert.Equal("tenant-1", observed[0].GetTagItem(TelemetryTagNames.TenantId));
    }

    [Fact]
    public void Activity_exception_sets_error_without_serializing_request()
    {
        Activity? observed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TelemetryActivityNames.Api,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => observed = activity
        };
        ActivitySource.AddActivityListener(listener);
        using (var activity = new TradeMindTelemetry().StartActivity(new TelemetryOperation("TradeMind.Test.Failure", "Api", TelemetryStage.Api)))
            activity.RecordException(new InvalidOperationException("secret request payload"));
        Assert.NotNull(observed);
        Assert.Equal(ActivityStatusCode.Error, observed!.Status);
        Assert.DoesNotContain(observed.Tags, tag => tag.Value?.ToString()?.Contains("secret request payload", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Activity_link_is_supported_for_async_continuation()
    {
        Activity? observed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TelemetryActivityNames.Outbox,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => observed = activity
        };
        ActivitySource.AddActivityListener(listener);
        var traceId = ActivityTraceId.CreateRandom().ToString();
        using var activity = new TradeMindTelemetry().StartActivity(
            new TelemetryOperation("TradeMind.Outbox.Process", "Outbox", TelemetryStage.Outbox),
            links: [new TelemetryLink(traceId)]);
        Assert.NotNull(observed);
        Assert.Single(observed!.Links);
    }

    [Fact]
    public void Metrics_use_bounded_dimensions_and_record_values()
    {
        var values = new List<long>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == TradeMindMeter.Name)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => values.Add(value));
        listener.Start();
        var metrics = new TradeMindMetrics();
        metrics.IncrementCounter(TelemetryMetricNames.ApiRequests, 1, new MetricDimensions(Module: "Api", Outcome: "Succeeded"));
        listener.RecordObservableInstruments();
        Assert.Contains(1, values);
    }

    [Fact]
    public void Logging_scope_contains_safe_correlation_fields()
    {
        var scopes = new List<IReadOnlyDictionary<string, object?>>();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new CapturingLoggerProvider(scopes)));
        var context = TelemetryContext.System("Test") with
        {
            CorrelationId = "corr-1",
            ExecutionSessionId = "session-1",
            TenantId = "tenant-1",
            ActorId = "actor-1"
        };

        var accessor = new TelemetryContextAccessor();
        using (accessor.Push(context))
        using (new TradeMindLogEnricher(accessor).BeginScope(loggerFactory.CreateLogger("test")))
        {
        }

        var scope = Assert.Single(scopes);
        Assert.Equal("corr-1", scope[TelemetryTagNames.CorrelationId]);
        Assert.Equal("session-1", scope[TelemetryTagNames.ExecutionSessionId]);
        Assert.Equal("tenant-1", scope[TelemetryTagNames.TenantId]);
        Assert.DoesNotContain(scope.Keys, key => key.Contains("authorization", StringComparison.OrdinalIgnoreCase));
        using (LoggingScopeFactory.BeginScope(new NullReturningLogger(), context))
        {
        }
    }

    [Fact]
    public void Metrics_support_all_declared_instruments_and_reject_unknown_names()
    {
        var metrics = new TradeMindMetrics();
        var dimensions = new MetricDimensions(Module: "Test", Operation: "Operation", Stage: "Api", Outcome: "Succeeded");
        foreach (var name in new[]
        {
            TelemetryMetricNames.ApiRequests, TelemetryMetricNames.ApiErrors, TelemetryMetricNames.AuthenticationAttempts,
            TelemetryMetricNames.AuthenticationFailures, TelemetryMetricNames.AuthorizationDenials, TelemetryMetricNames.RateLimitRejections,
            TelemetryMetricNames.ExecutionSessionsStarted, TelemetryMetricNames.ExecutionSessionsCompleted, TelemetryMetricNames.ExecutionSessionsFailed,
            TelemetryMetricNames.ExecutionSessionsCancelled, TelemetryMetricNames.ExecutionSessionsReplayRequests, TelemetryMetricNames.PipelineOperations,
            TelemetryMetricNames.PipelineFailures, TelemetryMetricNames.PaperTradingSimulations, TelemetryMetricNames.PaperTradingFailures,
            TelemetryMetricNames.OutboxMessagesCreated, TelemetryMetricNames.OutboxMessagesProcessed, TelemetryMetricNames.OutboxFailures,
            TelemetryMetricNames.IdempotencyHits, TelemetryMetricNames.IdempotencyConflicts,
            TelemetryMetricNames.BrokerIdempotencyConflicts, TelemetryMetricNames.BrokerOrphanPositionsDetected,
            TelemetryMetricNames.BrokerExecutionBlocked, TelemetryMetricNames.BrokerCleanupFailures
        })
            metrics.IncrementCounter(name, 1, dimensions);
        foreach (var name in new[]
        {
            TelemetryMetricNames.ApiRequestDuration, TelemetryMetricNames.PipelineOperationDuration,
            TelemetryMetricNames.ExecutionSessionDuration, TelemetryMetricNames.PaperTradingDuration,
            TelemetryMetricNames.ReplayManifestDuration, TelemetryMetricNames.DatabaseOperationDuration,
            TelemetryMetricNames.OutboxProcessingDuration
        })
            metrics.RecordDuration(name, TimeSpan.FromMilliseconds(1), dimensions);
        metrics.SetActiveExecutionSessions(-1);
        metrics.SetActiveExecutionSessions(100001);
        Assert.Throws<ArgumentException>(() => metrics.IncrementCounter("unknown", 1, dimensions));
        Assert.Throws<ArgumentException>(() => metrics.RecordDuration("unknown", TimeSpan.Zero, dimensions));
    }

    [Fact]
    public void Telemetry_no_listener_path_is_safe_for_every_stage()
    {
        var telemetry = new TradeMindTelemetry();
        foreach (var stage in Enum.GetValues<TelemetryStage>())
        {
            using var activity = telemetry.StartActivity(new TelemetryOperation($"TradeMind.Test.{stage}", "Test", stage));
            activity.SetTag(TelemetryTagNames.Operation, "test");
            activity.SetOutcome(TelemetryOutcome.Succeeded);
            activity.RecordException(new InvalidOperationException("not emitted"));
            Assert.False(activity.IsRecording);
            Assert.Null(activity.TraceId);
            Assert.Null(activity.SpanId);
        }
        Assert.Equal(TelemetryActivityNames.All.Count, TradeMindActivitySource.All.Count);
    }

    [Fact]
    public void Operation_classifier_uses_stable_module_names()
    {
        var classifications = new[]
        {
            TelemetryOperationClassifier.Classify(typeof(TradeMind.AI.Context.Application.ContextFixture)),
            TelemetryOperationClassifier.Classify(typeof(TradeMind.AI.ExpertAgents.Dispatch.Application.DispatchFixture)),
            TelemetryOperationClassifier.Classify(typeof(TradeMind.AI.ExpertAgents.Consensus.Application.ConsensusFixture)),
            TelemetryOperationClassifier.Classify(typeof(TradeMind.AI.ExpertAgents.Application.ExpertFixture)),
            TelemetryOperationClassifier.Classify(typeof(TradeMind.AI.TradingDecisions.Application.DecisionFixture)),
            TelemetryOperationClassifier.Classify(typeof(TradeMind.AI.RiskEngine.Application.RiskFixture)),
            TelemetryOperationClassifier.Classify(typeof(TradeMind.AI.TradingPlans.Application.PlanFixture)),
            TelemetryOperationClassifier.Classify(typeof(TradeMind.AI.TradingWorkspace.Application.WorkspaceFixture)),
            TelemetryOperationClassifier.Classify(typeof(TradeMind.AI.TradingAssistant.AssistantFixture)),
            TelemetryOperationClassifier.Classify(typeof(TradeMind.AI.PaperTrading.Application.PaperTradingFixture)),
            TelemetryOperationClassifier.Classify(typeof(TradeMind.ExecutionSessions.Application.ExecutionSessionFixture)),
            TelemetryOperationClassifier.Classify(typeof(ApplicationFixture))
        };
        Assert.Equal("MarketContext", classifications[0].Module);
        Assert.Equal("ExpertDispatch", classifications[1].Module);
        Assert.Equal("Consensus", classifications[2].Module);
        Assert.Equal("ExpertAnalysis", classifications[3].Module);
        Assert.Equal("TradingDecision", classifications[4].Module);
        Assert.Equal("Risk", classifications[5].Module);
        Assert.Equal("TradingPlan", classifications[6].Module);
        Assert.Equal("TradingWorkspace", classifications[7].Module);
        Assert.Equal("TradingAssistant", classifications[8].Module);
        Assert.Equal("PaperTrading", classifications[9].Module);
        Assert.Equal("ExecutionSessions", classifications[10].Module);
        Assert.Equal("Application", classifications[11].Module);
    }

    [Fact]
    public void Context_enricher_copies_activity_identifiers()
    {
        using var activity = new Activity("context-test").Start();
        var context = TelemetryContext.System("Test");
        var enriched = TelemetryContextEnricher.WithActivity(context, activity);
        Assert.Equal(activity.TraceId.ToString(), enriched.TraceId);
        Assert.Equal(activity.SpanId.ToString(), enriched.SpanId);
    }

    [Fact]
    public void Exporter_registration_configures_protocol_and_headers()
    {
        var exporter = new OtlpExporterOptions();
        ExporterRegistration.Configure(exporter, new OtlpOptions
        {
            Endpoint = "https://collector.example/v1/traces",
            Protocol = "HttpProtobuf",
            Headers = new Dictionary<string, string> { ["x-tenant"] = "test", ["x-purpose"] = "diagnostics" }
        });
        Assert.Equal(new Uri("https://collector.example/v1/traces"), exporter.Endpoint);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, exporter.Protocol);
        Assert.Contains("x-tenant=test", exporter.Headers, StringComparison.Ordinal);
        Assert.Contains("x-purpose=diagnostics", exporter.Headers, StringComparison.Ordinal);
    }

    [Fact]
    public void Exporter_registration_defaults_to_grpc_without_headers()
    {
        var exporter = new OtlpExporterOptions();
        ExporterRegistration.Configure(exporter, new OtlpOptions { Endpoint = "http://collector.example:4317", Protocol = "Grpc" });
        Assert.Equal(OtlpExportProtocol.Grpc, exporter.Protocol);
        Assert.Equal(new Uri("http://collector.example:4317"), exporter.Endpoint);
    }

    [Fact]
    public void Resource_builder_contains_service_identity()
    {
        var resource = ResourceBuilderFactory.Create(new OpenTelemetryOptions
        {
            ServiceName = "TradeMind.Tests",
            ServiceNamespace = "TradeMind",
            ServiceVersion = "test",
            Environment = "Test"
        }).Build();
        Assert.Contains(resource.Attributes, attribute => attribute.Key == "service.name" && (string)attribute.Value == "TradeMind.Tests");
        Assert.Contains(resource.Attributes, attribute => attribute.Key == "deployment.environment.name" && (string)attribute.Value == "Test");
    }

    [Fact]
    public async Task Observability_health_check_is_healthy_when_disabled()
    {
        var check = new ObservabilityHealthCheck(Options.Create(new OpenTelemetryOptions { Enabled = false }));
        var result = await check.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());
        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy, result.Status);
        Assert.Contains("disabled", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Disabled_observability_registration_preserves_environment_and_options()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TradeMind:Observability:Enabled"] = "false",
                ["TradeMind:Observability:Environment"] = "Unknown"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddTradeMindObservability(configuration, "Test");
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenTelemetryOptions>>().Value;
        Assert.False(options.Enabled);
        Assert.Equal("Test", options.Environment);
    }

    [Fact]
    public void Enabled_observability_registration_accepts_optional_exporters()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TradeMind:Observability:Enabled"] = "true",
                ["TradeMind:Observability:Tracing:ConsoleExporter:Enabled"] = "true",
                ["TradeMind:Observability:Tracing:Otlp:Enabled"] = "true",
                ["TradeMind:Observability:Tracing:Otlp:Endpoint"] = "http://collector.example:4317",
                ["TradeMind:Observability:Tracing:Otlp:Protocol"] = "Grpc",
                ["TradeMind:Observability:Metrics:Otlp:Enabled"] = "true",
                ["TradeMind:Observability:Metrics:Otlp:Endpoint"] = "http://collector.example:4317",
                ["TradeMind:Observability:Metrics:Otlp:Protocol"] = "Grpc"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddTradeMindObservability(configuration, "Test");
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITradeMindTelemetry));
    }

    [Fact]
    public void Enabled_observability_registration_can_disable_tracing_and_metrics()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TradeMind:Observability:Enabled"] = "true",
                ["TradeMind:Observability:Tracing:Enabled"] = "false",
                ["TradeMind:Observability:Metrics:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddTradeMindObservability(configuration, "Test");
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITradeMindTelemetry));
    }

    [Fact]
    public void Options_validator_rejects_invalid_shapes_and_accepts_ratio_sampler()
    {
        var validator = new OpenTelemetryOptionsValidator();
        Assert.False(validator.Validate(null, new OpenTelemetryOptions { ServiceName = "", Environment = "Test" }).Succeeded);
        Assert.False(validator.Validate(null, new OpenTelemetryOptions { ServiceNamespace = "", Environment = "Test" }).Succeeded);
        Assert.False(validator.Validate(null, new OpenTelemetryOptions { Environment = "" }).Succeeded);
        Assert.False(validator.Validate(null, new OpenTelemetryOptions { Tracing = null!, Environment = "Test" }).Succeeded);
        Assert.False(validator.Validate(null, new OpenTelemetryOptions
        {
            Environment = "Test",
            Tracing = new TracingOptions { Sampling = new SamplingOptions { Strategy = "Unknown" } }
        }).Succeeded);
        Assert.False(validator.Validate(null, new OpenTelemetryOptions
        {
            Environment = "Test",
            Metrics = new MetricsOptions { Prometheus = null! }
        }).Succeeded);
        Assert.False(validator.Validate(null, new OpenTelemetryOptions
        {
            Environment = "Test",
            Tracing = new TracingOptions { Otlp = null! }
        }).Succeeded);
        Assert.False(validator.Validate(null, new OpenTelemetryOptions
        {
            Environment = "Test",
            Tracing = new TracingOptions { Otlp = new OtlpOptions { Endpoint = "ftp://collector" } }
        }).Succeeded);
        Assert.False(validator.Validate(null, new OpenTelemetryOptions
        {
            Environment = "Test",
            Tracing = new TracingOptions { Otlp = new OtlpOptions { Enabled = true, Endpoint = "not-uri" } },
            Metrics = new MetricsOptions { Otlp = new OtlpOptions { Enabled = true, Endpoint = "also-not-uri" } }
        }).Succeeded);
        Assert.False(validator.Validate(null, new OpenTelemetryOptions
        {
            Environment = "Test",
            Tracing = new TracingOptions { Otlp = new OtlpOptions { Headers = new Dictionary<string, string> { [new string('k', 65)] = "value" } } }
        }).Succeeded);
        Assert.True(validator.Validate(null, new OpenTelemetryOptions
        {
            Environment = "Test",
            Tracing = new TracingOptions { Sampling = new SamplingOptions { Strategy = "TraceIdRatio", Ratio = 0.25 } }
        }).Succeeded);
    }

    [Fact]
    public void Activity_outcomes_map_to_expected_statuses()
    {
        var observed = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TelemetryActivityNames.Api,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = observed.Add
        };
        ActivitySource.AddActivityListener(listener);
        foreach (var outcome in Enum.GetValues<TelemetryOutcome>())
        {
            using var activity = new TradeMindTelemetry().StartActivity(new TelemetryOperation("TradeMind.Test.Outcome", "Api", TelemetryStage.Api));
            activity.SetTag("test", "ignored");
            activity.SetTag(TelemetryTagNames.ArtifactCount, 1);
            activity.SetOutcome(outcome);
        }
        Assert.Equal(5, observed.Count);
        Assert.Contains(observed, activity => activity.Status == ActivityStatusCode.Error);
        Assert.Contains(observed, activity => activity.Status == ActivityStatusCode.Ok);
    }

    [Fact]
    public void Activity_wrapper_exposes_identifiers_and_rejects_unsafe_tags()
    {
        Activity? observed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TelemetryActivityNames.Api,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => observed = activity
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = new TradeMindTelemetry().StartActivity(new TelemetryOperation("TradeMind.Test.Wrapper", "Api", TelemetryStage.Api));
        Assert.True(activity.IsRecording);
        Assert.NotNull(activity.TraceId);
        Assert.NotNull(activity.SpanId);
        activity.SetTag("authorization.header", "blocked");
        activity.SetTag(TelemetryTagNames.Operation, (string?)null);
        activity.SetTag(TelemetryTagNames.ArtifactCount, (long?)null);
        Assert.NotNull(observed);
        Assert.Null(observed!.GetTagItem("authorization.header"));
    }

    [Fact]
    public async Task Telemetry_behavior_propagates_cancellation_and_records_cancelled_outcome()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var behavior = new TelemetryBehavior<ApplicationFixture, string>(
            new TradeMindTelemetry(),
            new TradeMindMetrics(),
            new TelemetryContextAccessor());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => behavior.Handle(
            new ApplicationFixture(),
            () => Task.FromCanceled<string>(cancellation.Token),
            cancellation.Token));
    }

    [Fact]
    public void Snapshot_fingerprint_is_deterministic_and_order_independent()
    {
        var first = CreateSnapshot([new("Risk", TimeSpan.FromSeconds(2), 0, "Succeeded"), new("Context", TimeSpan.FromSeconds(1), 0, "Succeeded")]);
        var second = CreateSnapshot([new("Context", TimeSpan.FromSeconds(1), 0, "Succeeded"), new("Risk", TimeSpan.FromSeconds(2), 0, "Succeeded")]);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void Snapshot_comparison_reports_duration_and_missing_stages()
    {
        var reference = CreateSnapshot([new("Context", TimeSpan.FromSeconds(1), 0, "Succeeded"), new("Risk", TimeSpan.FromSeconds(2), 0, "Succeeded")], TimeSpan.FromSeconds(3), 0);
        var current = CreateSnapshot([new("Context", TimeSpan.FromSeconds(2), 1, "Failed")], TimeSpan.FromSeconds(2), 1);
        var comparison = TelemetrySnapshotComparer.Compare(current, reference);
        Assert.Equal(TimeSpan.FromSeconds(-1), comparison.DurationDelta);
        Assert.Equal(1, comparison.FailureCountDelta);
        Assert.Equal(["Risk"], comparison.MissingStages);
    }

    [Fact]
    public void Snapshot_rejects_negative_duration_and_unknown_schema()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateSnapshot([], TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetrySnapshot("s", "e", "o", "t", DateTimeOffset.UnixEpoch, TimeSpan.Zero, 0, [], 2));
        Assert.Throws<ArgumentException>(() => new TelemetrySnapshot("", "e", "o", "t", DateTimeOffset.UnixEpoch, TimeSpan.Zero, 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetrySnapshot("s", "e", "o", "t", DateTimeOffset.UnixEpoch, TimeSpan.Zero, -1, []));
        Assert.Throws<ArgumentNullException>(() => new TelemetrySnapshot("s", "e", "o", "t", DateTimeOffset.UnixEpoch, TimeSpan.Zero, 0, null!));
        Assert.Throws<ArgumentException>(() => new TelemetrySnapshotStage("", TimeSpan.Zero, 0, "Succeeded"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetrySnapshotStage("stage", TimeSpan.Zero, -1, "Succeeded"));
        Assert.Throws<ArgumentException>(() => new TelemetrySnapshotStage("stage", TimeSpan.Zero, 0, ""));
        var tooManyStages = Enumerable.Range(0, 101).Select(index => new TelemetrySnapshotStage($"stage-{index}", TimeSpan.Zero, 0, "Succeeded"));
        Assert.Throws<ArgumentException>(() => new TelemetrySnapshot("s", "e", "o", "t", DateTimeOffset.UnixEpoch, TimeSpan.Zero, 0, tooManyStages));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Invalid_sampling_ratio_is_rejected(double ratio)
    {
        var result = new OpenTelemetryOptionsValidator().Validate(null, new OpenTelemetryOptions
        {
            Tracing = new TracingOptions { Sampling = new SamplingOptions { Ratio = ratio } }
        });
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Invalid_otlp_protocol_is_rejected()
    {
        var result = new OpenTelemetryOptionsValidator().Validate(null, new OpenTelemetryOptions
        {
            Tracing = new TracingOptions { Otlp = new OtlpOptions { Protocol = "Json" } }
        });
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Invalid_prometheus_path_is_rejected()
    {
        var result = new OpenTelemetryOptionsValidator().Validate(null, new OpenTelemetryOptions
        {
            Metrics = new MetricsOptions { Prometheus = new PrometheusOptions { Endpoint = "metrics" } }
        });
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Oversized_otlp_header_is_rejected()
    {
        var result = new OpenTelemetryOptionsValidator().Validate(null, new OpenTelemetryOptions
        {
            Tracing = new TracingOptions
            {
                Otlp = new OtlpOptions { Headers = new Dictionary<string, string> { ["x-api-key"] = new string('x', 257) } }
            }
        });
        Assert.False(result.Succeeded);
    }

    private static TelemetrySnapshot CreateSnapshot(IEnumerable<TelemetrySnapshotStage> stages, TimeSpan? duration = null, int failureCount = 1) =>
        new("snapshot-1", "session-1", "org-1", "tenant-1", DateTimeOffset.UnixEpoch, duration ?? TimeSpan.FromSeconds(3), failureCount, stages);

    private sealed class CapturingLoggerProvider(List<IReadOnlyDictionary<string, object?>> scopes) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(scopes);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(List<IReadOnlyDictionary<string, object?>> scopes) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
                scopes.Add(values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
            return NoopScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class NullReturningLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
