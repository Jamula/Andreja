using System.Diagnostics;

namespace Andreja.Adapters.OpenTelemetry;

public static class AndrejaTelemetry
{
    public const string MeterName = "Andreja.Operations";
    public const string ActivitySourceName = "Andreja.Operations";

    public static readonly System.Diagnostics.Metrics.Meter Meter = new(MeterName);
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly System.Diagnostics.Metrics.Counter<long> PolicyChecks =
        Meter.CreateCounter<long>("andreja_telemetry_policy_checks_total");

    public static readonly System.Diagnostics.Metrics.Counter<long> PolicyViolations =
        Meter.CreateCounter<long>("andreja_telemetry_policy_violation_total");

    public static readonly System.Diagnostics.Metrics.Counter<long> SuppressedAttributes =
        Meter.CreateCounter<long>("andreja_telemetry_suppressed_attributes_total");
}
