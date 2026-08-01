using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using TradeMind.Api.Composition;
using TradeMind.Api.Authentication;
using TradeMind.Api.Endpoints;
using TradeMind.Api.Errors;
using TradeMind.Api.Middleware;
using TradeMind.Api.OpenApi;
using TradeMind.ExecutionSessions.Infrastructure;
using TradeMind.ExecutionSessions.Infrastructure.Persistence;
using TradeMind.Identity.Infrastructure;
using TradeMind.Observability.OpenTelemetry;
using TradeMind.Api.Health;
using TradeMind.Brokers.Application;
using TradeMind.Brokers.Infrastructure;
using TradeMind.Brokers.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTradeMindCore(builder.Configuration);
builder.Services.AddTradeMindIdentity(builder.Configuration, builder.Environment);
builder.Services.AddTradeMindApi(builder.Configuration);
builder.Services.AddTradeMindObservability(builder.Configuration, builder.Environment.EnvironmentName);
builder.Services.AddTradeMindBrokersApplication(builder.Configuration);
builder.Services.AddTradeMindBrokersInfrastructure(builder.Configuration);
builder.Services.AddTradeMindOpenApi(builder.Configuration);
builder.WebHost.ConfigureKestrel((context, options) =>
{
    var apiOptions = context.Configuration
        .GetSection("TradeMind:Api")
        .Get<ApiOptions>() ?? new ApiOptions();
    options.Limits.MaxRequestBodySize = apiOptions.PayloadLimits.MaximumBodyBytes;
    options.AddServerHeader = false;
});

var app = builder.Build();

var persistenceOptions = app.Configuration
    .GetSection(ExecutionSessionsPersistenceOptions.SectionName)
    .Get<ExecutionSessionsPersistenceOptions>() ?? new ExecutionSessionsPersistenceOptions();
if (string.Equals(persistenceOptions.Provider, "PostgreSql", StringComparison.OrdinalIgnoreCase)
    && persistenceOptions.ApplyMigrationsOnStartup)
{
    await app.Services.ApplyExecutionSessionsMigrationsAsync();
}

var identityOptions = app.Configuration
    .GetSection("TradeMind:Identity")
    .Get<TradeMind.Identity.Application.IdentityOptions>() ?? new();
if (identityOptions.ApplyMigrationsOnStartup
    && !string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Identity")))
{
    await app.Services.ApplyIdentityMigrationsAsync();
}
var brokerPersistenceOptions = app.Configuration.GetSection("TradeMind:Brokers:Persistence").Get<BrokerPersistenceOptions>() ?? new BrokerPersistenceOptions();
if (brokerPersistenceOptions.Enabled && brokerPersistenceOptions.ApplyMigrationsOnStartup && !string.IsNullOrWhiteSpace(brokerPersistenceOptions.ConnectionString))
{
    await app.Services.ApplyBrokerMigrationsAsync();
}
app.Services.GetRequiredService<StartupHealthCheckState>().MarkReady();

app.UseExceptionHandler(errorApp => errorApp.Run(ApiExceptionHandler.WriteAsync));
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestTimeoutMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.Use(async (context, next) =>
{
    await next().ConfigureAwait(false);
    if (context.Response.StatusCode is 401 or 403 && !context.Response.HasStarted)
    {
        context.Response.Headers.Remove("Content-Length");
        var title = context.Response.StatusCode == StatusCodes.Status401Unauthorized ? "Authentication required" : "Forbidden";
        var detail = context.Response.StatusCode == StatusCodes.Status401Unauthorized
            ? "A valid identity credential is required to access this resource."
            : "The authenticated actor is not permitted to perform this operation.";
        await ApiProblemDetails.WriteAsync(context, context.Response.StatusCode, title, detail).ConfigureAwait(false);
    }
});
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<ExecutionSessionHeaderMiddleware>();
app.UseMiddleware<RequestTelemetryMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

var apiOptions = app.Services.GetRequiredService<IOptions<ApiOptions>>().Value;
if (apiOptions.Security.EnableHttpsRedirection && !app.Environment.IsEnvironment("Test"))
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

if (apiOptions.Security.EnableSecurityHeaders)
{
    app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            return Task.CompletedTask;
        });
        await next().ConfigureAwait(false);
    });
}

if (apiOptions.OpenApi.Enabled && !app.Environment.IsProduction())
{
    app.MapOpenApi();
}

app.MapSystemEndpoints();
app.MapHealthEndpoints();
var observabilityOptions = app.Configuration
    .GetSection(OpenTelemetryOptions.SectionName)
    .Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions();
if (observabilityOptions.Enabled && observabilityOptions.Metrics.Enabled && observabilityOptions.Metrics.Prometheus.Enabled)
{
    app.MapPrometheusScrapingEndpoint(observabilityOptions.Metrics.Prometheus.Endpoint);
}
app.MapMarketContextEndpoints();
app.MapExpertEndpoints();
app.MapConsensusEndpoints();
app.MapTradingDecisionEndpoints();
app.MapRiskEndpoints();
app.MapTradingPlanEndpoints();
app.MapTradingWorkspaceEndpoints();
app.MapTradingAssistantEndpoints();
app.MapPaperTradingEndpoints();
app.MapKnowledgeEndpoints();
app.MapIdentityEndpoints();
app.MapExecutionSessionEndpoints();
app.MapBrokerEndpoints();

await app.RunAsync();

public partial class Program;
