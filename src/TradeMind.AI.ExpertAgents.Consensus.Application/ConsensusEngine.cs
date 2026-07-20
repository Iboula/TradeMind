using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Consensus.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.ExpertAgents.Consensus.Application;

public interface IConsensusEngine
{
    Task<ConsensusResult> BuildAsync(ConsensusRequest request, CancellationToken cancellationToken);
}

public sealed class ConsensusEngine(
    IConsensusEligibilityPolicy eligibilityPolicy,
    IConsensusWeightingPolicy weightingPolicy,
    IConsensusConflictPolicy conflictPolicy,
    IOptions<ConsensusOptions> options,
    TimeProvider timeProvider,
    ILogger<ConsensusEngine> logger) : IConsensusEngine
{
    private readonly ConsensusOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task<ConsensusResult> BuildAsync(ConsensusRequest request, CancellationToken cancellationToken)
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
            logger.LogInformation("Consensus {ConsensusId} was cancelled by the caller.", request.ConsensusId);
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            var completedAt = timeProvider.GetUtcNow();
            logger.LogWarning("Consensus {ConsensusId} exceeded its timeout.", request.ConsensusId);
            return Failure(
                request,
                createdAt,
                completedAt,
                ConsensusStatus.TimedOut,
                new ConsensusError("CONSENSUS_TIMEOUT", "The consensus operation exceeded its configured timeout."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Consensus {ConsensusId} failed unexpectedly.", request.ConsensusId);
            return Failure(
                request,
                createdAt,
                timeProvider.GetUtcNow(),
                ConsensusStatus.Failed,
                new ConsensusError("CONSENSUS_FAILURE", "The consensus operation failed unexpectedly."));
        }
    }

    private async Task<ConsensusResult> BuildCoreAsync(
        ConsensusRequest request,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        if (request.Version != ConsensusRequest.CurrentVersion)
        {
            return Failure(
                request,
                createdAt,
                timeProvider.GetUtcNow(),
                ConsensusStatus.Failed,
                new ConsensusError("UNSUPPORTED_REQUEST_VERSION", "The consensus request version is not supported."));
        }

        var eligible = new List<ConsensusEligibleAnalysis>();
        var rejected = new List<ConsensusRejectedAnalysis>();
        var seenRuns = new HashSet<AgentRunId>();

        foreach (var analysis in request.Analyses
                     .OrderBy(item => item.AgentRunId.Value, StringComparer.Ordinal)
                     .ThenBy(item => item.AgentId.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seenRuns.Add(analysis.AgentRunId))
            {
                rejected.Add(new ConsensusRejectedAnalysis(
                    analysis,
                    ConsensusRejectionCode.DuplicateRun,
                    "The analysis run identifier was already supplied."));
                continue;
            }

            if (analysis.MarketContextId != request.MarketContextId)
            {
                rejected.Add(new ConsensusRejectedAnalysis(
                    analysis,
                    ConsensusRejectionCode.ContextMismatch,
                    "The analysis belongs to a different market context."));
                continue;
            }

            var decision = await eligibilityPolicy.EvaluateAsync(analysis, request, cancellationToken).ConfigureAwait(false);
            if (decision.Eligible && decision.Analysis is not null)
            {
                eligible.Add(decision.Analysis);
            }
            else if (decision.Code is not null)
            {
                rejected.Add(new ConsensusRejectedAnalysis(
                    analysis,
                    decision.Code.Value,
                    decision.Message ?? "The analysis is not eligible for consensus."));
            }
        }

        if (eligible.Count == 0)
        {
            var noEligibleWarnings = request.Analyses.Count == 0
                ? new[] { "No analyses were supplied." }
                : Array.Empty<string>();
            return CreateResult(
                request,
                createdAt,
                ConsensusStatus.NoEligibleAnalyses,
                AgentDirectionalBias.InsufficientData,
                ConsensusLevel.None,
                EmptyConfidence("No eligible analysis was available."),
                new ConsensusScore(0, ["no-eligible-analyses"]),
                new ConsensusScore(0, ["no-eligible-analyses"]),
                new ConsensusConclusion("No eligible analysis was available for consolidation.", AgentDirectionalBias.InsufficientData, ConsensusLevel.None),
                eligible,
                [],
                rejected,
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                noEligibleWarnings,
                [],
                timeProvider.GetUtcNow());
        }

        var weights = await weightingPolicy.CalculateAsync(eligible, request, cancellationToken).ConfigureAwait(false);
        var conflicts = await conflictPolicy.AnalyzeAsync(eligible, weights, request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var consolidation = Consolidate(eligible, weights, conflicts, cancellationToken);
        var status = rejected.Count > 0 || eligible.Any(item => item.Result.Status == AgentAnalysisStatus.PartiallySucceeded)
            ? ConsensusStatus.PartiallySucceeded
            : ConsensusStatus.Succeeded;
        return CreateResult(
            request,
            createdAt,
            status,
            consolidation.Bias,
            consolidation.Level,
            consolidation.Confidence,
            consolidation.Agreement,
            consolidation.Disagreement,
            consolidation.Conclusion,
            eligible,
            weights,
            rejected,
            consolidation.Minorities,
            conflicts,
            consolidation.MarketLevels,
            consolidation.Scenarios,
            consolidation.Risks,
            consolidation.Invalidations,
            consolidation.Sources,
            consolidation.Warnings,
            [],
            timeProvider.GetUtcNow());
    }

    private Consolidation Consolidate(
        IReadOnlyList<ConsensusEligibleAnalysis> eligible,
        IReadOnlyList<ConsensusWeight> weights,
        IReadOnlyList<ConsensusConflict> conflicts,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var byRun = weights.ToDictionary(item => item.AgentRunId);
        var total = weights.Sum(item => item.EffectiveShare);
        var shares = Enum.GetValues<AgentDirectionalBias>().ToDictionary(value => value, _ => 0d);
        foreach (var analysis in eligible)
        {
            shares[analysis.Result.DirectionalBias] += byRun[analysis.Result.AgentRunId].EffectiveShare;
        }

        var directionalShare = shares[AgentDirectionalBias.Bullish] + shares[AgentDirectionalBias.Bearish];
        var winner = shares[AgentDirectionalBias.Bullish] >= shares[AgentDirectionalBias.Bearish]
            ? AgentDirectionalBias.Bullish
            : AgentDirectionalBias.Bearish;
        var winnerShare = shares[winner];
        var hasDirectionalVote = directionalShare > 0.0000001;
        var bias = shares[AgentDirectionalBias.Mixed] >= 0.25
            ? AgentDirectionalBias.Mixed
            : !hasDirectionalVote
                ? shares[AgentDirectionalBias.Neutral] > 0 ? AgentDirectionalBias.Neutral : AgentDirectionalBias.InsufficientData
                : Math.Abs(shares[AgentDirectionalBias.Bullish] - shares[AgentDirectionalBias.Bearish]) < 0.0000001
                    ? AgentDirectionalBias.Neutral
                    : winner;

        var agreementValue = hasDirectionalVote
            ? winnerShare / directionalShare * 100
            : shares[AgentDirectionalBias.Neutral] > 0 ? 100 : 0;
        var disagreementValue = hasDirectionalVote
            ? shares[AgentDirectionalBias.Bullish] == 0 || shares[AgentDirectionalBias.Bearish] == 0
                ? 0
                : Math.Min(shares[AgentDirectionalBias.Bullish], shares[AgentDirectionalBias.Bearish]) / directionalShare * 100
            : 0;
        var noOpinionShare = shares[AgentDirectionalBias.InsufficientData] + shares[AgentDirectionalBias.NotApplicable];
        var uncertainty = Math.Clamp(
            shares[AgentDirectionalBias.Mixed] * 100 + noOpinionShare * 100 + shares[AgentDirectionalBias.Neutral] * 10,
            0,
            100);
        var coverage = total <= 0 ? 0 : (total - noOpinionShare) / total * 100;
        var sourceConfidence = total <= 0
            ? 0
            : eligible.Sum(item => item.Result.Confidence.Score * byRun[item.Result.AgentRunId].EffectiveShare) / total;
        var confidenceScore = Math.Clamp(
            coverage * 0.25 + agreementValue * 0.35 + sourceConfidence * 0.25 + (100 - uncertainty) * 0.15,
            0,
            100);
        var limitations = new List<string> { "Consensus confidence is an agreement and data-quality measure, not a probability of profit." };
        var factors = new List<string>
        {
            $"coverage={coverage:0.##}",
            $"agreement={agreementValue:0.##}",
            $"source-confidence={sourceConfidence:0.##}",
            $"uncertainty={uncertainty:0.##}"
        };
        if (conflicts.Any(conflict => conflict.Severity == ConsensusConflictSeverity.Critical))
        {
            confidenceScore = Math.Max(0, confidenceScore - 15);
            limitations.Add("A critical conflict remains unresolved and is visible in the result.");
            factors.Add("critical-conflict-penalty=15");
        }

        var level = DetermineLevel(bias, winnerShare, directionalShare, agreementValue, confidenceScore, conflicts);
        var minorities = BuildMinorities(bias, shares, eligible, byRun);
        var warnings = new List<string>();
        var marketLevels = ConsolidateLevels(eligible, byRun, warnings);
        var scenarios = ConsolidateScenarios(eligible, warnings);
        var risks = ConsolidateRisks(eligible, warnings);
        var invalidations = ConsolidateInvalidations(eligible, warnings);
        var sources = ConsolidateSources(eligible, warnings);
        var summary = BuildConclusion(bias, level, agreementValue, conflicts, _options.MaximumConclusionCharacters, warnings);

        return new Consolidation(
            bias,
            level,
            new ConsensusConfidence(
                confidenceScore,
                BandFor(confidenceScore),
                coverage,
                agreementValue,
                disagreementValue,
                uncertainty,
                factors,
                limitations),
            new ConsensusScore(agreementValue, ["weighted-directional-agreement", "neutral-is-not-opposite-direction"]),
            new ConsensusScore(disagreementValue, ["weighted-directional-disagreement", "mixed-increases-uncertainty"]),
            new ConsensusConclusion(summary, bias, level),
            minorities,
            conflicts,
            marketLevels,
            scenarios,
            risks,
            invalidations,
            sources,
            warnings);
    }

    private List<ConsensusMinorityOpinion> BuildMinorities(
        AgentDirectionalBias bias,
        IReadOnlyDictionary<AgentDirectionalBias, double> shares,
        IReadOnlyList<ConsensusEligibleAnalysis> eligible,
        IReadOnlyDictionary<AgentRunId, ConsensusWeight> weights)
    {
        var result = new List<ConsensusMinorityOpinion>();
        foreach (var candidate in new[] { AgentDirectionalBias.Bullish, AgentDirectionalBias.Bearish, AgentDirectionalBias.Neutral, AgentDirectionalBias.Mixed })
        {
            if (candidate == bias || shares[candidate] < _options.MinimumMinorityShare)
            {
                continue;
            }

            var runs = eligible
                .Where(item => item.Result.DirectionalBias == candidate)
                .Select(item => item.Result.AgentRunId)
                .Where(weights.ContainsKey)
                .ToArray();
            if (runs.Length > 0)
            {
                result.Add(new ConsensusMinorityOpinion(
                    candidate,
                    shares[candidate],
                    runs,
                    $"{candidate} represents a material weighted minority and is preserved for review."));
            }
        }

        return result
            .OrderByDescending(item => item.WeightedShare)
            .ThenBy(item => item.Bias)
            .ToList();
    }

    private List<ConsensusMarketLevel> ConsolidateLevels(
        IReadOnlyList<ConsensusEligibleAnalysis> eligible,
        IReadOnlyDictionary<AgentRunId, ConsensusWeight> weights,
        ICollection<string> warnings)
    {
        var buckets = new List<LevelBucket>();
        foreach (var item in eligible
                     .SelectMany(analysis => analysis.Result.MarketLevels.Select(level => (analysis, level)))
                     .OrderBy(value => value.level.Type)
                     .ThenBy(value => value.level.Timeframe.Code, StringComparer.Ordinal)
                     .ThenBy(value => value.level.Price.Value)
                     .ThenBy(value => value.level.Id, StringComparer.Ordinal)
                     .ThenBy(value => value.analysis.Result.AgentRunId.Value, StringComparer.Ordinal))
        {
            var bucket = buckets.FirstOrDefault(candidate => candidate.CanMerge(item.level, _options.LevelMergeTolerance));
            if (bucket is null)
            {
                bucket = new LevelBucket(item.level);
                buckets.Add(bucket);
            }

            bucket.Add(item.analysis.Result, item.level, weights[item.analysis.Result.AgentRunId].EffectiveShare);
        }

        var materialized = buckets
            .OrderBy(bucket => bucket.Type)
            .ThenBy(bucket => bucket.Timeframe.Code, StringComparer.Ordinal)
            .ThenBy(bucket => bucket.Price)
            .Select((bucket, index) => bucket.ToConsensusLevel($"level-{index + 1}"))
            .ToList();
        return Truncate(materialized, _options.MaximumMarketLevels, "market-levels-truncated", warnings);
    }

    private List<ConsensusScenario> ConsolidateScenarios(
        IReadOnlyList<ConsensusEligibleAnalysis> eligible,
        ICollection<string> warnings)
    {
        var groups = eligible
            .SelectMany(analysis => analysis.Result.Scenarios.Select(scenario => (analysis, scenario)))
            .OrderBy(value => value.scenario.Direction)
            .ThenBy(value => value.scenario.Horizon)
            .ThenBy(value => string.Join("|", value.scenario.ActivationConditions), StringComparer.Ordinal)
            .ThenBy(value => value.scenario.Id, StringComparer.Ordinal)
            .GroupBy(value => new
            {
                value.scenario.Direction,
                value.scenario.Horizon,
                Activation = string.Join("|", value.scenario.ActivationConditions.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                Invalidation = string.Join("|", value.scenario.InvalidationConditions.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                Levels = string.Join("|", value.scenario.LevelIds.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            })
            .OrderBy(group => group.Key.Direction)
            .ThenBy(group => group.Key.Horizon)
            .ThenBy(group => group.Key.Activation, StringComparer.Ordinal)
            .ToArray();

        var result = new List<ConsensusScenario>();
        var index = 1;
        foreach (var group in groups)
        {
            var entries = group.OrderBy(item => item.analysis.Result.AgentRunId.Value, StringComparer.Ordinal).ToArray();
            var first = entries[0].scenario;
            result.Add(new ConsensusScenario(
                $"scenario-{index++}",
                first.Title,
                first.Description,
                first.Direction,
                entries.SelectMany(item => item.scenario.ActivationConditions).ToArray(),
                entries.SelectMany(item => item.scenario.InvalidationConditions).ToArray(),
                entries.SelectMany(item => item.scenario.LevelIds).ToArray(),
                entries.SelectMany(item => item.scenario.Risks).ToArray(),
                first.Horizon,
                entries.Average(item => item.scenario.Confidence.Score),
                entries.Select(item => item.analysis.Result.AgentRunId).Distinct().ToArray()));
        }

        return Truncate(result, _options.MaximumScenarios, "scenarios-truncated", warnings);
    }

    private List<ConsensusRisk> ConsolidateRisks(
        IReadOnlyList<ConsensusEligibleAnalysis> eligible,
        ICollection<string> warnings)
    {
        var groups = eligible
            .SelectMany(analysis => analysis.Result.Risks
                .Concat(analysis.Result.Scenarios.SelectMany(scenario => scenario.Risks))
                .Select(risk => (description: risk, analysis)))
            .GroupBy(item => item.description.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new ConsensusRisk(
                group.First().description,
                ParseRiskSeverity(group.First().description),
                group.Select(item => item.analysis.Result.AgentRunId).Distinct().ToArray(),
                group.SelectMany(item => item.analysis.Result.Evidence.Select(evidence => evidence.SourceReference)).Distinct().ToArray()))
            .OrderByDescending(risk => risk.Severity)
            .ThenBy(risk => risk.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var critical = groups.Where(risk => risk.Severity == ConsensusRiskSeverity.Critical).ToList();
        var result = groups.Take(_options.MaximumRisks).ToList();
        foreach (var item in critical)
        {
            if (!result.Contains(item))
            {
                result.Add(item);
            }
        }

        if (groups.Count > result.Count)
        {
            warnings.Add("risks-truncated-non-critical-items");
        }

        return result
            .OrderByDescending(risk => risk.Severity)
            .ThenBy(risk => risk.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<ConsensusInvalidation> ConsolidateInvalidations(
        IReadOnlyList<ConsensusEligibleAnalysis> eligible,
        ICollection<string> warnings)
    {
        var result = eligible
            .SelectMany(analysis => analysis.Result.Invalidations
                .Concat(analysis.Result.Scenarios.SelectMany(scenario => scenario.InvalidationConditions))
                .Select(value => (value.Trim(), analysis.Result.AgentRunId)))
            .GroupBy(value => value.Item1, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ConsensusInvalidation(group.First().Item1, group.Select(item => item.AgentRunId).Distinct().ToArray()))
            .OrderBy(item => item.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Truncate(result, _options.MaximumInvalidations, "invalidations-truncated", warnings);
    }

    private List<ConsensusSourceTrace> ConsolidateSources(
        IReadOnlyList<ConsensusEligibleAnalysis> eligible,
        ICollection<string> warnings)
    {
        var result = eligible
            .Select(analysis => new ConsensusSourceTrace(
                analysis.Result.AgentRunId,
                analysis.Result.AgentId,
                analysis.Result.AgentVersion,
                analysis.Result.Evidence.Select(item => item.SourceReference)
                    .Concat(analysis.Result.Observations.SelectMany(item => item.References))
                    .Concat(analysis.Result.MarketLevels.SelectMany(item => item.References))
                    .Concat(analysis.Result.Scenarios.SelectMany(item => item.References))
                    .Distinct()
                    .OrderBy(reference => reference.Kind, StringComparer.Ordinal)
                    .ThenBy(reference => reference.Value, StringComparer.Ordinal)
                    .ToArray()))
            .OrderBy(item => item.AgentRunId.Value, StringComparer.Ordinal)
            .ThenBy(item => item.AgentId.Value, StringComparer.Ordinal)
            .ToList();
        return Truncate(result, _options.MaximumSources, "sources-truncated", warnings);
    }

    private static ConsensusRiskSeverity ParseRiskSeverity(string value) =>
        value.StartsWith("critical:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("[critical]", StringComparison.OrdinalIgnoreCase)
            ? ConsensusRiskSeverity.Critical
            : value.StartsWith("high:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("[high]", StringComparison.OrdinalIgnoreCase)
                ? ConsensusRiskSeverity.High
                : value.StartsWith("medium:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("[medium]", StringComparison.OrdinalIgnoreCase)
                    ? ConsensusRiskSeverity.Medium
                    : ConsensusRiskSeverity.Low;

    private static ConsensusLevel DetermineLevel(
        AgentDirectionalBias bias,
        double winnerShare,
        double directionalShare,
        double agreement,
        double confidence,
        IReadOnlyCollection<ConsensusConflict> conflicts)
    {
        if (bias is AgentDirectionalBias.InsufficientData)
        {
            return ConsensusLevel.None;
        }

        if (conflicts.Any(conflict => conflict.Severity == ConsensusConflictSeverity.Critical)
            || bias == AgentDirectionalBias.Mixed)
        {
            return ConsensusLevel.Conflicted;
        }

        var winnerRatio = directionalShare <= 0 ? 0 : winnerShare / directionalShare;
        if (winnerRatio >= 0.70 && agreement >= 70 && confidence >= 70)
        {
            return ConsensusLevel.Strong;
        }

        if (winnerRatio >= 0.60 && agreement >= 55)
        {
            return ConsensusLevel.Moderate;
        }

        return ConsensusLevel.Weak;
    }

    private static ConsensusConfidenceBand BandFor(double score) => score switch
    {
        < 20 => ConsensusConfidenceBand.VeryLow,
        < 40 => ConsensusConfidenceBand.Low,
        < 70 => ConsensusConfidenceBand.Medium,
        < 90 => ConsensusConfidenceBand.High,
        _ => ConsensusConfidenceBand.VeryHigh
    };

    private static ConsensusConfidence EmptyConfidence(string limitation) =>
        new(0, ConsensusConfidenceBand.VeryLow, 0, 0, 0, 100, limitations: [limitation, "Consensus confidence is not a probability of profit."]);

    private static string BuildConclusion(
        AgentDirectionalBias bias,
        ConsensusLevel level,
        double agreement,
        IReadOnlyCollection<ConsensusConflict> conflicts,
        int maximumCharacters,
        ICollection<string> warnings)
    {
        var summary = conflicts.Any(conflict => conflict.Severity == ConsensusConflictSeverity.Critical)
            ? $"{bias} direction remains conflicted; critical conflicts require explicit review."
            : $"Weighted consensus is {bias} at {level} strength with {agreement:0.#}% directional agreement.";
        if (summary.Length <= maximumCharacters)
        {
            return summary;
        }

        warnings.Add("conclusion-truncated");
        return summary[..Math.Max(1, maximumCharacters)].TrimEnd();
    }

    private static List<T> Truncate<T>(
        IReadOnlyList<T> values,
        int maximum,
        string warning,
        ICollection<string> warnings)
    {
        if (values.Count <= maximum)
        {
            return values.ToList();
        }

        warnings.Add(warning);
        return values.Take(maximum).ToList();
    }

    private static ConsensusResult CreateResult(
        ConsensusRequest request,
        DateTimeOffset createdAt,
        ConsensusStatus status,
        AgentDirectionalBias bias,
        ConsensusLevel level,
        ConsensusConfidence confidence,
        ConsensusScore agreement,
        ConsensusScore disagreement,
        ConsensusConclusion conclusion,
        IReadOnlyCollection<ConsensusEligibleAnalysis> eligible,
        IReadOnlyCollection<ConsensusWeight> weights,
        IReadOnlyCollection<ConsensusRejectedAnalysis> rejected,
        IReadOnlyCollection<ConsensusMinorityOpinion> minorities,
        IReadOnlyCollection<ConsensusConflict> conflicts,
        IReadOnlyCollection<ConsensusMarketLevel> levels,
        IReadOnlyCollection<ConsensusScenario> scenarios,
        IReadOnlyCollection<ConsensusRisk> risks,
        IReadOnlyCollection<ConsensusInvalidation> invalidations,
        IReadOnlyCollection<ConsensusSourceTrace> sources,
        IReadOnlyCollection<string> warnings,
        IReadOnlyCollection<ConsensusError> errors,
        DateTimeOffset completedAt) =>
        new(
            request.ConsensusId,
            request.MarketContextId,
            request.Strategy,
            status,
            level,
            bias,
            confidence,
            agreement,
            disagreement,
            conclusion,
            eligible,
            weights,
            rejected,
            minorities,
            conflicts,
            levels,
            scenarios,
            risks,
            invalidations,
            sources,
            warnings,
            errors,
            createdAt,
            completedAt);

    private static ConsensusResult Failure(
        ConsensusRequest request,
        DateTimeOffset createdAt,
        DateTimeOffset completedAt,
        ConsensusStatus status,
        ConsensusError error) =>
        new(
            request.ConsensusId,
            request.MarketContextId,
            request.Strategy,
            status,
            ConsensusLevel.None,
            AgentDirectionalBias.InsufficientData,
            EmptyConfidence(error.Message),
            new ConsensusScore(0, ["failure"]),
            new ConsensusScore(0, ["failure"]),
            new ConsensusConclusion("Consensus could not be produced.", AgentDirectionalBias.InsufficientData, ConsensusLevel.None),
            [], [], [], [], [], [], [], [], [], [], [], [error],
            createdAt,
            completedAt);

    private sealed record Consolidation(
        AgentDirectionalBias Bias,
        ConsensusLevel Level,
        ConsensusConfidence Confidence,
        ConsensusScore Agreement,
        ConsensusScore Disagreement,
        ConsensusConclusion Conclusion,
        IReadOnlyList<ConsensusMinorityOpinion> Minorities,
        IReadOnlyList<ConsensusConflict> Conflicts,
        IReadOnlyList<ConsensusMarketLevel> MarketLevels,
        IReadOnlyList<ConsensusScenario> Scenarios,
        IReadOnlyList<ConsensusRisk> Risks,
        IReadOnlyList<ConsensusInvalidation> Invalidations,
        IReadOnlyList<ConsensusSourceTrace> Sources,
        IReadOnlyList<string> Warnings);

    private sealed class LevelBucket
    {
        private readonly List<(AgentAnalysisResult Analysis, AgentMarketLevel Level, double Weight)> _items = [];

        public LevelBucket(AgentMarketLevel level)
        {
            Type = level.Type;
            Timeframe = level.Timeframe;
        }

        public AgentMarketLevelType Type { get; }
        public Timeframe Timeframe { get; }
        public decimal Price => _items.Count == 0 ? 0 : WeightedPrice;
        private decimal WeightedPrice
        {
            get
            {
                var total = _items.Sum(item => item.Weight);
                return total <= 0
                    ? _items.Average(item => item.Level.Price.Value)
                    : _items.Sum(item => item.Level.Price.Value * (decimal)item.Weight) / (decimal)total;
            }
        }

        public bool CanMerge(AgentMarketLevel level, decimal tolerance) =>
            level.Type == Type
            && level.Timeframe.Code == Timeframe.Code
            && Math.Abs(level.Price.Value - WeightedPrice) <= tolerance;

        public void Add(AgentAnalysisResult analysis, AgentMarketLevel level, double weight) =>
            _items.Add((analysis, level, weight));

        public ConsensusMarketLevel ToConsensusLevel(string id)
        {
            var lower = _items.Min(item => item.Level.LowerBound?.Value ?? item.Level.Price.Value);
            var upper = _items.Max(item => item.Level.UpperBound?.Value ?? item.Level.Price.Value);
            var importance = _items.Max(item => item.Level.Importance);
            var reason = string.Join(" | ", _items
                .Select(item => item.Level.Reason)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal));
            return new ConsensusMarketLevel(
                Type,
                new Price(WeightedPrice),
                Timeframe,
                importance,
                reason,
                _items.Select(item => item.Analysis.AgentRunId).Distinct().ToArray(),
                new Price(lower),
                new Price(upper),
                _items.SelectMany(item => item.Level.References).Distinct().ToArray());
        }
    }
}
