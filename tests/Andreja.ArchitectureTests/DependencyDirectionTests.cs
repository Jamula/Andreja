using Andreja.Api.Contracts;
using Andreja.Modules.OpenLoops;
using Andreja.Platform.Contracts;

namespace Andreja.ArchitectureTests;

public sealed class DependencyDirectionTests
{
    [Fact]
    public void ModulesDoNotReferenceOutwardLayers()
    {
        string[] forbiddenAssemblyPrefixes =
        [
            "Andreja.Adapters",
            "Andreja.Api.Contracts",
            "Andreja.AppHost",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
        ];

        var references = typeof(OpenLoopsModule).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            references,
            reference => forbiddenAssemblyPrefixes.Any(
                prefix => reference.StartsWith(prefix, StringComparison.Ordinal)));
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
}
