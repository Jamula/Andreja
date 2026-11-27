using Andreja.Adapters.OpenTelemetry;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net.Sockets;

namespace Andreja.AppHost.Hosting;

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
        var probePath = Path.Join(path, $".readiness-{Guid.NewGuid():N}");
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
