using Andreja.Api.Contracts;
using Andreja.AppHost.Hosting;
using Andreja.AppHost.Components.Pages;
using Andreja.AppHost.OpenLoops;
using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Modules.Channels;
using Andreja.Modules.OpenLoops;
using Andreja.Modules.Semantics;
using Andreja.Modules.Skills;
using Andreja.Platform.Contracts;
using Andreja.Platform.Contracts.Assistant;
using Andreja.Platform.Contracts.Channels;
using Andreja.Platform.Contracts.Skills;
using Andreja.Platform.Contracts.Semantics;

namespace Andreja.ArchitectureTests;

[TestClass]
public sealed class DependencyDirectionTests
{
    private static readonly HashSet<string> ApprovedModuleAssemblyReferences =
        new(StringComparer.Ordinal)
        {
            "Andreja.Platform.Contracts",
            // Framework-neutral facade emitted for fundamental BCL types.
            "System.Collections",
            "System.Collections.Concurrent",
            "System.Collections.Immutable",
            "System.Linq",
            "System.Memory",
            "System.Runtime",
            "System.Security.Cryptography",
            "System.Text.Json",
            "System.Text.RegularExpressions",
            "System.Threading",
        };

    [TestMethod]
    public void ModulesDoNotReferenceOutwardLayers()
    {
        var unapprovedReferences = FindUnapprovedModuleReferences(
            typeof(OpenLoopsModule).Assembly.GetReferencedAssemblies());

        Assert.IsEmpty(unapprovedReferences);
    }

    [TestMethod]
    [DataRow("Andreja.Adapters")]
    [DataRow("Andreja.Api.Contracts")]
    [DataRow("Andreja.AppHost")]
    [DataRow("Microsoft.AspNetCore")]
    [DataRow("Microsoft.EntityFrameworkCore")]
    [DataRow("Npgsql")]
    [DataRow("Azure.AI.OpenAI")]
    [DataRow("Future.ProviderSdk")]
    public void ModuleReferenceAllowlistRejectsNonApprovedAssemblies(string assemblyName)
    {
        var unapprovedReferences = FindUnapprovedModuleReferences(
            [new(assemblyName)]);

        CollectionAssert.AreEqual(new[] { assemblyName }, unapprovedReferences);
    }

    [TestMethod]
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

        Assert.IsEmpty(outwardReferences);
    }

    [TestMethod]
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

        Assert.IsEmpty(testAuthenticationReferences);
        Assert.IsFalse(typeof(AspNetCoreIdentityAdapter).Assembly.GetTypes().Any(
            type => type.Name.Contains("FakeAuth", StringComparison.OrdinalIgnoreCase)
                || type.Name.Contains("TestAuth", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
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
        Assert.IsFalse(injectedTypes.Any(
            type => type.Namespace?.StartsWith("Andreja.Modules", StringComparison.Ordinal) == true
                || type.Namespace?.StartsWith("Andreja.Adapters", StringComparison.Ordinal) == true
                || type.Name.Contains("DbContext", StringComparison.Ordinal)));
    }

    [TestMethod]
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

        var boundaryProperties = boundaries.Select(boundary =>
            boundary.GetProperties().ToDictionary(
                property => property.Name,
                property => property.PropertyType,
                StringComparer.Ordinal));
        foreach (var properties in boundaryProperties)
        {
            Assert.AreEqual(typeof(Guid), properties["TenantId"]);
            Assert.AreEqual(typeof(Guid), properties["AppUserId"]);
            Assert.AreEqual(typeof(Guid), properties["PrincipalId"]);
        }
    }

    [TestMethod]
    public void SemanticContractsKeepTenantUserAndPrincipalAsDistinctTypedIds()
    {
        Assert.AreNotEqual(typeof(SemanticTenantId), typeof(SemanticAppUserId));
        Assert.AreNotEqual(typeof(SemanticTenantId), typeof(SemanticPrincipalId));
        Assert.AreNotEqual(typeof(SemanticAppUserId), typeof(SemanticPrincipalId));

        var properties = typeof(ProfileAssertion).GetProperties()
            .ToDictionary(property => property.Name, property => property.PropertyType);
        Assert.AreEqual(typeof(SemanticTenantId), properties[nameof(ProfileAssertion.TenantId)]);
        Assert.AreEqual(typeof(SemanticAppUserId), properties[nameof(ProfileAssertion.AppUserId)]);
        Assert.AreEqual(
            typeof(SemanticPrincipalId),
            properties[nameof(ProfileAssertion.PrincipalId)]);
    }

    [TestMethod]
    public void SemanticConformanceHasNoPersistenceOrGraphProviderBoundary()
    {
        var semanticTypes = typeof(InMemorySemanticAssertionLedger).Assembly.GetTypes()
            .Where(type => type.Namespace == "Andreja.Modules.Semantics")
            .ToArray();
        var forbiddenFragments = new[]
        {
            "DbContext",
            "EntityFramework",
            "Npgsql",
            "Neo4j",
            "Gremlin",
            "Sparql",
            "Embedding",
        };

        foreach (var type in semanticTypes)
        {
            foreach (var fragment in forbiddenFragments)
            {
                Assert.DoesNotContain(
                    fragment,
                    type.FullName ?? type.Name,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [TestMethod]
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

        foreach (var forbidden in forbiddenNames)
        {
            Assert.IsFalse(exposedTypes.Any(
                name => name.Contains(forbidden, StringComparison.Ordinal)));
        }
        Assert.IsFalse(typeof(InMemorySkillHost).GetConstructors()
                .Concat(typeof(InMemoryChannelHost).GetConstructors())
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType == typeof(IServiceProvider)));
    }

    [TestMethod]
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

        Assert.IsTrue(clientDependencies.Any(
            type => type.FullName
                == "Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider"));
        Assert.Contains(typeof(ICircuitDelegationTokenService), clientDependencies);
        Assert.IsEmpty(statefulHandlers);
        Assert.IsFalse(typeof(OpenLoopsApiClient).Assembly.GetTypes().Any(
            type => type.Name.Contains(
                "CookieForwarding",
                StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
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

        Assert.IsTrue(forwarding >= 0);
        Assert.IsTrue(forwarding < contentSecurityPolicy);
        Assert.IsTrue(forwarding < rateLimiter);
        Assert.IsTrue(forwarding < authentication);
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
