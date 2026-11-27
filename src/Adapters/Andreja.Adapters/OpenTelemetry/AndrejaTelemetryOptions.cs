using System.ComponentModel.DataAnnotations;

namespace Andreja.Adapters.OpenTelemetry;

public sealed class AndrejaTelemetryOptions
{
    public const string SectionName = "Andreja:Telemetry";

    public bool Enabled { get; init; }

    [Required]
    public string ServiceName { get; init; } = "andreja";

    [Url]
    public string OtlpEndpoint { get; init; } = "http://otel-collector:4317";
}
