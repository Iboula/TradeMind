using System.Reflection;
using TradeMind.Api.Contracts.Common;

namespace TradeMind.Api.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Domain_projects_do_not_reference_aspnet_core()
    {
        foreach (var project in Directory.GetFiles(RepositoryRoot(), "*.Domain.csproj", SearchOption.AllDirectories))
        {
            var projectText = File.ReadAllText(project);
            Assert.DoesNotContain("Microsoft.AspNetCore", projectText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Api_contracts_do_not_expose_domain_types()
    {
        var contractTypes = typeof(ApiResponseEnvelope).Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith("TradeMind.Api.Contracts", StringComparison.Ordinal) == true);

        foreach (var type in contractTypes)
        {
            Assert.DoesNotContain(".Domain", type.BaseType?.FullName ?? string.Empty, StringComparison.Ordinal);
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.DoesNotContain(".Domain", property.PropertyType.FullName ?? string.Empty, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Endpoints_do_not_reference_domain_or_infrastructure_implementations()
    {
        var endpointDirectory = Path.Combine(RepositoryRoot(), "src", "TradeMind.Api", "Endpoints");
        foreach (var file in Directory.GetFiles(endpointDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("TradeMind.KnowledgeHub.Infrastructure", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TradeMind.MarketConnectors.Infrastructure", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".Domain", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".Infrastructure", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Api_assembly_has_no_broker_mt5_or_concrete_llm_reference()
    {
        var references = typeof(Program).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.DoesNotContain(references, name => name is not null && (
            name.Contains("MT5", StringComparison.OrdinalIgnoreCase)
            || name.Contains("MetaTrader", StringComparison.OrdinalIgnoreCase)
            || name.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TradeMind.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("TradeMind repository root was not found.");
    }
}
