using Andreja.Adapters.OpenTelemetry;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net.Mime;
using System.Net.Sockets;
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

public sealed class OperationalReadinessHealthCheck(
    IOptions<AndrejaOperationsOptions> options,
    IOptions<AndrejaTelemetryOptions> telemetryOptions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var configured = options.Value;
            VerifyWritableKeyDirectory(configured.DataProtectionKeysPath);

            if (configured.Database.Enabled)
            {
                VerifyPasswordFile(configured.Database.PasswordFile);
                await VerifyTcpEndpointAsync(
                    configured.Database.Host,
                    configured.Database.Port,
                    cancellationToken);
            }

            var telemetry = telemetryOptions.Value;
            if (telemetry.Enabled)
            {
                var endpoint = new Uri(telemetry.OtlpEndpoint, UriKind.Absolute);
                await VerifyTcpEndpointAsync(
                    endpoint.Host,
                    endpoint.IsDefaultPort
                        ? endpoint.Scheme == Uri.UriSchemeHttps ? 443 : 80
                        : endpoint.Port,
                    cancellationToken);
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SocketException)
        {
            return HealthCheckResult.Unhealthy("Required local state is unavailable.");
        }
    }

    private static async Task VerifyTcpEndpointAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, cancellationToken);
    }

    private static void VerifyWritableKeyDirectory(string configuredPath)
    {
        var path = Path.GetFullPath(configuredPath);
        Directory.CreateDirectory(path);
        var probePath = Path.Combine(path, $".readiness-{Guid.NewGuid():N}");
        using (File.Create(probePath, 1, FileOptions.DeleteOnClose))
        {
        }
    }

    private static void VerifyPasswordFile(string configuredPath)
    {
        var path = Path.GetFullPath(configuredPath);
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            throw new IOException("The configured PostgreSQL password file is unavailable.");
        }
    }
}
