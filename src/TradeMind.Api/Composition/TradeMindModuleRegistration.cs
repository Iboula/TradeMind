using TradeMind.AI.Application;
using TradeMind.AI.Context.Infrastructure;
using TradeMind.AI.ExpertAgents.Consensus.Application;
using TradeMind.AI.ExpertAgents.Dispatch.Application;
using TradeMind.AI.ExpertAgents.Application;
using TradeMind.AI.Knowledge;
using TradeMind.AI.Memory;
using TradeMind.AI.PaperTrading.Application;
using TradeMind.AI.RiskEngine.Application;
using TradeMind.AI.TradingAssistant;
using TradeMind.AI.TradingDecisions.Application;
using TradeMind.AI.TradingPlans.Application;
using TradeMind.AI.TradingWorkspace.Application;
using TradeMind.KnowledgeHub.Infrastructure;
using TradeMind.MarketConnectors.Infrastructure;

namespace TradeMind.Api.Composition;

public static class TradeMindModuleRegistration
{
    public static IServiceCollection AddTradeMindCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddTradeMindAIOrchestration();
        services.AddTradeMindMemory();

        var knowledgeHubConfigured = !string.IsNullOrWhiteSpace(
            configuration.GetConnectionString("KnowledgeHub"));
        if (knowledgeHubConfigured)
        {
            services.AddKnowledgeHub(configuration);
            services.AddTradeMindKnowledgeRag();
        }

        services.AddTradeMindContextEngine();
        services.AddTradeMindExpertAgents();
        services.AddTradeMindExpertAgentDispatcher();
        services.AddTradeMindConsensus();
        services.AddTradeMindTradingDecisions();
        services.AddTradeMindRiskEngine();
        services.AddTradeMindTradingPlans();
        services.AddTradeMindTradingWorkspace();
        services.AddTradingAssistant();
        services.AddTradeMindPaperTrading();

        var marketConnectorsConfigured = !string.IsNullOrWhiteSpace(
            configuration.GetConnectionString("MarketConnectors"));
        if (marketConnectorsConfigured)
        {
            services.AddMarketConnectorCore(configuration);
        }

        return services;
    }
}
