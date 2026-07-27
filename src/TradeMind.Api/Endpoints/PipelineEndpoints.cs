using TradeMind.Api.Application;
using TradeMind.Api.Contracts.Common;
using TradeMind.Api.Middleware;
using TradeMind.Api.Authorization;

namespace TradeMind.Api.Endpoints;

public static class PipelineEndpoints
{
    public static IEndpointRouteBuilder MapExpertEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/experts/dispatch", async (ExpertDispatchApiRequest request, HttpContext context, ITradeMindApiApplication application, CancellationToken cancellationToken) =>
            await Execute(request.SchemaVersion, request.Payload, context, application, ApiModule.ExpertDispatch, cancellationToken))
            .WithName("DispatchExperts").WithTags("Experts").RequireAuthorization(IdentityPolicies.ExpertsDispatch);
        endpoints.MapPost("/api/v1/experts/analyze", async (ExpertAnalysisApiRequest request, HttpContext context, ITradeMindApiApplication application, CancellationToken cancellationToken) =>
            await Execute(request.SchemaVersion, request.Payload, context, application, ApiModule.ExpertAnalysis, cancellationToken))
            .WithName("AnalyzeWithExpert").WithTags("Experts").RequireAuthorization(IdentityPolicies.ExpertsAnalyze);
        return endpoints;
    }

    public static IEndpointRouteBuilder MapConsensusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/consensus/build", async (ConsensusApiRequest request, HttpContext context, ITradeMindApiApplication application, CancellationToken cancellationToken) =>
            await Execute(request.SchemaVersion, request.Payload, context, application, ApiModule.Consensus, cancellationToken))
            .WithName("BuildConsensus").WithTags("Consensus").RequireAuthorization(IdentityPolicies.ConsensusBuild);
        return endpoints;
    }

    public static IEndpointRouteBuilder MapTradingDecisionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/trading-decisions/evaluate", async (TradingDecisionApiRequest request, HttpContext context, ITradeMindApiApplication application, CancellationToken cancellationToken) =>
            await Execute(request.SchemaVersion, request.Payload, context, application, ApiModule.TradingDecision, cancellationToken))
            .WithName("EvaluateTradingDecision").WithTags("Trading Decisions").RequireAuthorization(IdentityPolicies.TradingDecisionsEvaluate);
        return endpoints;
    }

    public static IEndpointRouteBuilder MapRiskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/risk/evaluate", async (RiskApiRequest request, HttpContext context, ITradeMindApiApplication application, CancellationToken cancellationToken) =>
            await Execute(request.SchemaVersion, request.Payload, context, application, ApiModule.Risk, cancellationToken))
            .WithName("EvaluateRisk").WithTags("Risk").RequireAuthorization(IdentityPolicies.RiskEvaluate);
        return endpoints;
    }

    public static IEndpointRouteBuilder MapTradingPlanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/trading-plans/generate", async (TradingPlanApiRequest request, HttpContext context, ITradeMindApiApplication application, CancellationToken cancellationToken) =>
            await Execute(request.SchemaVersion, request.Payload, context, application, ApiModule.TradingPlan, cancellationToken))
            .WithName("GenerateTradingPlan").WithTags("Trading Plans").RequireAuthorization(IdentityPolicies.TradingPlansGenerate);
        return endpoints;
    }

    public static IEndpointRouteBuilder MapTradingWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/trading-workspaces/build", async (TradingWorkspaceApiRequest request, HttpContext context, ITradeMindApiApplication application, CancellationToken cancellationToken) =>
            await Execute(request.SchemaVersion, request.Payload, context, application, ApiModule.TradingWorkspace, cancellationToken))
            .WithName("BuildTradingWorkspace").WithTags("Trading Workspace").RequireAuthorization(IdentityPolicies.TradingWorkspaceBuild)
            .WithMetadata(new IdempotencyMetadata.Required());
        return endpoints;
    }

    public static IEndpointRouteBuilder MapTradingAssistantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/trading-assistant/ask", async (TradingAssistantApiRequest request, HttpContext context, ITradeMindApiApplication application, CancellationToken cancellationToken) =>
            await Execute(request.SchemaVersion, request.Payload, context, application, ApiModule.TradingAssistant, cancellationToken))
            .WithName("AskTradingAssistant").WithTags("Trading Assistant").RequireAuthorization(IdentityPolicies.TradingAssistantAsk)
            .WithMetadata(new IdempotencyMetadata.Required());
        return endpoints;
    }

    public static IEndpointRouteBuilder MapPaperTradingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/paper-trading/simulate", async (PaperTradingApiRequest request, HttpContext context, ITradeMindApiApplication application, CancellationToken cancellationToken) =>
            await Execute(request.SchemaVersion, request.Payload, context, application, ApiModule.PaperTrading, cancellationToken))
            .WithName("SimulatePaperTrading").WithTags("Paper Trading").RequireAuthorization(IdentityPolicies.PaperTradingSimulate)
            .WithMetadata(new IdempotencyMetadata.Required());
        return endpoints;
    }

    private static async Task<IResult> Execute(
        int schemaVersion,
        System.Text.Json.JsonElement payload,
        HttpContext context,
        ITradeMindApiApplication application,
        ApiModule module,
        CancellationToken cancellationToken)
    {
        var problem = EndpointHelpers.ValidateSchema(schemaVersion, payload);
        if (problem is not null) return problem;
        return await EndpointHelpers.ExecuteModuleAsync(context, application, module, payload, cancellationToken).ConfigureAwait(false);
    }
}
