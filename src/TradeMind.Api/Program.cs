using TradeMind.Modules.KnowledgeHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddKnowledgeHub(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.MapOpenApi();
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new
{
    service = "TradeMind.Api",
    architecture = "DDD Modular Monolith",
    runtime = ".NET 9"
}));

app.MapGroup("/api/trading-journal")
    .WithTags("Trading Journal")
    .MapGet("/status", () => Results.Ok(new { module = "TradingJournal", status = "available" }));

app.MapGroup("/api/risk-management")
    .WithTags("Risk Management")
    .MapGet("/status", () => Results.Ok(new { module = "RiskManagement", status = "available" }));

await app.ApplyKnowledgeHubMigrationsAsync();
app.MapKnowledgeHubEndpoints();

app.Run();

public partial class Program;
