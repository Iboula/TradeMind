using Microsoft.Extensions.Options;
using TradeMind.AI.ExpertAgents.Consensus.Domain;
using TradeMind.AI.ExpertAgents.Domain;

namespace TradeMind.AI.ExpertAgents.Consensus.Application;

public sealed record ConsensusEligibilityDecision
{
    private ConsensusEligibilityDecision(
        bool eligible,
        ConsensusEligibleAnalysis? analysis,
        ConsensusRejectionCode? code,
        string? message)
    {
        Eligible = eligible;
        Analysis = analysis;
        Code = code;
        Message = message;
    }

    public bool Eligible { get; }
    public ConsensusEligibleAnalysis? Analysis { get; }
    public ConsensusRejectionCode? Code { get; }
    public string? Message { get; }

    public static ConsensusEligibilityDecision Include(ConsensusEligibleAnalysis analysis) =>
        new(true, analysis, null, null);

    public static ConsensusEligibilityDecision Reject(ConsensusRejectionCode code, string message) =>
        new(false, null, code, message);
}

public interface IConsensusEligibilityPolicy
{
    ValueTask<ConsensusEligibilityDecision> EvaluateAsync(
        AgentAnalysisResult result,
        ConsensusRequest request,
        CancellationToken cancellationToken);
}

public sealed class DefaultConsensusEligibilityPolicy(IOptions<ConsensusOptions> options) : IConsensusEligibilityPolicy
{
    private readonly ConsensusOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public ValueTask<ConsensusEligibilityDecision> EvaluateAsync(
        AgentAnalysisResult result,
        ConsensusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (result.Status is not (AgentAnalysisStatus.Succeeded or AgentAnalysisStatus.PartiallySucceeded))
        {
            return ValueTask.FromResult(ConsensusEligibilityDecision.Reject(
                ConsensusRejectionCode.InvalidResultStatus,
                $"Analysis status '{result.Status}' cannot contribute to consensus."));
        }

        if (result.SchemaVersion != AgentAnalysisResult.CurrentSchemaVersion)
        {
            return ValueTask.FromResult(ConsensusEligibilityDecision.Reject(
                ConsensusRejectionCode.UnsupportedSchema,
                "The analysis schema version is not supported by the consensus engine."));
        }

        if (result.Errors.Any(error => error.Fatal))
        {
            return ValueTask.FromResult(ConsensusEligibilityDecision.Reject(
                ConsensusRejectionCode.InvalidResult,
                "The analysis contains a fatal validation error."));
        }

        var hasNoOpinion = result.DirectionalBias is AgentDirectionalBias.InsufficientData or AgentDirectionalBias.NotApplicable;
        if (!hasNoOpinion && result.Confidence.Score < _options.MinimumConfidenceScore)
        {
            return ValueTask.FromResult(ConsensusEligibilityDecision.Reject(
                ConsensusRejectionCode.InsufficientConfidence,
                "The analysis confidence is below the consensus threshold."));
        }

        if (!hasNoOpinion && result.Confidence.ContextFreshness < _options.MinimumFreshnessScore)
        {
            return ValueTask.FromResult(ConsensusEligibilityDecision.Reject(
                ConsensusRejectionCode.InsufficientFreshness,
                "The analysis context freshness is below the consensus threshold."));
        }

        var penalty = result.Status == AgentAnalysisStatus.PartiallySucceeded
            ? _options.PartiallySucceededPenalty
            : 1;
        var reasons = new List<string>();
        if (result.Status == AgentAnalysisStatus.PartiallySucceeded)
        {
            reasons.Add("PartiallySucceeded analysis included with an explicit penalty.");
        }
        else if (hasNoOpinion)
        {
            reasons.Add("The analysis remains traceable but does not contribute a directional vote.");
        }
        else
        {
            reasons.Add("Analysis status and confidence/freshness thresholds are eligible.");
        }
        return ValueTask.FromResult(ConsensusEligibilityDecision.Include(
            new ConsensusEligibleAnalysis(result, !hasNoOpinion, penalty, reasons)));
    }
}

public interface IConsensusWeightingPolicy
{
    ValueTask<IReadOnlyList<ConsensusWeight>> CalculateAsync(
        IReadOnlyCollection<ConsensusEligibleAnalysis> analyses,
        ConsensusRequest request,
        CancellationToken cancellationToken);
}

public sealed class DefaultConsensusWeightingPolicy(IOptions<ConsensusOptions> options) : IConsensusWeightingPolicy
{
    private readonly ConsensusOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public ValueTask<IReadOnlyList<ConsensusWeight>> CalculateAsync(
        IReadOnlyCollection<ConsensusEligibleAnalysis> analyses,
        ConsensusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analyses);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var raw = analyses
            .OrderBy(analysis => analysis.Result.AgentRunId.Value, StringComparer.Ordinal)
            .Select(analysis =>
            {
                var confidence = analysis.Result.Confidence.Score / 100;
                var coverage = analysis.Result.Confidence.DataCoverage / 100;
                var freshness = analysis.Result.Confidence.ContextFreshness / 100;
                var evidence = analysis.Result.Evidence.Count == 0
                    ? 0.85
                    : analysis.Result.Evidence.Average(item => item.Weight) / 100;
                var noOpinionPenalty = analysis.ContributesOpinion ? 1 : 0.5;
                var value = Math.Max(0.001, confidence * Math.Max(0.1, coverage) * Math.Max(0.1, freshness)
                    * evidence * analysis.Penalty * noOpinionPenalty);
                var factors = new List<string>
                {
                    $"confidence={analysis.Result.Confidence.Score:0.##}",
                    $"coverage={analysis.Result.Confidence.DataCoverage:0.##}",
                    $"freshness={analysis.Result.Confidence.ContextFreshness:0.##}",
                    $"evidence={evidence * 100:0.##}",
                    $"status-penalty={analysis.Penalty:0.##}"
                };
                if (!analysis.ContributesOpinion)
                {
                    factors.Add("no-directional-vote=0.5");
                }

                return new RawWeight(analysis, value, factors);
            })
            .ToArray();
        var totalRaw = raw.Sum(item => item.Value);
        var cap = totalRaw * _options.MaximumAgentContributionShare;
        var capped = raw
            .GroupBy(item => item.Analysis.Result.AgentId)
            .SelectMany(group =>
            {
                var groupTotal = group.Sum(item => item.Value);
                var scale = groupTotal > cap && groupTotal > 0 ? cap / groupTotal : 1;
                return group.Select(item => new
                {
                    item.Analysis,
                    item.Value,
                    Effective = item.Value * scale,
                    Capped = scale < 1,
                    item.Factors
                });
            })
            .OrderBy(item => item.Analysis.Result.AgentRunId.Value, StringComparer.Ordinal)
            .ToArray();
        var result = capped
            .Select(item => new ConsensusWeight(
                item.Analysis.Result.AgentRunId,
                item.Analysis.Result.AgentId,
                item.Value,
                totalRaw == 0 ? 0 : item.Effective / totalRaw,
                item.Capped,
                item.Factors.Concat(item.Capped ? ["agent-contribution-cap-applied"] : []).ToArray()))
            .ToArray();
        return ValueTask.FromResult<IReadOnlyList<ConsensusWeight>>(Array.AsReadOnly(result));
    }

    private sealed record RawWeight(ConsensusEligibleAnalysis Analysis, double Value, IReadOnlyCollection<string> Factors);
}

public interface IConsensusConflictPolicy
{
    ValueTask<IReadOnlyList<ConsensusConflict>> AnalyzeAsync(
        IReadOnlyCollection<ConsensusEligibleAnalysis> analyses,
        IReadOnlyCollection<ConsensusWeight> weights,
        ConsensusRequest request,
        CancellationToken cancellationToken);
}

public sealed class DefaultConsensusConflictPolicy(IOptions<ConsensusOptions> options) : IConsensusConflictPolicy
{
    private readonly ConsensusOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public ValueTask<IReadOnlyList<ConsensusConflict>> AnalyzeAsync(
        IReadOnlyCollection<ConsensusEligibleAnalysis> analyses,
        IReadOnlyCollection<ConsensusWeight> weights,
        ConsensusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analyses);
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var byRun = weights.ToDictionary(weight => weight.AgentRunId);
        var conflicts = new List<ConsensusConflict>();
        var directional = analyses
            .Where(analysis => analysis.ContributesOpinion)
            .GroupBy(analysis => analysis.Result.DirectionalBias)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var bullish = Share(AgentDirectionalBias.Bullish, directional, byRun);
        var bearish = Share(AgentDirectionalBias.Bearish, directional, byRun);
        if (bullish >= _options.CriticalConflictShare && bearish >= _options.CriticalConflictShare)
        {
            conflicts.Add(new(
                ConsensusConflictKind.Directional,
                ConsensusConflictSeverity.Critical,
                "Strongly weighted Bullish and Bearish opinions remain in conflict.",
                Runs(directional, AgentDirectionalBias.Bullish).Concat(Runs(directional, AgentDirectionalBias.Bearish)).ToArray()));
        }

        var invalidationGroups = analyses
            .SelectMany(analysis => analysis.Result.Invalidations.Select(value => (Value: Normalize(value), Analysis: analysis)))
            .GroupBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (invalidationGroups.Length > 1)
        {
            conflicts.Add(new(
                ConsensusConflictKind.Invalidation,
                ConsensusConflictSeverity.High,
                "Eligible analyses contain multiple distinct invalidation conditions.",
                invalidationGroups.SelectMany(group => group.Select(item => item.Analysis.Result.AgentRunId)).Distinct().ToArray()));
        }

        var levels = analyses.SelectMany(analysis => analysis.Result.MarketLevels.Select(level => (level, analysis))).ToArray();
        foreach (var group in levels.GroupBy(item => (item.level.Type, item.level.Timeframe.Code)))
        {
            if (group.Select(item => item.level.Price.Value).Max() - group.Select(item => item.level.Price.Value).Min() > _options.LevelMergeTolerance)
            {
                conflicts.Add(new(
                    ConsensusConflictKind.Level,
                    ConsensusConflictSeverity.High,
                    $"Multiple incompatible {group.Key.Type} levels exist on timeframe {group.Key.Code}.",
                    group.Select(item => item.analysis.Result.AgentRunId).Distinct().ToArray(),
                    group.SelectMany(item => item.level.References).ToArray()));
            }
        }

        var scenarios = analyses.SelectMany(analysis => analysis.Result.Scenarios.Select(scenario => (scenario, analysis))).ToArray();
        foreach (var group in scenarios.GroupBy(item => item.scenario.Horizon))
        {
            var directions = group.Select(item => item.scenario.Direction).Where(direction => direction is AgentDirectionalBias.Bullish or AgentDirectionalBias.Bearish).Distinct().ToArray();
            if (directions.Contains(AgentDirectionalBias.Bullish) && directions.Contains(AgentDirectionalBias.Bearish))
            {
                conflicts.Add(new(
                    ConsensusConflictKind.Scenario,
                    ConsensusConflictSeverity.High,
                    "Opposite scenarios exist for the same horizon.",
                    group.Select(item => item.analysis.Result.AgentRunId).Distinct().ToArray()));
            }
        }

        var criticalRisks = analyses
            .SelectMany(analysis => analysis.Result.Risks
                .Where(IsCriticalRisk)
                .Select(_ => analysis.Result.AgentRunId))
            .Distinct()
            .ToArray();
        if (criticalRisks.Length > 0 && (bullish > 0 || bearish > 0))
        {
            conflicts.Add(new(
                ConsensusConflictKind.RiskDirection,
                ConsensusConflictSeverity.Critical,
                "A critical risk is present alongside a directional opinion and must remain visible.",
                criticalRisks.Concat(analyses
                    .Where(analysis => analysis.ContributesOpinion)
                    .Select(analysis => analysis.Result.AgentRunId))
                    .Distinct()
                    .ToArray()));
        }

        return ValueTask.FromResult<IReadOnlyList<ConsensusConflict>>(Array.AsReadOnly(conflicts
            .OrderBy(conflict => conflict.Kind)
            .ThenByDescending(conflict => conflict.Severity)
            .ThenBy(conflict => conflict.Summary, StringComparer.Ordinal)
            .ToArray()));
    }

    private static double Share(
        AgentDirectionalBias bias,
        IReadOnlyDictionary<AgentDirectionalBias, ConsensusEligibleAnalysis[]> groups,
        IReadOnlyDictionary<AgentRunId, ConsensusWeight> weights) =>
        groups.TryGetValue(bias, out var analyses)
            ? analyses.Sum(analysis => weights[analysis.Result.AgentRunId].EffectiveShare)
            : 0;

    private static IReadOnlyCollection<AgentRunId> Runs(
        IReadOnlyDictionary<AgentDirectionalBias, ConsensusEligibleAnalysis[]> groups,
        AgentDirectionalBias bias) =>
        groups.TryGetValue(bias, out var analyses)
            ? analyses.Select(analysis => analysis.Result.AgentRunId).ToArray()
            : [];

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static bool IsCriticalRisk(string value) =>
        value.StartsWith("critical:", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("[critical]", StringComparison.OrdinalIgnoreCase);
}
