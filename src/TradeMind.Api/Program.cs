using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using TradeMind.Api.Composition;
using TradeMind.Api.Endpoints;
using TradeMind.Api.Errors;
using TradeMind.Api.Middleware;
using TradeMind.Api.OpenApi;
using TradeMind.ExecutionSessions.Infrastructure;
using TradeMind.ExecutionSessions.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTradeMindCore(builder.Configuration);
builder.Services.AddTradeMindApi(builder.Configuration);
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

app.UseExceptionHandler(errorApp => errorApp.Run(ApiExceptionHandler.WriteAsync));
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestTimeoutMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExecutionSessionHeaderMiddleware>();
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
app.MapExecutionSessionEndpoints();

await app.RunAsync();

public partial class Program;
