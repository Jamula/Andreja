using Andreja.Adapters.Assistant.OpenAiCompatible;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Andreja.Adapters.OpenTelemetry;

public static class OpenTelemetryServiceCollectionExtensions
{
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
                configured => !configured.Enabled ||
                    Uri.TryCreate(configured.OtlpEndpoint, UriKind.Absolute, out var endpoint) &&
                    endpoint.Scheme is "http" or "https",
                "An enabled OTLP endpoint must be an absolute HTTP(S) URI.")
            .ValidateOnStart();

        if (!options.Enabled)
        {
            return services;
        }

        var endpoint = new Uri(options.OtlpEndpoint, UriKind.Absolute);
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
                .AddSource(AndrejaTelemetry.ActivitySourceName)
                .AddAspNetCoreInstrumentation(instrumentation =>
                {
                    instrumentation.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health");
                    instrumentation.RecordException = false;
                })
                .AddProcessor<ContentSuppressingActivityProcessor>()
                .AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = endpoint;
                    exporter.Protocol = OtlpExportProtocol.Grpc;
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddMeter(AndrejaTelemetry.MeterName, OpenAiCompatibleMetrics.MeterName)
                .AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = endpoint;
                    exporter.Protocol = OtlpExportProtocol.Grpc;
                }));

        return services;
    }
}
