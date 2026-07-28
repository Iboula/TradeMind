using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability.ArchitectureTests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Abstractions_do_not_reference_runtime_or_web_implementations()
    {
        var references = typeof(TelemetryContext).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.DoesNotContain(references, name => name is not null && (name.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase)
            || name.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase)
            || name.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Serilog", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Public_contracts_are_provider_neutral()
    {
        var publicTypes = typeof(TelemetryContext).Assembly.GetExportedTypes();
        Assert.All(publicTypes, type => Assert.DoesNotContain("OpenTelemetry", type.FullName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Metric_dimensions_exclude_unbounded_identity_values()
    {
        var names = typeof(MetricDimensions).GetProperties().Select(property => property.Name).ToArray();
        Assert.DoesNotContain("TenantId", names);
        Assert.DoesNotContain("ExecutionSessionId", names);
        Assert.DoesNotContain("CorrelationId", names);
        Assert.Contains("Operation", names);
    }
}
