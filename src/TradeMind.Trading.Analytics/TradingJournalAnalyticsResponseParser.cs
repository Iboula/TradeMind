using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics;

public sealed class TradingJournalAnalyticsResponseParser : ITradingJournalAnalyticsResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly TradingJournalAnalyticsOptions _options;

    public TradingJournalAnalyticsResponseParser(IOptions<TradingJournalAnalyticsOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public TradingJournalAIInterpretation Parse(
        string response,
        string requestedLanguage,
        Guid analysisId,
        string? correlationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedLanguage);
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(response);
            using var document = JsonDocument.Parse(response, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32
            });
            EnsureNoDuplicateProperties(document.RootElement);
            var model = JsonSerializer.Deserialize<ResponseModel>(response, JsonOptions)
                ?? throw new JsonException("Missing response object.");
            Validate(model, requestedLanguage);
            var recommendations = model.Recommendations!.Select(item => new TradingJournalAnalyticsRecommendation(
                item.Code!, item.Text!, item.RelatedFindingCodes)).ToArray();
            var interpretation = new TradingJournalAIInterpretation(
                model.Summary!,
                model.Explanations,
                recommendations,
                model.NextReviewChecklist,
                model.Disclaimer!,
                model.Language!);
            if (AllText(interpretation).Any(TradingCoachSafetyPolicy.ContainsProhibitedContent))
            {
                throw new JsonException("Unsafe response content.");
            }

            return interpretation;
        }
        catch (Exception exception) when (exception is not TradingJournalAnalyticsParsingException)
        {
            throw new TradingJournalAnalyticsParsingException(analysisId, correlationId, exception);
        }
    }

    private void Validate(ResponseModel model, string requestedLanguage)
    {
        Require(model.Summary);
        Require(model.Disclaimer);
        Require(model.Language);
        ArgumentNullException.ThrowIfNull(model.Explanations);
        ArgumentNullException.ThrowIfNull(model.Recommendations);
        ArgumentNullException.ThrowIfNull(model.NextReviewChecklist);
        if (model.Explanations.Count > 20
            || model.Recommendations.Count > _options.MaximumRecommendations
            || model.NextReviewChecklist.Count > _options.MaximumChecklistItems)
        {
            throw new JsonException("Response collection limit exceeded.");
        }

        if (!LanguageMatches(requestedLanguage, model.Language!))
        {
            throw new JsonException("Response language does not match the request.");
        }

        foreach (var recommendation in model.Recommendations)
        {
            Require(recommendation.Code);
            Require(recommendation.Text);
            ArgumentNullException.ThrowIfNull(recommendation.RelatedFindingCodes);
        }
    }

    private static bool LanguageMatches(string requested, string actual)
    {
        var requestedCode = requested.Trim().Split('-', '_')[0];
        var actualCode = actual.Trim().Split('-', '_')[0];
        return string.Equals(requestedCode, actualCode, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureNoDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException("Duplicate JSON property.");
                }

                EnsureNoDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                EnsureNoDuplicateProperties(item);
            }
        }
    }

    private static IEnumerable<string> AllText(TradingJournalAIInterpretation interpretation)
    {
        yield return interpretation.Summary;
        yield return interpretation.Disclaimer;
        foreach (var value in interpretation.Explanations.Concat(interpretation.NextReviewChecklist))
        {
            yield return value;
        }

        foreach (var recommendation in interpretation.Recommendations)
        {
            yield return recommendation.Text;
        }
    }

    private static void Require(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("A required field is missing.");
        }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ResponseModel
    {
        public string? Summary { get; init; }
        public List<string>? Explanations { get; init; }
        public List<RecommendationModel>? Recommendations { get; init; }
        public List<string>? NextReviewChecklist { get; init; }
        public string? Disclaimer { get; init; }
        public string? Language { get; init; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class RecommendationModel
    {
        public string? Code { get; init; }
        public string? Text { get; init; }
        public List<string>? RelatedFindingCodes { get; init; }
    }
}
