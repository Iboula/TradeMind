using TradeMind.Modules.Knowledge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddKnowledgeModule(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.MapOpenApi();
app.MapHealthChecks("/health");

await app.Services.ApplyKnowledgeMigrationsAsync();

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

app.MapKnowledgeEndpoints();

app.Run();

public partial class Program;
