using System.Text.Json;

namespace TradeMind.AI.Tools;

public sealed class EchoAITool : IAITool
{
    public AIToolDefinition Definition { get; } = new(
        new AIToolId("echo"),
        "Echo",
        "Returns the supplied text as structured data for development verification.",
        "1.0",
        [
            new AIToolParameterDefinition(
                "text",
                AIToolParameterType.String,
                required: true,
                "Text to return.",
                maxLength: 1_000)
        ],
        sideEffectLevel: AIToolSideEffectLevel.None,
        isIdempotent: true,
        defaultTimeout: TimeSpan.FromSeconds(2),
        availability: AIToolAvailability.DevelopmentOnly,
        tags: ["development", "deterministic"]);

    public Task<AIToolExecutionResult> ExecuteAsync(
        AIToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = context.TimeProvider.GetUtcNow();
        var output = JsonSerializer.SerializeToElement(new { text = context.GetRequiredString("text") });
        return Task.FromResult(AIToolExecutionResult.Succeeded(
            Definition.Id,
            output,
            now,
            now,
            idempotencyKey: context.Request.IdempotencyKey));
    }
}

public sealed class AddNumbersAITool : IAITool
{
    public AIToolDefinition Definition { get; } = new(
        new AIToolId("add-numbers"),
        "Add numbers",
        "Adds two decimal values and returns their deterministic sum.",
        "1.0",
        [
            new AIToolParameterDefinition(
                "left",
                AIToolParameterType.Decimal,
                required: true,
                "Left operand."),
            new AIToolParameterDefinition(
                "right",
                AIToolParameterType.Decimal,
                required: true,
                "Right operand.")
        ],
        sideEffectLevel: AIToolSideEffectLevel.None,
        isIdempotent: true,
        defaultTimeout: TimeSpan.FromSeconds(2),
        availability: AIToolAvailability.Enabled,
        tags: ["deterministic", "math"]);

    public Task<AIToolExecutionResult> ExecuteAsync(
        AIToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = context.TimeProvider.GetUtcNow();
        var output = JsonSerializer.SerializeToElement(new
        {
            sum = context.GetRequiredDecimal("left") + context.GetRequiredDecimal("right")
        });

        return Task.FromResult(AIToolExecutionResult.Succeeded(
            Definition.Id,
            output,
            now,
            now,
            idempotencyKey: context.Request.IdempotencyKey));
    }
}
