using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Domain;

namespace TradeMind.AI.ExpertAgents.Application;

public sealed record AgentResultValidationResult
{
    public AgentResultValidationResult(
        bool isValid,
        AgentAnalysisResult? result,
        IReadOnlyCollection<AgentError>? errors = null)
    {
        IsValid = isValid;
        Result = result;
        Errors = Array.AsReadOnly(errors?.ToArray() ?? []);
    }

    public bool IsValid { get; }
    public AgentAnalysisResult? Result { get; }
    public IReadOnlyList<AgentError> Errors { get; }
}

public interface IAgentAnalysisResultValidator
{
    AgentResultValidationResult ValidateAndNormalize(
        AgentDescriptor descriptor,
        MarketContext context,
        AgentExecutionRequest request,
        AgentAnalysisResult result);
}

public sealed class AgentAnalysisResultValidator(
    Microsoft.Extensions.Options.IOptions<ExpertAgentOptions> options) : IAgentAnalysisResultValidator
{
    private readonly ExpertAgentOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public AgentResultValidationResult ValidateAndNormalize(
        AgentDescriptor descriptor,
        MarketContext context,
        AgentExecutionRequest request,
        AgentAnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var errors = new List<AgentError>();
        if (result.AgentRunId != request.AgentRunId)
        {
            errors.Add(new(AgentErrorCode.InvalidAgentResult, "The result AgentRunId does not match the execution request."));
        }

        if (result.AgentId != descriptor.Id)
        {
            errors.Add(new(AgentErrorCode.InvalidAgentResult, "The result AgentId does not match the resolved agent."));
        }

        if (result.AgentVersion != descriptor.Version)
        {
            errors.Add(new(AgentErrorCode.InvalidAgentResult, "The result agent version does not match the resolved agent."));
        }

        if (result.MarketContextId != context.Id || result.MarketContextId != request.MarketContextId)
        {
            errors.Add(new(AgentErrorCode.InvalidAgentResult, "The result MarketContextId does not match the analyzed context."));
        }

        if (result.SchemaVersion != descriptor.ResultSchemaVersion)
        {
            errors.Add(new(AgentErrorCode.InvalidAgentResult, "The result schema version is not supported by the descriptor."));
        }

        if (result.StartedAtUtc.Offset != TimeSpan.Zero
            || result.CompletedAtUtc.Offset != TimeSpan.Zero
            || result.CompletedAtUtc < result.StartedAtUtc)
        {
            errors.Add(new(AgentErrorCode.InvalidAgentResult, "Result timestamps must be ordered UTC values."));
        }

        if (result.Status is AgentAnalysisStatus.Succeeded or AgentAnalysisStatus.PartiallySucceeded
            && result.Errors.Any(error => error.Fatal))
        {
            errors.Add(new(AgentErrorCode.InvalidAgentResult, "A successful result cannot contain fatal errors."));
        }

        if (result.Status == AgentAnalysisStatus.Failed && result.Errors.Count == 0)
        {
            errors.Add(new(AgentErrorCode.InvalidAgentResult, "A failed result must contain at least one structured error."));
        }

        ValidateReferences(context, result, errors);
        ValidateLevelAndScenarioIds(result, errors);

        if (errors.Count > 0)
        {
            return new AgentResultValidationResult(false, null, errors);
        }

        var warnings = new List<AgentWarning>();
        AgentAnalysisResult normalized;
        try
        {
            normalized = Normalize(result, warnings, errors);
        }
        catch (AgentResultLimitException exception)
        {
            errors.Add(new(AgentErrorCode.InvalidAgentResult, exception.Message));
            return new AgentResultValidationResult(false, null, errors);
        }
        if (errors.Count > 0)
        {
            return new AgentResultValidationResult(false, null, errors);
        }

        return new AgentResultValidationResult(true, normalized, []);
    }

    private AgentAnalysisResult Normalize(
        AgentAnalysisResult result,
        List<AgentWarning> warnings,
        List<AgentError> errors)
    {
        var observations = NormalizeCollection(
            result.Observations,
            _options.MaximumObservations,
            "observations",
            warnings,
            value => new AgentObservation(
                value.Type,
                value.Importance,
                Limit(value.Description, "observation description", warnings),
                value.Instrument,
                value.Timeframe,
                value.IntervalStartUtc,
                value.IntervalEndUtc,
                value.References,
                value.Tags));
        var evidence = NormalizeCollection(
            result.Evidence,
            _options.MaximumEvidence,
            "evidence",
            warnings,
            value => new AgentEvidence(
                value.SourceCategory,
                value.SourceReference,
                Limit(value.Explanation, "evidence explanation", warnings),
                value.Weight,
                value.Freshness,
                value.SourceTimestampUtc));
        var marketLevels = NormalizeCollection(
            result.MarketLevels,
            _options.MaximumMarketLevels,
            "market levels",
            warnings,
            value => new AgentMarketLevel(
                value.Id,
                value.Type,
                value.Price,
                value.Timeframe,
                value.Importance,
                Limit(value.Reason, "market level reason", warnings),
                value.LowerBound,
                value.UpperBound,
                value.References));
        var scenarios = NormalizeCollection(
            result.Scenarios,
            _options.MaximumScenarios,
            "scenarios",
            warnings,
            value => new AgentScenario(
                value.Id,
                Limit(value.Title, "scenario title", warnings),
                Limit(value.Description, "scenario description", warnings),
                value.Direction,
                value.ActivationConditions,
                value.InvalidationConditions,
                value.LevelIds,
                value.Risks,
                value.Horizon,
                value.Confidence,
                value.References));

        var invalidations = NormalizeStrings(result.Invalidations, _options.MaximumInvalidations, "invalidations", warnings);
        var risks = NormalizeStrings(result.Risks, _options.MaximumRisks, "risks", warnings);
        var limitations = NormalizeStrings(result.Limitations, _options.MaximumLimitations, "limitations", warnings);
        var summary = result.Summary is null
            ? null
            : Limit(result.Summary, "summary", warnings, _options.MaximumSummaryCharacters);
        var metadata = NormalizeMetadata(result.Metadata, warnings);
        var normalizedWarnings = NormalizeWarnings(result.Warnings.Concat(warnings));
        var normalizedErrors = NormalizeErrors(result.Errors, errors);

        if (errors.Count > 0)
        {
            return result;
        }

        return new AgentAnalysisResult(
            result.AgentRunId,
            result.AgentId,
            result.AgentVersion,
            result.MarketContextId,
            result.Status,
            result.StartedAtUtc,
            result.CompletedAtUtc,
            result.DirectionalBias,
            result.Confidence,
            summary,
            observations,
            evidence,
            marketLevels,
            scenarios,
            invalidations,
            risks,
            normalizedWarnings,
            limitations,
            normalizedErrors,
            result.SchemaVersion,
            metadata);
    }

    private IReadOnlyList<T> NormalizeCollection<T>(
        IReadOnlyList<T> values,
        int maximum,
        string label,
        List<AgentWarning> warnings,
        Func<T, T> normalize)
    {
        var selected = values.Take(maximum).Select(normalize).ToArray();
        if (values.Count > maximum)
        {
            if (!_options.TruncateExcessCollections)
            {
                throw new AgentResultLimitException($"The result exceeds the maximum number of {label}.");
            }

            warnings.Add(new("RESULT_TRUNCATED", $"Result was deterministically truncated to {maximum} {label}."));
        }

        return Array.AsReadOnly(selected);
    }

    private IReadOnlyList<string> NormalizeStrings(
        IReadOnlyList<string> values,
        int maximum,
        string label,
        List<AgentWarning> warnings)
    {
        if (values.Count > maximum && !_options.TruncateExcessCollections)
        {
            throw new AgentResultLimitException($"The result exceeds the maximum number of {label}.");
        }

        if (values.Count > maximum)
        {
            warnings.Add(new("RESULT_TRUNCATED", $"Result was deterministically truncated to {maximum} {label}."));
        }

        return Array.AsReadOnly(values.Take(maximum).ToArray());
    }

    private IReadOnlyDictionary<string, string> NormalizeMetadata(
        IReadOnlyDictionary<string, string> metadata,
        List<AgentWarning> warnings)
    {
        var ordered = metadata
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(_options.MaximumMetadataEntries)
            .ToDictionary(
                pair => pair.Key,
                pair => Limit(pair.Value, "metadata value", warnings, _options.MaximumMetadataValueCharacters),
                StringComparer.OrdinalIgnoreCase);
        if (metadata.Count > _options.MaximumMetadataEntries)
        {
            if (!_options.TruncateExcessCollections)
            {
                throw new AgentResultLimitException("The result exceeds the maximum metadata entry count.");
            }

            warnings.Add(new("RESULT_TRUNCATED", $"Result metadata was deterministically truncated to {_options.MaximumMetadataEntries} entries."));
        }

        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(ordered);
    }

    private IReadOnlyList<AgentWarning> NormalizeWarnings(IEnumerable<AgentWarning> values)
    {
        var materialized = values.ToArray();
        if (materialized.Length <= _options.MaximumWarnings)
        {
            return Array.AsReadOnly(materialized);
        }

        if (!_options.TruncateExcessCollections)
        {
            throw new AgentResultLimitException("The result exceeds the maximum warning count.");
        }

        var selectedCount = Math.Max(0, _options.MaximumWarnings - 1);
        return Array.AsReadOnly(materialized
            .Take(selectedCount)
            .Append(new AgentWarning("RESULT_TRUNCATED", $"Result warnings were deterministically truncated to {_options.MaximumWarnings} entries."))
            .ToArray());
    }

    private IReadOnlyList<AgentError> NormalizeErrors(
        IReadOnlyList<AgentError> values,
        List<AgentError> errors)
    {
        if (values.Count <= _options.MaximumErrors)
        {
            return Array.AsReadOnly(values.ToArray());
        }

        if (!_options.TruncateExcessCollections)
        {
            errors.Add(new(AgentErrorCode.InvalidAgentResult, "The result exceeds the maximum error count."));
            return [];
        }

        return Array.AsReadOnly(values.Take(_options.MaximumErrors).ToArray());
    }

    private string Limit(string value, string label, List<AgentWarning> warnings, int? maximum = null)
    {
        var limit = maximum ?? _options.MaximumDescriptionCharacters;
        if (value.Length <= limit)
        {
            return value;
        }

        if (!_options.TruncateExcessCollections)
        {
            throw new AgentResultLimitException($"The result exceeds the maximum length for {label}.");
        }

        warnings.Add(new("RESULT_TRUNCATED", $"The {label} was deterministically truncated to {limit} characters."));
        return value[..limit];
    }

    private static void ValidateReferences(
        MarketContext context,
        AgentAnalysisResult result,
        ICollection<AgentError> errors)
    {
        var knownReferences = context.Traces
            .SelectMany(trace => trace.References)
            .ToHashSet();
        var references = result.Observations.SelectMany(observation => observation.References)
            .Concat(result.Evidence.Select(evidence => evidence.SourceReference))
            .Concat(result.MarketLevels.SelectMany(level => level.References))
            .Concat(result.Scenarios.SelectMany(scenario => scenario.References));
        if (references.Any(reference => !knownReferences.Contains(reference)))
        {
            errors.Add(new(AgentErrorCode.InvalidAgentResult, "The result contains a reference to an unknown context source."));
        }
    }

    private static void ValidateLevelAndScenarioIds(
        AgentAnalysisResult result,
        ICollection<AgentError> errors)
    {
        if (result.MarketLevels.GroupBy(level => level.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            errors.Add(new(AgentErrorCode.InvalidAgentResult, "The result contains duplicate market level identifiers."));
        }

        var levelIds = result.MarketLevels.Select(level => level.Id).ToHashSet(StringComparer.Ordinal);
        if (result.Scenarios.SelectMany(scenario => scenario.LevelIds).Any(levelId => !levelIds.Contains(levelId)))
        {
            errors.Add(new(AgentErrorCode.InvalidAgentResult, "A scenario references an unknown market level."));
        }
    }
}

public sealed class AgentResultLimitException(string message) : InvalidOperationException(message);
