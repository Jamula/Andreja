using Andreja.Adapters.Assistant.OpenAiCompatible;
using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Adapters.OpenTelemetry;
using Andreja.Adapters.PostgreSql;
using Andreja.Modules.Assistant;
using Andreja.Modules.Channels;
using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Andreja.Modules.Portability;
using Andreja.Modules.Skills;
using Andreja.Platform.Contracts.Composition;

namespace Andreja.AppHost.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAndrejaFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<AndrejaHostOptions>()
            .Bind(configuration.GetRequiredSection(AndrejaHostOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IModuleBoundary, IdentityModule>();
        services.AddSingleton<IModuleBoundary, OpenLoopsModule>();
        services.AddSingleton<IModuleBoundary, AssistantModule>();
        services.AddSingleton<IModuleBoundary, SkillsModule>();
        services.AddSingleton<IModuleBoundary, ChannelsModule>();
        services.AddSingleton<IModuleBoundary, PortabilityModule>();

        services.AddSingleton<IAdapterBoundary, PostgreSqlAdapter>();
        services.AddSingleton<IAdapterBoundary, AspNetCoreIdentityAdapter>();
        services.AddSingleton<IAdapterBoundary, OpenAiCompatibleAssistantAdapter>();
        services.AddSingleton<IAdapterBoundary, OpenTelemetryAdapter>();

        return services;
    }
}
