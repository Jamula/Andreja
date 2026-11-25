using Andreja.Adapters.OpenTelemetry;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Mime;
using System.Text.Json;

namespace Andreja.AppHost.Hosting;

public static class OperationsServiceCollectionExtensions
{
    public static IServiceCollection AddAndrejaOperations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetRequiredSection(AndrejaOperationsOptions.SectionName);
        var options = section.Get<AndrejaOperationsOptions>()
            ?? throw new InvalidOperationException("Andreja operations configuration is required.");
        var instanceName = configuration.GetRequiredSection(AndrejaHostOptions.SectionName)
            .GetValue<string>(nameof(AndrejaHostOptions.InstanceName))
            ?? throw new InvalidOperationException("Andreja instance name is required.");

        services.AddOptions<AndrejaOperationsOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .Validate(
                configured => !configured.Database.Enabled ||
                    !string.IsNullOrWhiteSpace(configured.Database.PasswordFile),
                "A PostgreSQL password file is required when database readiness is enabled.")
            .ValidateOnStart();

        services.AddDataProtection()
            .SetApplicationName($"Andreja:{instanceName}")
            .PersistKeysToFileSystem(new DirectoryInfo(options.DataProtectionKeysPath));

        services.AddAndrejaOpenTelemetry(configuration, instanceName);
        services.AddHealthChecks()
            .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck<OperationalReadinessHealthCheck>("operations", tags: ["ready"]);

        return services;
    }

    public static IEndpointRouteBuilder MapAndrejaOperationalEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks("/health/live", CreateHealthOptions("live"));
        endpoints.MapHealthChecks("/health/ready", CreateHealthOptions("ready"));
        return endpoints;
    }

    private static HealthCheckOptions CreateHealthOptions(string tag) => new()
    {
        Predicate = registration => registration.Tags.Contains(tag),
        ResponseWriter = static async (context, report) =>
        {
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                new { status = report.Status.ToString() },
                cancellationToken: context.RequestAborted);
        },
    };
}
