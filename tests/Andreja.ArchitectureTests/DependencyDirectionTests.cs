using Andreja.Api.Contracts;
using Andreja.AppHost.Hosting;
using Andreja.AppHost.Components.Pages;
using Andreja.AppHost.OpenLoops;
using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Modules.Channels;
using Andreja.Modules.OpenLoops;
using Andreja.Modules.Skills;
using Andreja.Platform.Contracts;
using Andreja.Platform.Contracts.Assistant;
using Andreja.Platform.Contracts.Channels;
using Andreja.Platform.Contracts.Skills;

namespace Andreja.ArchitectureTests;

public sealed class DependencyDirectionTests
{
    private static readonly HashSet<string> ApprovedModuleAssemblyReferences =
        new(StringComparer.Ordinal)
        {
            "Andreja.Platform.Contracts",
            // Framework-neutral facade emitted for fundamental BCL types.
            "System.Collections",
            "System.Collections.Concurrent",
            "System.Linq",
            "System.Memory",
            "System.Runtime",
            "System.Security.Cryptography",
            "System.Text.Json",
            "System.Text.RegularExpressions",
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

    [Fact]
    public void ProductionAssembliesCannotReferenceTestAuthentication()
    {
        var productionAssemblies = new[]
        {
            typeof(AndrejaHostOptions).Assembly,
            typeof(AspNetCoreIdentityAdapter).Assembly,
        };

        var testAuthenticationReferences = productionAssemblies
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Where(reference =>
                (reference.Name ?? string.Empty).Contains(
                    "TestAuth",
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(testAuthenticationReferences);
        Assert.DoesNotContain(
            typeof(AspNetCoreIdentityAdapter).Assembly.GetTypes(),
            type => type.Name.Contains("FakeAuth", StringComparison.OrdinalIgnoreCase)
                || type.Name.Contains("TestAuth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BlazorTaskPageInjectsOnlyTypedApiBoundary()
    {
        var injectedTypes = typeof(Home)
            .GetProperties(
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
            .Where(property => property.CustomAttributes.Any(attribute =>
                attribute.AttributeType.FullName
                    == "Microsoft.AspNetCore.Components.InjectAttribute"))
            .Select(property => property.PropertyType)
            .ToArray();

        Assert.Contains(typeof(IOpenLoopsApiClient), injectedTypes);
        Assert.DoesNotContain(
            injectedTypes,
            type => type.Namespace?.StartsWith("Andreja.Modules", StringComparison.Ordinal) == true
                || type.Namespace?.StartsWith("Andreja.Adapters", StringComparison.Ordinal) == true
                || type.Name.Contains("DbContext", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryExecutionAndInvocationBoundaryCarriesDistinctIdentityIds()
    {
        var boundaries = new[]
        {
            typeof(AssistantExecutionContext),
            typeof(SkillExecutionContext),
            typeof(SkillInvocation),
            typeof(ChannelExecutionContext),
            typeof(ChannelInvocation),
        };

        foreach (var boundary in boundaries)
        {
            var properties = boundary.GetProperties().ToDictionary(
                property => property.Name,
                property => property.PropertyType,
                StringComparer.Ordinal);
            Assert.Equal(typeof(Guid), properties["TenantId"]);
            Assert.Equal(typeof(Guid), properties["AppUserId"]);
            Assert.Equal(typeof(Guid), properties["PrincipalId"]);
        }
    }

    [Fact]
    public void SkillAndChannelHostsExposeNoAmbientServiceOrSecretBoundary()
    {
        var boundaryTypes = new[]
        {
            typeof(ISkillHost),
            typeof(IChannelHost),
            typeof(SkillExecutionContext),
            typeof(ChannelExecutionContext),
            typeof(SkillInvocation),
            typeof(ChannelInvocation),
            typeof(SkillToolHandler),
            typeof(ChannelOperationHandler),
        };
        var forbiddenNames = new[]
        {
            "IServiceProvider",
            "DbContext",
            "Credential",
            "Secret",
            "AccessToken",
            "RefreshToken",
            "HttpClient",
        };

        var exposedTypes = boundaryTypes
            .SelectMany(type =>
                type.GetMethods().SelectMany(method =>
                    method.GetParameters().Select(parameter => parameter.ParameterType))
                .Concat(type.GetProperties().Select(property => property.PropertyType)))
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        Assert.All(
            forbiddenNames,
            forbidden => Assert.DoesNotContain(
                exposedTypes,
                name => name.Contains(forbidden, StringComparison.Ordinal)));
        Assert.DoesNotContain(
            typeof(InMemorySkillHost).GetConstructors()
                .Concat(typeof(InMemoryChannelHost).GetConstructors())
                .SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType == typeof(IServiceProvider));
    }

    [Fact]
    public void PooledHttpHandlersDoNotCaptureCircuitOrRequestState()
    {
        var clientDependencies = typeof(OpenLoopsApiClient)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        var statefulHandlers = typeof(OpenLoopsApiClient).Assembly.GetTypes()
            .Where(type => type.IsAssignableTo(typeof(DelegatingHandler)))
            .Where(type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter =>
                    parameter.ParameterType.FullName
                        == "Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider"
                    || parameter.ParameterType.FullName
                        == "Microsoft.AspNetCore.Http.IHttpContextAccessor"))
            .ToArray();

        Assert.Contains(
            clientDependencies,
            type => type.FullName
                == "Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider");
        Assert.Contains(typeof(ICircuitDelegationTokenService), clientDependencies);
        Assert.Empty(statefulHandlers);
        Assert.DoesNotContain(
            typeof(OpenLoopsApiClient).Assembly.GetTypes(),
            type => type.Name.Contains(
                "CookieForwarding",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ForwardedHeadersRunBeforeSecurityAndNoTrustAllSwitchExists()
    {
        var repositoryRoot = new DirectoryInfo(AppContext.BaseDirectory);
        for (var level = 0; level < 5; level++)
        {
            repositoryRoot = repositoryRoot.Parent
                ?? throw new DirectoryNotFoundException();
        }
        var program = File.ReadAllText(Path.Join(
            repositoryRoot.FullName,
            "src",
            "Andreja.AppHost",
            "Program.cs"));
        var compose = File.ReadAllText(Path.Join(
            repositoryRoot.FullName,
            "compose.yaml"));
        var forwarding = program.IndexOf(
            "app.UseForwardedHeaders();",
            StringComparison.Ordinal);
        var contentSecurityPolicy = program.IndexOf(
            "Andreja.CspNonce",
            StringComparison.Ordinal);
        var rateLimiter = program.IndexOf(
            "app.UseRateLimiter();",
            StringComparison.Ordinal);
        var authentication = program.IndexOf(
            "app.UseAuthentication();",
            StringComparison.Ordinal);

        Assert.True(forwarding >= 0);
        Assert.True(forwarding < contentSecurityPolicy);
        Assert.True(forwarding < rateLimiter);
        Assert.True(forwarding < authentication);
        Assert.DoesNotContain(
            "ASPNETCORE_FORWARDEDHEADERS_ENABLED",
            compose,
            StringComparison.Ordinal);
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
