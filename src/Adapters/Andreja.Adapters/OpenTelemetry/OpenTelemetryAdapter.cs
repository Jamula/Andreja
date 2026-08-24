using Andreja.Platform.Contracts.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace Andreja.Adapters.OpenTelemetry;

public sealed class OpenTelemetryAdapter : IAdapterBoundary;

public sealed class AndrejaTelemetryOptions
{
    public const string SectionName = "Andreja:Telemetry";

    public bool Enabled { get; init; }

    [Required]
    public string ServiceName { get; init; } = "andreja";

    [Url]
    public string OtlpEndpoint { get; init; } = "http://otel-collector:4317";
}

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
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                options.ServiceName,
                serviceInstanceId: instanceName))
            .WithTracing(tracing => tracing
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
                .AddMeter(AndrejaTelemetry.MeterName)
                .AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = endpoint;
                    exporter.Protocol = OtlpExportProtocol.Grpc;
                }));

        return services;
    }
}

public static class AndrejaTelemetry
{
    public const string MeterName = "Andreja.Operations";

    public static readonly System.Diagnostics.Metrics.Meter Meter = new(MeterName);

    public static readonly System.Diagnostics.Metrics.Counter<long> PolicyChecks =
        Meter.CreateCounter<long>("andreja_telemetry_policy_checks_total");

    public static readonly System.Diagnostics.Metrics.Counter<long> PolicyViolations =
        Meter.CreateCounter<long>("andreja_telemetry_policy_violation_total");

    public static readonly System.Diagnostics.Metrics.Counter<long> SuppressedAttributes =
        Meter.CreateCounter<long>("andreja_telemetry_suppressed_attributes_total");
}

public sealed class ContentSuppressingActivityProcessor : BaseProcessor<Activity>
{
    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.Ordinal)
    {
        "error.type",
        "http.request.method",
        "http.response.status_code",
        "network.protocol.version",
        "http.route",
        "url.scheme",
    };

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var removed = 0;
        foreach (var tag in data.TagObjects.ToArray())
        {
            if (!AllowedAttributes.Contains(tag.Key))
            {
                data.SetTag(tag.Key, value: null);
                removed++;
                if (IsProhibited(tag.Key))
                {
                    AndrejaTelemetry.PolicyViolations.Add(1);
                }
            }
        }

        AndrejaTelemetry.PolicyChecks.Add(1);
        if (removed > 0)
        {
            AndrejaTelemetry.SuppressedAttributes.Add(removed);
        }
    }

    private static bool IsProhibited(string key) =>
        key.Contains("task", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("prompt", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("response", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("user.id", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("user_id", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("userid", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("enduser", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("recovery", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("api_key", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("passkey", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("connector.content", StringComparison.OrdinalIgnoreCase);
}
