using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Agents;
using TradeMind.AI.Application;

namespace TradeMind.Trading.Coaching;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindTradingCoaching(
        this IServiceCollection services,
        Action<TradingJournalValidationOptions>? configureValidation = null,
        Action<TradingCoachSafetyOptions>? configureSafety = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);

        var validationBuilder = services.AddOptions<TradingJournalValidationOptions>();
        if (configureValidation is not null)
        {
            validationBuilder.Configure(configureValidation);
        }

        validationBuilder.ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IValidateOptions<TradingJournalValidationOptions>,
            TradingJournalValidationOptionsValidator>());

        var safetyBuilder = services.AddOptions<TradingCoachSafetyOptions>();
        if (configureSafety is not null)
        {
            safetyBuilder.Configure(configureSafety);
        }

        safetyBuilder.ValidateOnStart();
        services.AddTradeMindAIAgents();
        if (!services.Any(IsTradingCoachTemplateRegistration))
        {
            services.AddSingleton(TradingCoachPromptTemplates.Analysis);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAIAgent, TradingCoachAgentV1>());
        services.TryAddSingleton<ITradingJournalValidator, TradingJournalValidator>();
        services.TryAddSingleton<ITradingJournalNormalizer, TradingJournalNormalizer>();
        services.TryAddSingleton<ITradeMetricsCalculator, TradeMetricsCalculator>();
        services.TryAddSingleton<ITradingBehaviorPatternDetector, TradingBehaviorPatternDetector>();
        services.TryAddSingleton<ITradingCoachScoreCalculator, TradingCoachScoreCalculator>();
        services.TryAddSingleton<ITradingCoachRuleAnalyzer, TradingCoachRuleAnalyzer>();
        services.TryAddSingleton<ITradingCoachResponseParser, TradingCoachResponseParser>();
        services.TryAddSingleton<ITradingCoachAnalysisMerger, TradingCoachAnalysisMerger>();
        services.TryAddSingleton<ITradingCoachSafetyFilter, TradingCoachSafetyFilter>();
        services.TryAddTransient<ITradingCoachService, TradingCoachService>();
        return services;
    }

    private static bool IsTradingCoachTemplateRegistration(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(PromptTemplate)
        && descriptor.ImplementationInstance is PromptTemplate template
        && template.Id == TradingCoachPromptTemplates.Analysis.Id
        && template.Version == TradingCoachPromptTemplates.Analysis.Version;
}
