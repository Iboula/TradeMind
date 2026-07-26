using System.Text.Json;

namespace TradeMind.Trading.Coaching;

public sealed class TradingCoachResponseParser : ITradingCoachResponseParser
{
    private static readonly HashSet<string> RootProperties = new(
    [
        "summary", "dataQuality", "strengths", "ruleViolations", "riskObservations",
        "executionObservations", "psychologyObservations", "missingInformation",
        "priorityIssues", "recommendedActions", "nextTradeChecklist", "scores", "disclaimer"
    ], StringComparer.Ordinal);

    public TradingCoachAnalysis Parse(
        string response,
        Guid analysisId,
        DateTimeOffset generatedAtUtc,
        string agentVersion,
        string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new TradingCoachResponseParsingException(analysisId, correlationId);
        }

        try
        {
            using var document = JsonDocument.Parse(response, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32
            });

            var root = document.RootElement;
            RequireObject(root);
            RequireExactProperties(root, RootProperties);
            var scores = ParseScores(Required(root, "scores"));

            return new TradingCoachAnalysis(
                RequiredString(root, "summary"),
                RequiredString(root, "dataQuality"),
                StringList(root, "strengths"),
                FindingList(root, "ruleViolations"),
                StringList(root, "riskObservations"),
                StringList(root, "executionObservations"),
                StringList(root, "psychologyObservations"),
                StringList(root, "missingInformation"),
                FindingList(root, "priorityIssues"),
                ActionList(root, "recommendedActions"),
                StringList(root, "nextTradeChecklist"),
                scores,
                RequiredString(root, "disclaimer"),
                generatedAtUtc,
                agentVersion,
                analysisId);
        }
        catch (TradingCoachResponseParsingException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            throw new TradingCoachResponseParsingException(analysisId, correlationId, exception);
        }
    }

    private static TradingCoachScores ParseScores(JsonElement element)
    {
        RequireObject(element);
        RequireExactProperties(element, new HashSet<string>(
        [
            "planAdherence", "riskDiscipline", "executionQuality", "emotionalControl",
            "journalCompleteness", "overallProcessQuality", "explanations"
        ], StringComparer.Ordinal));

        var explanations = Required(element, "explanations");
        RequireArray(explanations);
        var parsedExplanations = explanations.EnumerateArray().Select(ParseExplanation).ToArray();
        return new TradingCoachScores(
            RequiredScore(element, "planAdherence"),
            RequiredScore(element, "riskDiscipline"),
            RequiredScore(element, "executionQuality"),
            RequiredScore(element, "emotionalControl"),
            RequiredScore(element, "journalCompleteness"),
            RequiredScore(element, "overallProcessQuality"),
            parsedExplanations);
    }

    private static TradingCoachScoreExplanation ParseExplanation(JsonElement element)
    {
        RequireObject(element);
        RequireExactProperties(element, new HashSet<string>(
            ["scoreName", "value", "factors", "confidence"], StringComparer.Ordinal));
        var confidenceElement = Required(element, "confidence");
        if (confidenceElement.ValueKind != JsonValueKind.Number || !confidenceElement.TryGetDecimal(out var confidence))
        {
            throw Invalid();
        }

        return new TradingCoachScoreExplanation(
            RequiredString(element, "scoreName"),
            RequiredScore(element, "value"),
            StringList(element, "factors"),
            confidence,
            false);
    }

    private static IReadOnlyCollection<TradingCoachFinding> FindingList(JsonElement parent, string name)
    {
        var element = Required(parent, name);
        RequireArray(element);
        return element.EnumerateArray().Select(ParseFinding).ToArray();
    }

    private static TradingCoachFinding ParseFinding(JsonElement element)
    {
        RequireObject(element);
        RequireExactProperties(element, new HashSet<string>(
            ["code", "category", "message", "severity"], StringComparer.Ordinal));
        if (!Enum.TryParse<TradingCoachFindingSeverity>(RequiredString(element, "severity"), true, out var severity))
        {
            throw Invalid();
        }

        return new TradingCoachFinding(
            RequiredString(element, "code"),
            RequiredString(element, "category"),
            RequiredString(element, "message"),
            severity,
            false);
    }

    private static IReadOnlyCollection<TradingCoachRecommendedAction> ActionList(JsonElement parent, string name)
    {
        var element = Required(parent, name);
        RequireArray(element);
        return element.EnumerateArray().Select(action =>
        {
            RequireObject(action);
            RequireExactProperties(action, new HashSet<string>(
                ["code", "action", "relatedFindingCodes"], StringComparer.Ordinal));
            return new TradingCoachRecommendedAction(
                RequiredString(action, "code"),
                RequiredString(action, "action"),
                StringList(action, "relatedFindingCodes"));
        }).ToArray();
    }

    private static IReadOnlyCollection<string> StringList(JsonElement parent, string name)
    {
        var element = Required(parent, name);
        RequireArray(element);
        var values = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw Invalid();
            }

            values.Add(item.GetString()!);
        }

        return values;
    }

    private static int RequiredScore(JsonElement parent, string name)
    {
        var element = Required(parent, name);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value) || value is < 0 or > 100)
        {
            throw Invalid();
        }

        return value;
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        var element = Required(parent, name);
        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw Invalid();
        }

        return element.GetString()!;
    }

    private static JsonElement Required(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            throw Invalid();
        }

        return value;
    }

    private static void RequireObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Invalid();
        }
    }

    private static void RequireArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw Invalid();
        }
    }

    private static void RequireExactProperties(JsonElement element, IReadOnlySet<string> expected)
    {
        var names = element.EnumerateObject().Select(property => property.Name).ToArray();
        if (names.Length != expected.Count
            || names.Distinct(StringComparer.Ordinal).Count() != names.Length
            || names.Any(name => !expected.Contains(name))
            || expected.Any(name => !names.Contains(name, StringComparer.Ordinal)))
        {
            throw Invalid();
        }
    }

    private static InvalidOperationException Invalid() => new("Structured response validation failed.");
}
