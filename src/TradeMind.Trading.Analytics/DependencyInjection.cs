using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Agents;
using TradeMind.AI.Application;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindTradingAnalytics(
        this IServiceCollection services,
        Action<TradingJournalAnalyticsOptions>? configure = null,
        Action<TradingJournalAnalyticsSafetyOptions>? configureSafety = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);

        var optionsBuilder = services.AddOptions<TradingJournalAnalyticsOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder.ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IValidateOptions<TradingJournalAnalyticsOptions>,
            TradingJournalAnalyticsOptionsValidator>());

        var safetyBuilder = services.AddOptions<TradingJournalAnalyticsSafetyOptions>();
        if (configureSafety is not null)
        {
            safetyBuilder.Configure(configureSafety);
        }

        safetyBuilder.ValidateOnStart();
        services.AddTradeMindTradingCoaching();
        if (!services.Any(IsTemplateRegistration))
        {
            services.AddSingleton(TradingJournalAnalyticsPromptTemplates.Analysis);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAIAgent, TradingJournalAnalyticsAgentV1>());
        services.TryAddSingleton<ITradingJournalAnalyticsValidator, TradingJournalAnalyticsValidator>();
        services.TryAddSingleton<ITradingJournalCollectionNormalizer, TradingJournalCollectionNormalizer>();
        services.TryAddSingleton<ITradingStatisticsCalculator, TradingStatisticsCalculator>();
        services.TryAddSingleton<ITradingPeriodAggregator, TradingPeriodAggregator>();
        services.TryAddSingleton<ITradingSetupAnalyzer, TradingSetupAnalyzer>();
        services.TryAddSingleton<ITradingBehaviorTrendAnalyzer, TradingBehaviorTrendAnalyzer>();
        services.TryAddSingleton<IRiskDriftAnalyzer, RiskDriftAnalyzer>();
        services.TryAddSingleton<IPostOutcomeBehaviorAnalyzer, PostOutcomeBehaviorAnalyzer>();
        services.TryAddSingleton<ITradingJournalDataQualityAnalyzer, TradingJournalDataQualityAnalyzer>();
        services.TryAddSingleton<ITradingJournalRuleAnalyzer, TradingJournalRuleAnalyzer>();
        services.TryAddSingleton<ITradingJournalAgentRequestFactory, TradingJournalAgentRequestFactory>();
        services.TryAddSingleton<ITradingJournalAnalyticsResponseParser, TradingJournalAnalyticsResponseParser>();
        services.TryAddTransient<ITradingJournalAIInterpreter, TradingJournalAIInterpreter>();
        services.TryAddSingleton<ITradingJournalAnalyticsMerger, TradingJournalAnalyticsMerger>();
        services.TryAddSingleton<ITradingJournalAnalyticsSafetyFilter, TradingJournalAnalyticsSafetyFilter>();
        services.TryAddTransient<ITradingJournalAnalyticsService, TradingJournalAnalyticsService>();
        return services;
    }

    private static bool IsTemplateRegistration(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(PromptTemplate)
        && descriptor.ImplementationInstance is PromptTemplate template
        && template.Id == TradingJournalAnalyticsPromptTemplates.Analysis.Id
        && template.Version == TradingJournalAnalyticsPromptTemplates.Analysis.Version;
}
