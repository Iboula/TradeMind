using Microsoft.Extensions.Options;
using TradeMind.AI.ExpertAgents.Consensus.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.AI.TradingDecisions.Domain;

namespace TradeMind.AI.TradingDecisions.Application;

public sealed record TradingDecisionEligibilityDecision
{
    private TradingDecisionEligibilityDecision(bool eligible, TradingDecisionRejectionCode? code, string? message)
    {
        Eligible = eligible;
        Code = code;
        Message = message;
    }

    public bool Eligible { get; }
    public TradingDecisionRejectionCode? Code { get; }
    public string? Message { get; }

    public static TradingDecisionEligibilityDecision Include() => new(true, null, null);

    public static TradingDecisionEligibilityDecision Reject(TradingDecisionRejectionCode code, string message) =>
        new(false, code, message);
}

public interface ITradingDecisionEligibilityPolicy
{
    ValueTask<TradingDecisionEligibilityDecision> EvaluateAsync(
        TradingDecisionRequest request,
        CancellationToken cancellationToken);
}

public sealed class DefaultTradingDecisionEligibilityPolicy(
    IOptions<TradingDecisionOptions> options) : ITradingDecisionEligibilityPolicy
{
    private readonly TradingDecisionOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public ValueTask<TradingDecisionEligibilityDecision> EvaluateAsync(
        TradingDecisionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var consensus = request.Consensus;

        if (request.Version != TradingDecisionRequest.CurrentVersion)
        {
            return Reject(TradingDecisionRejectionCode.UnsupportedRequestVersion, "The trading decision request version is not supported.");
        }

        if (consensus.SchemaVersion != ConsensusResult.CurrentSchemaVersion)
        {
            return Reject(TradingDecisionRejectionCode.UnsupportedConsensusVersion, "The consensus result schema version is not supported.");
        }

        if (consensus.ConsensusId != request.ConsensusId)
        {
            return Reject(TradingDecisionRejectionCode.ConsensusMismatch, "The request consensus id does not match the supplied result.");
        }

        if (consensus.MarketContextId != request.MarketContextId)
        {
            return Reject(TradingDecisionRejectionCode.MarketContextMismatch, "The consensus belongs to a different market context.");
        }

        if (consensus.Status is not (ConsensusStatus.Succeeded or ConsensusStatus.PartiallySucceeded))
        {
            return Reject(TradingDecisionRejectionCode.ConsensusStatusNotEligible, "The consensus status cannot produce a trading decision.");
        }

        if (consensus.Confidence.Score < _options.MinimumConsensusConfidence)
        {
            return Reject(TradingDecisionRejectionCode.InsufficientConsensusConfidence, "Consensus confidence is below the decision threshold.");
        }

        if (consensus.Confidence.Coverage < _options.MinimumConsensusCoverage)
        {
            return Reject(TradingDecisionRejectionCode.InsufficientConsensusCoverage, "Consensus coverage is below the decision threshold.");
        }

        return ValueTask.FromResult(TradingDecisionEligibilityDecision.Include());
    }

    private static ValueTask<TradingDecisionEligibilityDecision> Reject(TradingDecisionRejectionCode code, string message) =>
        ValueTask.FromResult(TradingDecisionEligibilityDecision.Reject(code, message));
}

public sealed record TradingDecisionPolicyOutcome
{
    public TradingDecisionPolicyOutcome(
        TradingDecisionType type,
        DecisionScenario? primaryScenario,
        IReadOnlyCollection<DecisionScenario> alternatives,
        EntryProposal? entry,
        StopProposal? stop,
        IReadOnlyCollection<TargetProposal> targets,
        IReadOnlyCollection<TradingDecisionWarning> warnings,
        IReadOnlyCollection<TradingDecisionError> errors,
        double score)
    {
        if (score is < 0 or > 100 || double.IsNaN(score) || double.IsInfinity(score))
        {
            throw new ArgumentOutOfRangeException(nameof(score));
        }

        ArgumentNullException.ThrowIfNull(alternatives);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(errors);
        Type = type;
        PrimaryScenario = primaryScenario;
        Alternatives = Array.AsReadOnly(alternatives.ToArray());
        Entry = entry;
        Stop = stop;
        Targets = Array.AsReadOnly(targets.ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
        Errors = Array.AsReadOnly(errors.ToArray());
        Score = score;
    }

    public TradingDecisionType Type { get; }
    public DecisionScenario? PrimaryScenario { get; }
    public IReadOnlyList<DecisionScenario> Alternatives { get; }
    public EntryProposal? Entry { get; }
    public StopProposal? Stop { get; }
    public IReadOnlyList<TargetProposal> Targets { get; }
    public IReadOnlyList<TradingDecisionWarning> Warnings { get; }
    public IReadOnlyList<TradingDecisionError> Errors { get; }
    public double Score { get; }
}

public interface ITradingDecisionPolicy
{
    ValueTask<TradingDecisionPolicyOutcome> EvaluateAsync(
        TradingDecisionRequest request,
        CancellationToken cancellationToken);
}

public sealed class DefaultTradingDecisionPolicy(
    IOptions<TradingDecisionOptions> options) : ITradingDecisionPolicy
{
    private readonly TradingDecisionOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public ValueTask<TradingDecisionPolicyOutcome> EvaluateAsync(
        TradingDecisionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var consensus = request.Consensus;
        var warnings = new List<TradingDecisionWarning>();
        var errors = new List<TradingDecisionError>();
        var alternatives = SelectScenarios(request, warnings, out var primary);

        if (consensus.Status == ConsensusStatus.NoEligibleAnalyses
            || consensus.Confidence.Coverage < _options.MinimumConsensusCoverage)
        {
            errors.Add(new TradingDecisionError("INSUFFICIENT_DATA", "The consensus does not contain enough eligible data."));
            return Outcome(TradingDecisionType.InsufficientData, primary, alternatives, null, null, [], warnings, errors, consensus.Confidence.Score);
        }

        if (consensus.Conflicts.Any(conflict => conflict.Severity == ConsensusConflictSeverity.Critical)
            || consensus.Level == ConsensusLevel.Conflicted)
        {
            errors.Add(new TradingDecisionError("CRITICAL_CONFLICT", "A critical consensus conflict blocks a directional setup."));
            return Outcome(TradingDecisionType.Conflicted, primary, alternatives, null, null, [], warnings, errors, Math.Min(consensus.Confidence.Score, 50));
        }

        if (consensus.ConsolidatedBias is not (AgentDirectionalBias.Bullish or AgentDirectionalBias.Bearish))
        {
            var neutralType = consensus.ConsolidatedBias == AgentDirectionalBias.InsufficientData
                ? TradingDecisionType.InsufficientData
                : TradingDecisionType.Monitor;
            warnings.Add(new TradingDecisionWarning("NO_DIRECTIONAL_BIAS", "The consensus does not support a directional decision."));
            return Outcome(neutralType, primary, alternatives, null, null, [], warnings, errors, consensus.Confidence.Score);
        }

        if (consensus.Level == ConsensusLevel.Weak)
        {
            warnings.Add(new TradingDecisionWarning("WEAK_CONSENSUS", "Weak consensus is insufficient for a directional setup."));
            return Outcome(TradingDecisionType.Wait, primary, alternatives, null, null, [], warnings, errors, Math.Min(consensus.Confidence.Score, 55));
        }

        if (primary is null || primary.Source.Confidence < _options.MinimumScenarioConfidence)
        {
            errors.Add(new TradingDecisionError("MISSING_DIRECTIONAL_SCENARIO", "No sufficiently confident directional scenario is available."));
            return Outcome(TradingDecisionType.Wait, primary, alternatives, null, null, [], warnings, errors, Math.Min(consensus.Confidence.Score, 55));
        }

        var invalidations = consensus.Invalidations;
        if (invalidations.Count == 0 || primary.Source.InvalidationConditions.Count == 0)
        {
            errors.Add(new TradingDecisionError("MISSING_INVALIDATION", "A directional decision requires an explicit invalidation."));
            return Outcome(TradingDecisionType.NoTrade, primary, alternatives, null, null, [], warnings, errors, Math.Min(consensus.Confidence.Score, 50));
        }

        var direction = consensus.ConsolidatedBias;
        var levels = consensus.MarketLevels
            .Where(level => level.Timeframe.Code == request.Timeframe.Code)
            .OrderBy(level => level.Type)
            .ThenBy(level => level.Price.Value)
            .ThenBy(level => string.Join("|", level.SourceRuns.Select(run => run.Value)), StringComparer.Ordinal)
            .ToArray();
        if (consensus.MarketLevels.Any(level => level.Timeframe.Code != request.Timeframe.Code))
        {
            warnings.Add(new TradingDecisionWarning("TIMEFRAME_LEVELS_IGNORED", "Levels from another timeframe were excluded from the decision."));
        }

        var entries = levels.Where(level => level.Type == AgentMarketLevelType.Entry).ToArray();
        var stops = levels.Where(level => level.Type is AgentMarketLevelType.Stop or AgentMarketLevelType.Invalidation).ToArray();
        var targets = levels.Where(level => level.Type == AgentMarketLevelType.Target).ToArray();
        if (entries.Length == 0)
        {
            errors.Add(new TradingDecisionError("MISSING_ENTRY", "No explicit Entry level is available; an entry was not invented."));
            return Outcome(TradingDecisionType.Wait, primary, alternatives, null, null, [], warnings, errors, Math.Min(consensus.Confidence.Score, 55));
        }

        if (stops.Length == 0)
        {
            errors.Add(new TradingDecisionError("MISSING_STOP", "No explicit Stop or Invalidation level is available; a stop was not invented."));
            return Outcome(TradingDecisionType.NoTrade, primary, alternatives, null, null, [], warnings, errors, Math.Min(consensus.Confidence.Score, 50));
        }

        var entryLevel = direction == AgentDirectionalBias.Bullish
            ? entries.OrderBy(level => level.Price.Value).First()
            : entries.OrderByDescending(level => level.Price.Value).First();
        var stopLevels = direction == AgentDirectionalBias.Bullish
            ? stops.OrderByDescending(level => level.Price.Value).ToArray()
            : stops.OrderBy(level => level.Price.Value).ToArray();
        var stopLevel = stopLevels[0];
        var stopIsCoherent = direction == AgentDirectionalBias.Bullish
            ? stopLevel.Price.Value < entryLevel.Price.Value
            : stopLevel.Price.Value > entryLevel.Price.Value;
        var allStopsCoherent = stops.All(level => direction == AgentDirectionalBias.Bullish
            ? level.Price.Value < entryLevel.Price.Value
            : level.Price.Value > entryLevel.Price.Value);
        if (!stopIsCoherent || !allStopsCoherent)
        {
            errors.Add(new TradingDecisionError("INCOHERENT_STOP", "The explicit stop levels are not coherent with the decision direction."));
            return Outcome(TradingDecisionType.NoTrade, primary, alternatives, null, null, [], warnings, errors, Math.Min(consensus.Confidence.Score, 45));
        }

        var targetLevels = direction == AgentDirectionalBias.Bullish
            ? targets.OrderBy(level => level.Price.Value).ToArray()
            : targets.OrderByDescending(level => level.Price.Value).ToArray();
        var allTargetsCoherent = targetLevels.All(level => direction == AgentDirectionalBias.Bullish
            ? level.Price.Value > entryLevel.Price.Value
            : level.Price.Value < entryLevel.Price.Value);
        if (!allTargetsCoherent)
        {
            errors.Add(new TradingDecisionError("INCOHERENT_TARGETS", "The explicit target levels are not ordered coherently with the decision direction."));
            return Outcome(TradingDecisionType.NoTrade, primary, alternatives, null, null, [], warnings, errors, Math.Min(consensus.Confidence.Score, 45));
        }

        var entry = new EntryProposal(
            request.Instrument,
            request.Timeframe,
            entryLevel.Price,
            entryLevel.Type,
            entryLevel.SourceRuns,
            entryLevel.References,
            "Selected from an explicit consensus Entry level.");
        var stop = new StopProposal(
            request.Instrument,
            request.Timeframe,
            stopLevel.Price,
            stopLevel.Type,
            stopLevel.SourceRuns,
            stopLevel.References,
            "Selected from an explicit consensus invalidation level.");
        var targetProposals = targetLevels
            .Take(_options.MaximumTargets)
            .Select((level, index) => new TargetProposal(
                request.Instrument,
                request.Timeframe,
                level.Price,
                index + 1,
                level.SourceRuns,
                level.References,
                "Selected from an explicit consensus Target level."))
            .ToArray();
        if (targetLevels.Length > targetProposals.Length)
        {
            warnings.Add(new TradingDecisionWarning("TARGETS_TRUNCATED", "Additional explicit targets were omitted at the configured limit."));
        }

        var type = direction == AgentDirectionalBias.Bullish
            ? TradingDecisionType.LongSetup
            : TradingDecisionType.ShortSetup;
        return Outcome(type, primary, alternatives, entry, stop, targetProposals, warnings, errors, CalculateScore(consensus, primary, true));
    }

    private List<DecisionScenario> SelectScenarios(
        TradingDecisionRequest request,
        ICollection<TradingDecisionWarning> warnings,
        out DecisionScenario? primary)
    {
        var consensus = request.Consensus;
        var candidates = consensus.Scenarios
            .Select(scenario => new
            {
                Scenario = scenario,
                Score = ScenarioScore(scenario),
                DirectionMatch = scenario.Direction == consensus.ConsolidatedBias
            })
            .OrderByDescending(item => item.DirectionMatch)
            .ThenByDescending(item => item.Score)
            .ThenBy(item => item.Scenario.Direction)
            .ThenBy(item => item.Scenario.Id, StringComparer.Ordinal)
            .ThenBy(item => string.Join("|", item.Scenario.SourceRuns.Select(run => run.Value)), StringComparer.Ordinal)
            .ToArray();
        var selected = candidates.FirstOrDefault(item => item.DirectionMatch)
            ?? candidates.FirstOrDefault();
        primary = selected is null
            ? null
            : new DecisionScenario(selected.Scenario, true, selected.Score, selected.DirectionMatch ? "Direction matches the consolidated consensus." : "Fallback scenario retained for traceability.");
        var alternatives = candidates
            .Where(item => selected is null || item.Scenario.Id != selected.Scenario.Id || !item.DirectionMatch)
            .Take(_options.MaximumAlternativeScenarios)
            .Select(item => new DecisionScenario(
                item.Scenario,
                false,
                item.Score,
                item.DirectionMatch ? "Alternative with the same consolidated direction." : "Alternative direction preserved as a material minority."))
            .ToList();
        if (candidates.Length > (selected is null ? 0 : 1) + alternatives.Count)
        {
            warnings.Add(new TradingDecisionWarning("SCENARIOS_TRUNCATED", "Additional scenarios were omitted at the configured limit."));
        }

        return alternatives;
    }

    private double ScenarioScore(ConsensusScenario scenario) => Math.Clamp(
        scenario.Confidence * 0.7
        + (scenario.ActivationConditions.Count > 0 ? 10 : 0)
        + (scenario.InvalidationConditions.Count > 0 ? 15 : 0)
        + (scenario.LevelIds.Count > 0 ? 5 : 0),
        0,
        100);

    private static double CalculateScore(ConsensusResult consensus, DecisionScenario? scenario, bool levelsCoherent) => Math.Clamp(
        consensus.Confidence.Score * 0.65
        + (scenario?.Source.Confidence ?? 0) * 0.2
        + (levelsCoherent ? 15 : 0),
        0,
        100);

    private static ValueTask<TradingDecisionPolicyOutcome> Outcome(
        TradingDecisionType type,
        DecisionScenario? primary,
        IReadOnlyCollection<DecisionScenario> alternatives,
        EntryProposal? entry,
        StopProposal? stop,
        IReadOnlyCollection<TargetProposal> targets,
        IReadOnlyCollection<TradingDecisionWarning> warnings,
        IReadOnlyCollection<TradingDecisionError> errors,
        double score) => ValueTask.FromResult(
            new TradingDecisionPolicyOutcome(type, primary, alternatives, entry, stop, targets, warnings, errors, score));
}
