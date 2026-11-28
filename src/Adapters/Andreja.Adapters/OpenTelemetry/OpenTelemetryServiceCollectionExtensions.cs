using System.Diagnostics.Metrics;
using Andreja.Adapters.Assistant.OpenAiCompatible;
using Andreja.Platform.Contracts.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Andreja.Adapters.OpenTelemetry;

public static class OpenTelemetryServiceCollectionExtensions
{
    public const string ContentCaptureProhibitedMessage =
        "Andreja telemetry prohibits content and sensitive-data capture (CaptureContent and EnableSensitiveData must remain false).";

    private static readonly string[] ApprovedNpgsqlMetricTagKeys =
    [
        "db.operation.name",
        "db.response.status_code",
        "db.system",
        "db.system.name",
        "error.type",
        "state",
    ];

    public static IServiceCollection AddAndrejaOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string instanceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(AndrejaTelemetryOptions.SectionName);
        var options = section.Get<AndrejaTelemetryOptions>() ?? new AndrejaTelemetryOptions();

        services.AddOptions<AndrejaTelemetryOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .Validate(
                configured => !configured.CaptureContent && !configured.EnableSensitiveData,
                ContentCaptureProhibitedMessage)
            .Validate(
                configured => !configured.Enabled ||
                    Uri.TryCreate(configured.OtlpEndpoint, UriKind.Absolute, out var endpoint) &&
                    endpoint.Scheme is "http" or "https",
                "An enabled OTLP endpoint must be an absolute HTTP(S) URI.")
            .ValidateOnStart();

        if (options.CaptureContent || options.EnableSensitiveData)
        {
            throw new InvalidOperationException(ContentCaptureProhibitedMessage);
        }

        if (!options.Enabled)
        {
            services.TryAddSingleton<IControlPlaneTelemetry>(NullControlPlaneTelemetry.Instance);
            return services;
        }

        var endpoint = new Uri(options.OtlpEndpoint, UriKind.Absolute);
        services.AddSingleton<IControlPlaneTelemetry, OpenTelemetryControlPlaneTelemetry>();
        services.AddSingleton<NpgsqlSpanSanitizingProcessor>();
        services.AddSingleton<ContentSuppressingActivityProcessor>();
        services.AddHostedService<TelemetryStartupCanaryService>();
        services.AddLogging(logging => logging
            .AddFilter<OpenTelemetryLoggerProvider>(
                (category, level) =>
                    category == TelemetryStartupCanaryService.Category &&
                    level >= LogLevel.Information)
            .AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = false;
                logging.IncludeScopes = false;
                logging.ParseStateValues = false;
                logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
                    options.ServiceName,
                    serviceInstanceId: instanceName));
                logging.AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = endpoint;
                    exporter.Protocol = OtlpExportProtocol.Grpc;
                });
            }));
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                options.ServiceName,
                serviceInstanceId: instanceName))
            .WithTracing(tracing => tracing
                .AddSource(
                    AndrejaTelemetry.ActivitySourceName,
                    AndrejaTelemetry.GenAiActivitySourceName,
                    AndrejaTelemetry.ExtensionsAiDiagnosticsName)
                .AddNpgsql()
                .AddAspNetCoreInstrumentation(instrumentation =>
                {
                    instrumentation.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health");
                    instrumentation.RecordException = false;
                })
                .AddHttpClientInstrumentation(instrumentation =>
                {
                    instrumentation.RecordException = false;
                    instrumentation.FilterHttpRequestMessage = message =>
                        message.RequestUri?.AbsolutePath.StartsWith(
                            "/health",
                            StringComparison.OrdinalIgnoreCase) != true;
                    instrumentation.EnrichWithHttpRequestMessage = static (activity, message) =>
                    {
                        activity.SetTag("url.full", null);
                        activity.SetTag("url.query", null);
                        activity.SetTag("url.path", null);
                        if (message.RequestUri is { } uri)
                        {
                            activity.SetTag(
                                "andreja.endpoint.locality",
                                uri.IsLoopback ? "loopback" : "external");
                        }
                    };
                })
                .AddProcessor<NpgsqlSpanSanitizingProcessor>()
                .AddProcessor<ContentSuppressingActivityProcessor>()
                .AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = endpoint;
                    exporter.Protocol = OtlpExportProtocol.Grpc;
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(
                    "Npgsql",
                    AndrejaTelemetry.MeterName,
                    AndrejaTelemetry.GenAiMeterName,
                    AndrejaTelemetry.ExtensionsAiDiagnosticsName,
                    OpenAiCompatibleMetrics.MeterName)
                .AddView(ConfigureMetricView)
                .AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = endpoint;
                    exporter.Protocol = OtlpExportProtocol.Grpc;
                }));

        return services;
    }

    public static MetricStreamConfiguration? ConfigureMetricView(Instrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        return string.Equals(
            instrument.Meter.Name,
            NpgsqlSpanSanitizingProcessor.NpgsqlActivitySourceName,
            StringComparison.Ordinal)
            ? new MetricStreamConfiguration
            {
                TagKeys = ApprovedNpgsqlMetricTagKeys,
            }
            : null;
    }
}
