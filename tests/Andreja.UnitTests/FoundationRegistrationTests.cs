using Andreja.AppHost.Hosting;
using Andreja.Platform.Contracts.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Andreja.UnitTests;

public sealed class FoundationRegistrationTests
{
    [Fact]
    public void FoundationRegistersEachDeclaredModuleAndAdapter()
    {
        var configuration = CreateConfiguration("test");
        var services = new ServiceCollection();

        services.AddAndrejaFoundation(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Equal(6, provider.GetServices<IModuleBoundary>().Count());
        Assert.Equal(4, provider.GetServices<IAdapterBoundary>().Count());
    }

    [Fact]
    public void FoundationRejectsInvalidOptions()
    {
        var configuration = CreateConfiguration(string.Empty);
        var services = new ServiceCollection();

        services.AddAndrejaFoundation(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<AndrejaHostOptions>>().Value);
    }

    private static IConfiguration CreateConfiguration(string instanceName)
    {
        Dictionary<string, string?> values = new()
        {
            [$"{AndrejaHostOptions.SectionName}:InstanceName"] = instanceName,
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
