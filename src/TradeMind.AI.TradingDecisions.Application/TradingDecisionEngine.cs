using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Consensus.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.AI.TradingDecisions.Domain;

namespace TradeMind.AI.TradingDecisions.Application;

public interface ITradingDecisionEngine
{
    Task<TradingDecisionResult> BuildAsync(
        TradingDecisionRequest request,
        CancellationToken cancellationToken);
}

public sealed class TradingDecisionEngine(
    ITradingDecisionEligibilityPolicy eligibilityPolicy,
    ITradingDecisionPolicy decisionPolicy,
    IOptions<TradingDecisionOptions> options,
    TimeProvider timeProvider,
    ILogger<TradingDecisionEngine> logger) : ITradingDecisionEngine
{
    private readonly TradingDecisionOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task<TradingDecisionResult> BuildAsync(
        TradingDecisionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var createdAt = timeProvider.GetUtcNow();
        using var timeoutSource = new CancellationTokenSource(request.Timeout ?? _options.Timeout, timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            return await BuildCoreAsync(request, createdAt, linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Trading decision {DecisionId} was cancelled by the caller.", request.DecisionId);
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            logger.LogWarning("Trading decision {DecisionId} exceeded its timeout.", request.DecisionId);
            return Failure(
                request,
                createdAt,
                timeProvider.GetUtcNow(),
                TradingDecisionStatus.TimedOut,
                "DECISION_TIMEOUT",
                "The trading decision operation exceeded its configured timeout.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Trading decision {DecisionId} failed unexpectedly.", request.DecisionId);
            return Failure(
                request,
                createdAt,
                timeProvider.GetUtcNow(),
                TradingDecisionStatus.Failed,
                "DECISION_FAILURE",
                "The trading decision operation failed unexpectedly.");
        }
    }

    private async Task<TradingDecisionResult> BuildCoreAsync(
        TradingDecisionRequest request,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var eligibility = await eligibilityPolicy.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!eligibility.Eligible)
        {
            logger.LogInformation(
                "Trading decision {DecisionId} was not eligible: {Code}.",
                request.DecisionId,
                eligibility.Code);
            return FromEligibility(request, createdAt, eligibility);
        }

        var outcome = await decisionPolicy.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return Materialize(request, createdAt, timeProvider.GetUtcNow(), outcome);
    }

    private TradingDecisionResult FromEligibility(
        TradingDecisionRequest request,
        DateTimeOffset createdAt,
        TradingDecisionEligibilityDecision eligibility)
    {
        var type = eligibility.Code is TradingDecisionRejectionCode.InsufficientConsensusConfidence
            or TradingDecisionRejectionCode.InsufficientConsensusCoverage
            or TradingDecisionRejectionCode.ConsensusStatusNotEligible
            or TradingDecisionRejectionCode.UnsupportedConsensusVersion
            ? TradingDecisionType.InsufficientData
            : TradingDecisionType.NoTrade;
        var error = new TradingDecisionError(
            eligibility.Code?.ToString() ?? TradingDecisionRejectionCode.Unknown.ToString(),
            eligibility.Message ?? "The trading decision request is not eligible.");
        var outcome = new TradingDecisionPolicyOutcome(
            type,
            null,
            [],
            null,
            null,
            [],
            [],
            [error],
            Math.Min(request.Consensus.Confidence.Score, type == TradingDecisionType.InsufficientData ? 25 : 40));
        return Materialize(request, createdAt, timeProvider.GetUtcNow(), outcome);
    }

    private TradingDecisionResult Materialize(
        TradingDecisionRequest request,
        DateTimeOffset createdAt,
        DateTimeOffset completedAt,
        TradingDecisionPolicyOutcome outcome)
    {
        var consensus = request.Consensus;
        var warnings = outcome.Warnings.ToList();
        var errors = outcome.Errors.ToList();
        var risks = MaterializeRisks(consensus.Risks, warnings);
        var invalidations = MaterializeInvalidations(consensus.Invalidations, consensus.ConsolidatedBias, outcome.Stop is not null, warnings);
        var traces = MaterializeTraces(consensus, outcome, risks, invalidations, warnings);
        warnings = TruncateWarnings(warnings);
        var status = consensus.Status == ConsensusStatus.PartiallySucceeded
            ? TradingDecisionStatus.PartiallySucceeded
            : outcome.Type == TradingDecisionType.InsufficientData
                ? TradingDecisionStatus.InsufficientData
                : TradingDecisionStatus.Succeeded;
        var limitations = new List<string>
        {
            "This decision model describes setup coherence and evidence quality; it is not a promise of gain, sizing instruction or risk calculation."
        };
        if (outcome.Type is not (TradingDecisionType.LongSetup or TradingDecisionType.ShortSetup))
        {
            limitations.Add("No executable trading setup was produced.");
        }

        var factors = new List<string>
        {
            $"decision-score={outcome.Score:0.##}",
            $"consensus-confidence={consensus.Confidence.Score:0.##}",
            $"consensus-coverage={consensus.Confidence.Coverage:0.##}",
            $"scenario-selected={(outcome.PrimaryScenario is not null).ToString().ToLowerInvariant()}",
            $"levels-coherent={(outcome.Entry is not null && outcome.Stop is not null).ToString().ToLowerInvariant()}"
        };
        var confidence = new TradingDecisionConfidence(
            outcome.Score,
            BandFor(outcome.Score),
            consensus.Confidence.Score,
            consensus.Confidence.Coverage,
            factors,
            limitations);

        return new TradingDecisionResult(
            request.DecisionId,
            request.ConsensusId,
            request.MarketContextId,
            request.Instrument,
            request.Timeframe,
            request.Strategy,
            status,
            outcome.Type,
            confidence,
            outcome.PrimaryScenario,
            outcome.Alternatives,
            outcome.Entry,
            outcome.Stop,
            outcome.Targets,
            risks,
            invalidations,
            consensus.Conflicts,
            traces,
            warnings,
            errors,
            createdAt,
            completedAt,
            userId: request.UserId,
            sessionId: request.SessionId);
    }

    private List<DecisionRisk> MaterializeRisks(
        IReadOnlyList<ConsensusRisk> sourceRisks,
        ICollection<TradingDecisionWarning> warnings)
    {
        var ordered = sourceRisks
            .OrderByDescending(risk => risk.Severity)
            .ThenBy(risk => risk.Description, StringComparer.OrdinalIgnoreCase)
            .ThenBy(risk => string.Join("|", risk.SourceRuns.Select(run => run.Value)), StringComparer.Ordinal)
            .Select(risk => new DecisionRisk(risk, "Preserved from the consensus risk set."))
            .ToList();
        var critical = ordered.Where(risk => risk.IsCritical).ToList();
        var result = ordered.Take(_options.MaximumRisks).ToList();
        foreach (var risk in critical)
        {
            if (!result.Contains(risk))
            {
                result.Add(risk);
            }
        }

        if (result.Count < ordered.Count)
        {
            warnings.Add(new TradingDecisionWarning("RISKS_TRUNCATED", "Non-critical risks were truncated at the configured limit."));
        }

        return result
            .OrderByDescending(risk => risk.Source.Severity)
            .ThenBy(risk => risk.Source.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<DecisionInvalidation> MaterializeInvalidations(
        IReadOnlyList<ConsensusInvalidation> sourceInvalidations,
        AgentDirectionalBias direction,
        bool hasCoherentStop,
        ICollection<TradingDecisionWarning> warnings)
    {
        var result = sourceInvalidations
            .OrderBy(item => item.Description, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => string.Join("|", item.SourceRuns.Select(run => run.Value)), StringComparer.Ordinal)
            .Take(_options.MaximumInvalidations)
            .Select(item => new DecisionInvalidation(
                item,
                direction,
                hasCoherentStop,
                hasCoherentStop ? "The invalidation is backed by an explicit coherent stop level." : "The invalidation is preserved but no coherent stop level was available."))
            .ToList();
        if (sourceInvalidations.Count > result.Count)
        {
            warnings.Add(new TradingDecisionWarning("INVALIDATIONS_TRUNCATED", "Invalidations were truncated at the configured limit."));
        }

        return result;
    }

    private List<DecisionTraceReference> MaterializeTraces(
        ConsensusResult consensus,
        TradingDecisionPolicyOutcome outcome,
        IReadOnlyCollection<DecisionRisk> risks,
        IReadOnlyCollection<DecisionInvalidation> invalidations,
        ICollection<TradingDecisionWarning> warnings)
    {
        var traces = new List<DecisionTraceReference>();
        foreach (var source in consensus.Sources
                     .OrderBy(item => item.AgentRunId.Value, StringComparer.Ordinal)
                     .ThenBy(item => item.AgentId.Value, StringComparer.Ordinal))
        {
            traces.AddRange(source.References
                .OrderBy(reference => reference.Kind, StringComparer.Ordinal)
                .ThenBy(reference => reference.Value, StringComparer.Ordinal)
                .Select(reference => new DecisionTraceReference(
                    source.AgentRunId,
                    source.AgentId,
                    source.AgentVersion,
                    reference,
                    DecisionTraceRole.Consensus)));
        }

        AddProposalTraces(traces, outcome.Entry?.SourceRuns, outcome.Entry?.References, consensus, DecisionTraceRole.Entry);
        AddProposalTraces(traces, outcome.Stop?.SourceRuns, outcome.Stop?.References, consensus, DecisionTraceRole.Stop);
        AddScenarioTraces(traces, outcome.PrimaryScenario, consensus, DecisionTraceRole.Scenario);
        foreach (var alternative in outcome.Alternatives)
        {
            AddScenarioTraces(traces, alternative, consensus, DecisionTraceRole.Scenario);
        }

        foreach (var target in outcome.Targets)
        {
            AddProposalTraces(traces, target.SourceRuns, target.References, consensus, DecisionTraceRole.Target);
        }

        foreach (var risk in risks)
        {
            AddRiskTraces(traces, risk.Source, consensus, DecisionTraceRole.Risk);
        }

        foreach (var invalidation in invalidations)
        {
            AddInvalidationTraces(traces, invalidation.Source, consensus, DecisionTraceRole.Invalidation);
        }

        var unique = traces
            .GroupBy(item => new
            {
                Run = item.AgentRunId.Value,
                Agent = item.AgentId.Value,
                Version = item.AgentVersion.ToString(),
                Kind = item.Reference.Kind,
                Value = item.Reference.Value,
                item.Role
            })
            .Select(group => group.First())
            .OrderBy(item => item.AgentRunId.Value, StringComparer.Ordinal)
            .ThenBy(item => item.Role)
            .ThenBy(item => item.Reference.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Reference.Value, StringComparer.Ordinal)
            .ToList();
        if (unique.Count > _options.MaximumTraces)
        {
            warnings.Add(new TradingDecisionWarning("TRACES_TRUNCATED", "Decision traces were truncated at the configured limit."));
        }

        return unique.Take(_options.MaximumTraces).ToList();
    }

    private static void AddScenarioTraces(
        ICollection<DecisionTraceReference> traces,
        DecisionScenario? scenario,
        ConsensusResult consensus,
        DecisionTraceRole role)
    {
        if (scenario is null)
        {
            return;
        }

        foreach (var sourceRun in scenario.Source.SourceRuns)
        {
            var source = consensus.Sources.FirstOrDefault(item => item.AgentRunId == sourceRun);
            if (source is null)
            {
                continue;
            }

            foreach (var reference in source.References)
            {
                traces.Add(new DecisionTraceReference(sourceRun, source.AgentId, source.AgentVersion, reference, role));
            }
        }
    }

    private static void AddProposalTraces(
        ICollection<DecisionTraceReference> traces,
        IEnumerable<AgentRunId>? runs,
        IEnumerable<ContextSourceReference>? references,
        ConsensusResult consensus,
        DecisionTraceRole role)
    {
        if (runs is null || references is null)
        {
            return;
        }

        foreach (var run in runs)
        {
            var source = consensus.Sources.FirstOrDefault(item => item.AgentRunId == run);
            if (source is null)
            {
                continue;
            }

            foreach (var reference in references)
            {
                traces.Add(new DecisionTraceReference(run, source.AgentId, source.AgentVersion, reference, role));
            }
        }
    }

    private static void AddRiskTraces(
        ICollection<DecisionTraceReference> traces,
        ConsensusRisk risk,
        ConsensusResult consensus,
        DecisionTraceRole role)
    {
        foreach (var sourceRun in risk.SourceRuns)
        {
            var source = consensus.Sources.FirstOrDefault(item => item.AgentRunId == sourceRun);
            if (source is null)
            {
                continue;
            }

            foreach (var reference in risk.References)
            {
                traces.Add(new DecisionTraceReference(sourceRun, source.AgentId, source.AgentVersion, reference, role));
            }
        }
    }

    private static void AddInvalidationTraces(
        ICollection<DecisionTraceReference> traces,
        ConsensusInvalidation invalidation,
        ConsensusResult consensus,
        DecisionTraceRole role)
    {
        foreach (var sourceRun in invalidation.SourceRuns)
        {
            var source = consensus.Sources.FirstOrDefault(item => item.AgentRunId == sourceRun);
            if (source is null)
            {
                continue;
            }

            foreach (var reference in source.References)
            {
                traces.Add(new DecisionTraceReference(sourceRun, source.AgentId, source.AgentVersion, reference, role));
            }
        }
    }

    private List<TradingDecisionWarning> TruncateWarnings(IEnumerable<TradingDecisionWarning> warnings)
    {
        var unique = warnings
            .GroupBy(item => (item.Code, item.Message))
            .Select(group => group.First())
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Message, StringComparer.Ordinal)
            .ToList();
        return unique.Take(_options.MaximumWarnings).ToList();
    }

    private static TradingDecisionConfidenceBand BandFor(double score) => score switch
    {
        < 20 => TradingDecisionConfidenceBand.VeryLow,
        < 40 => TradingDecisionConfidenceBand.Low,
        < 70 => TradingDecisionConfidenceBand.Medium,
        < 90 => TradingDecisionConfidenceBand.High,
        _ => TradingDecisionConfidenceBand.VeryHigh
    };

    private static TradingDecisionResult Failure(
        TradingDecisionRequest request,
        DateTimeOffset createdAt,
        DateTimeOffset completedAt,
        TradingDecisionStatus status,
        string code,
        string message) =>
        new(
            request.DecisionId,
            request.ConsensusId,
            request.MarketContextId,
            request.Instrument,
            request.Timeframe,
            request.Strategy,
            status,
            TradingDecisionType.NoTrade,
            new TradingDecisionConfidence(
                0,
                TradingDecisionConfidenceBand.VeryLow,
                request.Consensus.Confidence.Score,
                request.Consensus.Confidence.Coverage,
                limitations: [message, "This result is not a promise of gain."]),
            null,
            [],
            null,
            null,
            [],
            [],
            [],
            request.Consensus.Conflicts,
            [],
            [],
            [new TradingDecisionError(code, message, true)],
            createdAt,
            completedAt,
            userId: request.UserId,
            sessionId: request.SessionId);
}
