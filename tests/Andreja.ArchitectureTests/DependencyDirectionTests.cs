using Andreja.Api.Contracts;
using Andreja.Modules.OpenLoops;
using Andreja.Platform.Contracts;

namespace Andreja.ArchitectureTests;

public sealed class DependencyDirectionTests
{
    private static readonly HashSet<string> ApprovedModuleAssemblyReferences =
        new(StringComparer.Ordinal)
        {
            "Andreja.Platform.Contracts",
            // Framework-neutral facade emitted for fundamental BCL types.
            "System.Collections",
            "System.Linq",
            "System.Memory",
            "System.Runtime",
            "System.Security.Cryptography",
            "System.Text.Json",
            "System.Threading",
        };

    [Fact]
    public void ModulesDoNotReferenceOutwardLayers()
    {
        var unapprovedReferences = FindUnapprovedModuleReferences(
            typeof(OpenLoopsModule).Assembly.GetReferencedAssemblies());

        Assert.Empty(unapprovedReferences);
    }

    [Theory]
    [InlineData("Andreja.Adapters")]
    [InlineData("Andreja.Api.Contracts")]
    [InlineData("Andreja.AppHost")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Npgsql")]
    [InlineData("Azure.AI.OpenAI")]
    [InlineData("Future.ProviderSdk")]
    public void ModuleReferenceAllowlistRejectsNonApprovedAssemblies(string assemblyName)
    {
        var unapprovedReferences = FindUnapprovedModuleReferences(
            [new(assemblyName)]);

        Assert.Equal([assemblyName], unapprovedReferences);
    }

    [Fact]
    public void ContractsDoNotReferenceModulesAdaptersOrHost()
    {
        var contractAssemblies = new[]
        {
            ApiContractAssembly.Reference,
            PlatformContractAssembly.Reference,
        };

        var outwardReferences = contractAssemblies
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Select(reference => reference.Name ?? string.Empty)
            .Where(reference => reference.StartsWith("Andreja.", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(outwardReferences);
    }

    private static string[] FindUnapprovedModuleReferences(
        IEnumerable<System.Reflection.AssemblyName> references)
    {
        return references
            .Select(reference => reference.Name ?? string.Empty)
            .Where(reference => !ApprovedModuleAssemblyReferences.Contains(reference))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
