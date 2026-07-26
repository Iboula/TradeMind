using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Application;
using TradeMind.AI.ExpertAgents.Domain;

namespace TradeMind.AI.ExpertAgents.Tests;

public sealed class ExpertAgentExecutorTests
{
    [Fact]
    public async Task Executor_ShouldReturnStructuredSuccess()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var request = ExpertAgentTestData.Request(context);
        var executor = ExpertAgentTestData.Executor([new TestExpertAgent(descriptor)]);

        var result = await executor.ExecuteAsync(context, request, CancellationToken.None);

        Assert.Equal(AgentAnalysisStatus.Succeeded, result.Status);
        Assert.Equal(request.AgentRunId, result.AgentRunId);
        Assert.Equal(context.Id, result.MarketContextId);
        Assert.NotEmpty(result.Observations);
        Assert.NotEmpty(result.Evidence);
    }

    [Fact]
    public async Task Executor_ShouldPreservePartialSuccess()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var request = ExpertAgentTestData.Request(context);
        var agent = new TestExpertAgent(descriptor, (marketContext, executionRequest, _) => Task.FromResult(
            ExpertAgentTestData.Result(
                marketContext,
                executionRequest,
                descriptor,
                AgentAnalysisStatus.PartiallySucceeded,
                warnings: [new AgentWarning("PARTIAL", "One optional analysis branch was unavailable.")])));
        var executor = ExpertAgentTestData.Executor([agent]);

        var result = await executor.ExecuteAsync(context, request, CancellationToken.None);

        Assert.Equal(AgentAnalysisStatus.PartiallySucceeded, result.Status);
        Assert.Contains(result.Warnings, warning => warning.Code == "PARTIAL");
    }

    [Fact]
    public async Task Executor_ShouldReturnAgentNotFound()
    {
        var context = ExpertAgentTestData.Context();
        var request = ExpertAgentTestData.Request(context);
        var executor = ExpertAgentTestData.Executor([]);

        var result = await executor.ExecuteAsync(context, request, CancellationToken.None);

        Assert.Equal(AgentAnalysisStatus.Unavailable, result.Status);
        Assert.Contains(result.Errors, error => error.Code == AgentErrorCode.AgentNotFound);
    }

    [Fact]
    public async Task Executor_ShouldReturnDisabledAgentAsUnavailable()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor(activationStatus: AgentActivationStatus.Disabled);
        var executor = ExpertAgentTestData.Executor([new TestExpertAgent(descriptor)]);

        var result = await executor.ExecuteAsync(context, ExpertAgentTestData.Request(context), CancellationToken.None);

        Assert.Equal(AgentAnalysisStatus.Unavailable, result.Status);
        Assert.Contains(result.Errors, error => error.Code == AgentErrorCode.AgentDisabled);
    }

    [Fact]
    public async Task Executor_ShouldResolveExactVersionAndReturnVersionNotFound()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor(version: "2.0.0");
        var executor = ExpertAgentTestData.Executor([new TestExpertAgent(descriptor)]);
        var request = ExpertAgentTestData.Request(
            context,
            selection: AgentVersionSelection.Exact,
            exactVersion: AgentVersion.Parse("1.0.0"));

        var result = await executor.ExecuteAsync(context, request, CancellationToken.None);

        Assert.Equal(AgentErrorCode.AgentVersionNotFound, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task Executor_ShouldReturnUnauthorizedWithoutRunningAgent()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var ran = false;
        var agent = new TestExpertAgent(descriptor, (_, _, _) =>
        {
            ran = true;
            return Task.FromResult<AgentAnalysisResult>(null!);
        });
        var executor = ExpertAgentTestData.Executor([agent], authorization: new DenyAllAuthorizationPolicy());

        var result = await executor.ExecuteAsync(context, ExpertAgentTestData.Request(context), CancellationToken.None);

        Assert.Equal(AgentAnalysisStatus.Unauthorized, result.Status);
        Assert.False(ran);
    }

    [Fact]
    public async Task Executor_ShouldReturnTimedOutAndObserveAgentCompletion()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new TestExpertAgent(descriptor, async (_, _, token) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            catch (OperationCanceledException)
            {
                cancelled.SetResult(true);
                throw;
            }

            throw new InvalidOperationException("Unreachable");
        });
        var executor = ExpertAgentTestData.Executor([agent], new ExpertAgentOptions
        {
            DefaultExecutionTimeout = TimeSpan.FromMilliseconds(30),
            MaximumExecutionTimeout = TimeSpan.FromMilliseconds(100)
        });

        var result = await executor.ExecuteAsync(context, ExpertAgentTestData.Request(context), CancellationToken.None);

        Assert.Equal(AgentAnalysisStatus.TimedOut, result.Status);
        Assert.Contains(result.Errors, error => error.Code == AgentErrorCode.AgentTimeout);
        Assert.True(await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Executor_ShouldPropagateExternalCancellationAndToken()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var observedCancellation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new TestExpertAgent(descriptor, async (_, _, token) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            catch (OperationCanceledException)
            {
                observedCancellation.SetResult(token.IsCancellationRequested);
                throw;
            }

            throw new InvalidOperationException("Unreachable");
        });
        var executor = ExpertAgentTestData.Executor([agent]);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(context, ExpertAgentTestData.Request(context), cancellation.Token));
        Assert.True(await observedCancellation.Task.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Executor_ShouldReturnUnexpectedFailureForUnhandledException()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var agent = new TestExpertAgent(descriptor, (_, _, _) => throw new InvalidOperationException("technical detail"));
        var executor = ExpertAgentTestData.Executor([agent]);

        var result = await executor.ExecuteAsync(context, ExpertAgentTestData.Request(context), CancellationToken.None);

        Assert.Equal(AgentAnalysisStatus.Failed, result.Status);
        Assert.Contains(result.Errors, error => error.Code == AgentErrorCode.UnexpectedAgentFailure);
        Assert.DoesNotContain(result.Errors, error => error.Message.Contains("technical detail", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Executor_ShouldRejectResultWithWrongAgentId()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var wrongDescriptor = ExpertAgentTestData.Descriptor("other-agent");
        var agent = new TestExpertAgent(descriptor, (marketContext, request, _) =>
            Task.FromResult(ExpertAgentTestData.Result(marketContext, request, wrongDescriptor)));
        var executor = ExpertAgentTestData.Executor([agent]);

        var result = await executor.ExecuteAsync(context, ExpertAgentTestData.Request(context), CancellationToken.None);

        Assert.Equal(AgentAnalysisStatus.Failed, result.Status);
        Assert.Contains(result.Errors, error => error.Code == AgentErrorCode.InvalidAgentResult);
    }

    [Fact]
    public async Task Executor_ShouldRejectResultWithWrongAgentRunId()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var request = ExpertAgentTestData.Request(context);
        var agent = new TestExpertAgent(descriptor, (_, _, _) => Task.FromResult(new AgentAnalysisResult(
            new AgentRunId("other-run"),
            descriptor.Id,
            descriptor.Version,
            context.Id,
            AgentAnalysisStatus.Succeeded,
            ExpertAgentTestData.Now,
            ExpertAgentTestData.Now.AddSeconds(1),
            AgentDirectionalBias.Neutral,
            AgentConfidence.FromScore(50, 50, 50))));
        var executor = ExpertAgentTestData.Executor([agent]);

        var result = await executor.ExecuteAsync(context, request, CancellationToken.None);

        Assert.Contains(result.Errors, error => error.Code == AgentErrorCode.InvalidAgentResult);
    }

    [Fact]
    public async Task Executor_ShouldRejectResultWithWrongMarketContextId()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var request = ExpertAgentTestData.Request(context);
        var agent = new TestExpertAgent(descriptor, (_, _, _) => Task.FromResult(new AgentAnalysisResult(
            request.AgentRunId,
            descriptor.Id,
            descriptor.Version,
            MarketContextId.New(),
            AgentAnalysisStatus.Succeeded,
            ExpertAgentTestData.Now,
            ExpertAgentTestData.Now.AddSeconds(1),
            AgentDirectionalBias.Neutral,
            AgentConfidence.FromScore(50, 50, 50))));
        var executor = ExpertAgentTestData.Executor([agent]);

        var result = await executor.ExecuteAsync(context, request, CancellationToken.None);

        Assert.Contains(result.Errors, error => error.Code == AgentErrorCode.InvalidAgentResult);
    }

    [Fact]
    public async Task Executor_ShouldRejectIncoherentTimestamps()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var agent = new TestExpertAgent(descriptor, (marketContext, request, _) => Task.FromResult(
            ExpertAgentTestData.Result(
                marketContext,
                request,
                descriptor,
                startedAtUtc: ExpertAgentTestData.Now.AddSeconds(2),
                completedAtUtc: ExpertAgentTestData.Now)));
        var executor = ExpertAgentTestData.Executor([agent]);

        var result = await executor.ExecuteAsync(context, ExpertAgentTestData.Request(context), CancellationToken.None);

        Assert.Contains(result.Errors, error => error.Code == AgentErrorCode.InvalidAgentResult);
    }

    [Fact]
    public async Task Executor_ShouldRejectSuccessWithFatalError()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var agent = new TestExpertAgent(descriptor, (marketContext, request, _) => Task.FromResult(
            ExpertAgentTestData.Result(
                marketContext,
                request,
                descriptor,
                errors: [new AgentError(AgentErrorCode.UnexpectedAgentFailure, "Fatal error")])));
        var executor = ExpertAgentTestData.Executor([agent]);

        var result = await executor.ExecuteAsync(context, ExpertAgentTestData.Request(context), CancellationToken.None);

        Assert.Contains(result.Errors, error => error.Code == AgentErrorCode.InvalidAgentResult);
    }

    [Fact]
    public async Task Executor_ShouldReturnAgentCancelledWhenAgentCancelsItsOwnWork()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var agent = new TestExpertAgent(descriptor, (marketContext, request, _) => Task.FromResult(
            ExpertAgentTestData.Result(
                marketContext,
                request,
                descriptor,
                AgentAnalysisStatus.Cancelled,
                errors: [new AgentError(AgentErrorCode.AgentCancelled, "Agent stopped before completion")])));
        var executor = ExpertAgentTestData.Executor([agent]);

        var result = await executor.ExecuteAsync(context, ExpertAgentTestData.Request(context), CancellationToken.None);

        Assert.Equal(AgentAnalysisStatus.Cancelled, result.Status);
        Assert.Contains(result.Errors, error => error.Code == AgentErrorCode.AgentCancelled);
    }

    [Fact]
    public async Task Executor_ShouldRejectUnknownSourceReferences()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var unknown = new ContextSourceReference("unknown", "source");
        var agent = new TestExpertAgent(descriptor, (marketContext, request, _) => Task.FromResult(
            ExpertAgentTestData.Result(
                marketContext,
                request,
                descriptor,
                observations: [],
                evidence: [new AgentEvidence(ContextProviderCategory.Knowledge, unknown, "Unknown evidence")] )));
        var executor = ExpertAgentTestData.Executor([agent]);

        var result = await executor.ExecuteAsync(context, ExpertAgentTestData.Request(context), CancellationToken.None);

        Assert.Contains(result.Errors, error => error.Code == AgentErrorCode.InvalidAgentResult);
    }

    [Fact]
    public async Task Executor_ShouldTruncateCollectionsDeterministicallyWithWarning()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var observations = new[]
        {
            new AgentObservation("first", AgentObservationImportance.Low, "First"),
            new AgentObservation("second", AgentObservationImportance.Low, "Second")
        };
        var options = new ExpertAgentOptions
        {
            MaximumObservations = 1,
            MaximumSummaryCharacters = 5
        };
        var agent = new TestExpertAgent(descriptor, (marketContext, request, _) => Task.FromResult(
            ExpertAgentTestData.Result(
                marketContext,
                request,
                descriptor,
                observations: observations,
                summary: "Long summary")));
        var executor = ExpertAgentTestData.Executor([agent], options);

        var result = await executor.ExecuteAsync(context, ExpertAgentTestData.Request(context), CancellationToken.None);

        Assert.Single(result.Observations);
        Assert.Equal("first", result.Observations[0].Type);
        Assert.Equal("Long", result.Summary);
        Assert.Contains(result.Warnings, warning => warning.Code == "RESULT_TRUNCATED");
    }

    [Fact]
    public async Task Executor_ShouldBoundRequestedTimeoutByOptions()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var agent = new TestExpertAgent(descriptor, async (_, _, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Unreachable");
        });
        var options = new ExpertAgentOptions
        {
            DefaultExecutionTimeout = TimeSpan.FromMilliseconds(100),
            MaximumExecutionTimeout = TimeSpan.FromMilliseconds(50)
        };
        var executor = ExpertAgentTestData.Executor([agent], options);

        var result = await executor.ExecuteAsync(
            context,
            ExpertAgentTestData.Request(context, timeout: TimeSpan.FromSeconds(5)),
            CancellationToken.None);

        Assert.Equal(AgentAnalysisStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task Executor_ShouldUseInjectedTimeProviderForFailureTimestamps()
    {
        var context = ExpertAgentTestData.Context();
        var fixedTime = new FixedTimeProvider(ExpertAgentTestData.Now);
        var executor = ExpertAgentTestData.Executor([], timeProvider: fixedTime);

        var result = await executor.ExecuteAsync(context, ExpertAgentTestData.Request(context), CancellationToken.None);

        Assert.Equal(ExpertAgentTestData.Now, result.StartedAtUtc);
        Assert.Equal(ExpertAgentTestData.Now, result.CompletedAtUtc);
    }

    [Fact]
    public void ResultValidator_ShouldRejectExcessCollectionsWhenTruncationDisabled()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var request = ExpertAgentTestData.Request(context);
        var result = ExpertAgentTestData.Result(
            context,
            request,
            descriptor,
            observations: [
                new AgentObservation("one", AgentObservationImportance.Low, "One"),
                new AgentObservation("two", AgentObservationImportance.Low, "Two")]);
        var validator = new AgentAnalysisResultValidator(Options.Create(new ExpertAgentOptions
        {
            MaximumObservations = 1,
            TruncateExcessCollections = false
        }));

        var validation = validator.ValidateAndNormalize(descriptor, context, request, result);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Code == AgentErrorCode.InvalidAgentResult);
    }
}
