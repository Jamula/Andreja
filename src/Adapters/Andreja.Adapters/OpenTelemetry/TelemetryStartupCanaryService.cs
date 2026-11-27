using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Andreja.Adapters.OpenTelemetry;

internal sealed class TelemetryStartupCanaryService(
    ILogger<TelemetryStartupCanaryService> logger) : IHostedService
{
    public const string Category =
        "Andreja.Adapters.OpenTelemetry.TelemetryStartupCanaryService";
    private static readonly Action<ILogger, Exception?> WriteStartupCanary =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(44001, "TelemetryStartupCanary"),
            "Andreja telemetry startup canary");

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var activity = AndrejaTelemetry.ActivitySource.StartActivity(
            "andreja.telemetry.startup-canary",
            ActivityKind.Internal);
        activity?.SetTag("andreja.evidence.canary", "synthetic");
        WriteStartupCanary(logger, null);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
