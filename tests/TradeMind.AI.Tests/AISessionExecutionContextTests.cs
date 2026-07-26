using TradeMind.AI.Abstractions;
using TradeMind.AI.Application;

namespace TradeMind.AI.Tests;

public sealed class AISessionExecutionContextTests
{
    [Fact]
    public void AISessionFactory_ShouldGenerateSessionIdWhenAbsent()
    {
        var session = CreateFactory().Create(ValidRequest() with { SessionId = null });

        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
    }

    [Fact]
    public void AISessionFactory_ShouldPreserveProvidedSessionId()
    {
        var session = CreateFactory().Create(ValidRequest() with { SessionId = "session-1" });

        Assert.Equal("session-1", session.SessionId);
    }

    [Fact]
    public void AISessionFactory_ShouldGenerateCorrelationIdWhenAbsent()
    {
        var session = CreateFactory().Create(ValidRequest() with { CorrelationId = null });

        Assert.False(string.IsNullOrWhiteSpace(session.CorrelationId));
    }

    [Fact]
    public void AISessionFactory_ShouldPreserveProvidedCorrelationId()
    {
        var session = CreateFactory().Create(ValidRequest() with { CorrelationId = "corr-1" });

        Assert.Equal("corr-1", session.CorrelationId);
    }

    [Fact]
    public void AISessionFactory_ShouldUseUtcDateFromTimeProvider()
    {
        var now = new DateTimeOffset(2026, 7, 18, 12, 30, 0, TimeSpan.Zero);
        var session = CreateFactory(now).Create(ValidRequest());

        Assert.Equal(now, session.CreatedAtUtc);
    }

    [Fact]
    public void AISession_ShouldRejectEmptyScenario()
    {
        Assert.Throws<ArgumentException>(() =>
            new AISession("session", null, "corr", null, null, null, " ", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AIExecutionContext_ShouldMoveFromCreatedToRunning()
    {
        var context = CreateContext();

        context.Start();

        Assert.Equal(AIExecutionState.Running, context.State);
    }

    [Fact]
    public void AIExecutionContext_ShouldMoveFromRunningToCompleted()
    {
        var context = CreateRunningContext();
        context.SetFinalResponse(Response(context));

        context.Complete(context.Metrics.StartedAtUtc.AddSeconds(1));

        Assert.Equal(AIExecutionState.Completed, context.State);
    }

    [Fact]
    public void AIExecutionContext_ShouldMoveFromRunningToFailed()
    {
        var context = CreateRunningContext();

        context.Fail(Error(), context.Metrics.StartedAtUtc.AddSeconds(1));

        Assert.Equal(AIExecutionState.Failed, context.State);
    }

    [Fact]
    public void AIExecutionContext_ShouldMoveFromRunningToCancelled()
    {
        var context = CreateRunningContext();

        context.Cancel(context.Metrics.StartedAtUtc.AddSeconds(1));

        Assert.Equal(AIExecutionState.Cancelled, context.State);
    }

    [Fact]
    public void AIExecutionContext_ShouldRejectCompletedToRunning()
    {
        var context = CreateRunningContext();
        context.SetFinalResponse(Response(context));
        context.Complete(context.Metrics.StartedAtUtc.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(context.Start);
    }

    [Fact]
    public void AIExecutionContext_ShouldRejectFailedToCompleted()
    {
        var context = CreateRunningContext();
        context.Fail(Error(), context.Metrics.StartedAtUtc.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(() =>
            context.Complete(context.Metrics.StartedAtUtc.AddSeconds(2)));
    }

    [Fact]
    public void AIExecutionContext_ShouldRejectCompletingTwice()
    {
        var context = CreateRunningContext();
        context.SetFinalResponse(Response(context));
        context.Complete(context.Metrics.StartedAtUtc.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(() =>
            context.Complete(context.Metrics.StartedAtUtc.AddSeconds(2)));
    }

    [Fact]
    public void AIExecutionMetrics_ShouldCalculateTotalDuration()
    {
        var metrics = new AIExecutionMetrics(DateTimeOffset.Parse("2026-07-18T10:00:00Z"));

        metrics.Complete(DateTimeOffset.Parse("2026-07-18T10:00:03Z"));

        Assert.Equal(TimeSpan.FromSeconds(3), metrics.TotalDuration);
    }

    [Fact]
    public void AIExecutionMetrics_ShouldRecordProviderDuration()
    {
        var metrics = new AIExecutionMetrics(DateTimeOffset.UtcNow);

        metrics.RecordProviderDuration(TimeSpan.FromMilliseconds(25));

        Assert.Equal(TimeSpan.FromMilliseconds(25), metrics.ProviderDuration);
    }

    [Fact]
    public void AIExecutionMetrics_ShouldRejectNegativeTokens()
    {
        var metrics = new AIExecutionMetrics(DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            metrics.RecordTokens(-1, null, null));
    }

    [Fact]
    public void AIExecutionMetrics_ShouldCalculateTotalTokens()
    {
        var metrics = new AIExecutionMetrics(DateTimeOffset.UtcNow);

        metrics.RecordTokens(10, 5, null);

        Assert.Equal(15, metrics.TotalTokens);
    }

    [Fact]
    public void AIExecutionMetrics_ShouldLeaveAbsentTokensNull()
    {
        var metrics = new AIExecutionMetrics(DateTimeOffset.UtcNow);

        metrics.RecordTokens(null, null, null);

        Assert.Null(metrics.InputTokens);
        Assert.Null(metrics.OutputTokens);
        Assert.Null(metrics.TotalTokens);
    }

    [Fact]
    public void AIExecutionMetrics_ShouldRejectInconsistentTotalTokens()
    {
        var metrics = new AIExecutionMetrics(DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() =>
            metrics.RecordTokens(10, 5, 20));
    }

    [Fact]
    public void AIExecutionContext_ShouldRecordStepsInOrder()
    {
        var context = CreateRunningContext();

        context.MarkStepStarted("first");
        context.MarkStepCompleted("first");
        context.MarkStepStarted("second");
        context.MarkStepCompleted("second");

        Assert.Equal(["first", "second"], context.ExecutedSteps);
    }

    [Fact]
    public void AIExecutionContext_ShouldRejectTwoFinalResponses()
    {
        var context = CreateRunningContext();
        context.SetFinalResponse(Response(context));

        Assert.Throws<InvalidOperationException>(() =>
            context.SetFinalResponse(Response(context)));
    }

    [Fact]
    public void AIExecutionContext_ShouldRecordSafeError()
    {
        var context = CreateRunningContext();
        var error = new AIExecutionError(
            "AI_PROVIDER_FAILED",
            "Provider execution failed.",
            DateTimeOffset.UtcNow,
            AIOrchestrationStepNames.ProviderExecution,
            "Fake",
            exceptionType: "InvalidOperationException");

        context.Fail(error, DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.Equal(error, context.Error);
        var recordedError = Assert.IsType<AIExecutionError>(context.Error);
        Assert.DoesNotContain("stack", recordedError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AIExecutionContext_ShouldRejectServicesInItems()
    {
        var context = CreateRunningContext();

        Assert.Throws<ArgumentException>(() =>
            context.SetItem(AIExecutionContextItemKey.Create("service"), new ServiceProviderMarker()));
    }

    [Fact]
    public void AIExecutionContext_ShouldKeepSessionIdentifiers()
    {
        var context = CreateContext();

        Assert.Equal("session-1", context.Session.SessionId);
        Assert.Equal("corr-1", context.Session.CorrelationId);
        Assert.Equal("TradingCoach", context.Session.Scenario);
    }

    [Fact]
    public void AIExecutionError_ShouldCopyMetadataAsReadOnlyContract()
    {
        var metadata = new Dictionary<string, string> { ["safe"] = "value" };
        var error = new AIExecutionError(
            "AI_FAILED",
            "Safe message.",
            DateTimeOffset.UtcNow,
            metadata: metadata);

        metadata["safe"] = "changed";

        Assert.Equal("value", error.Metadata["safe"]);
    }

    private static AISessionFactory CreateFactory(DateTimeOffset? now = null)
    {
        return new AISessionFactory(new FixedTimeProvider(now ?? DateTimeOffset.UtcNow));
    }

    private static AIExecutionContext CreateRunningContext()
    {
        var context = CreateContext();
        context.Start();
        return context;
    }

    private static AIExecutionContext CreateContext()
    {
        var request = ValidRequest();
        var session = new AISession(
            "session-1",
            "conversation-1",
            "corr-1",
            "tenant-1",
            "user-1",
            "agent-1",
            "TradingCoach",
            DateTimeOffset.UtcNow);

        return new AIExecutionContext(session, request, DateTimeOffset.UtcNow);
    }

    private static AIOrchestrationRequest ValidRequest()
    {
        return new AIOrchestrationRequest(
            "system",
            "message",
            "TradingCoach",
            correlationId: "corr-1")
        {
            SessionId = "session-1",
            ConversationId = "conversation-1",
            Identity = new AIIdentityContext("tenant-1", "user-1", "agent-1")
        };
    }

    private static AIExecutionError Error()
    {
        return new AIExecutionError("AI_FAILED", "Safe failure.", DateTimeOffset.UtcNow);
    }

    private static AIOrchestrationResponse Response(AIExecutionContext context)
    {
        return new AIOrchestrationResponse(
            context.Session.SessionId,
            context.Session.ConversationId,
            context.Session.CorrelationId,
            context.Session.Scenario,
            "Fake",
            "fake-model",
            "content",
            new ChatUsage(1, 2, 3),
            TimeSpan.Zero,
            null,
            context.ExecutedSteps,
            DateTimeOffset.UtcNow,
            context.State);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class ServiceProviderMarker : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
