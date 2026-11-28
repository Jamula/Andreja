using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Andreja.Adapters.OpenTelemetry;

public static class AndrejaTelemetry
{
    public const string MeterName = "Andreja.Operations";
    public const string ActivitySourceName = "Andreja.Operations";
    public const string GenAiMeterName = "Andreja.GenAi";
    public const string GenAiActivitySourceName = "Andreja.GenAi";
    public const string ExtensionsAiDiagnosticsName = "Experimental.Microsoft.Extensions.AI";

    public static readonly Meter Meter = new(MeterName);
    public static readonly Meter GenAiMeter = new(GenAiMeterName);
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly ActivitySource GenAiActivitySource = new(GenAiActivitySourceName);

    public static readonly Counter<long> PolicyChecks =
        Meter.CreateCounter<long>("andreja_telemetry_policy_checks_total");

    public static readonly Counter<long> PolicyViolations =
        Meter.CreateCounter<long>("andreja_telemetry_policy_violation_total");

    public static readonly Counter<long> SuppressedAttributes =
        Meter.CreateCounter<long>("andreja_telemetry_suppressed_attributes_total");

    public static readonly Counter<long> ProposalsCreated =
        Meter.CreateCounter<long>("andreja_proposals_created_total");

    public static readonly Counter<long> ProposalsConfirmed =
        Meter.CreateCounter<long>("andreja_proposals_confirmed_total");

    public static readonly Counter<long> ProposalsExpired =
        Meter.CreateCounter<long>("andreja_proposals_expired_total");

    public static readonly Counter<long> ProposalsIdempotentReplay =
        Meter.CreateCounter<long>("andreja_proposals_idempotent_replay_total");

    public static readonly Counter<long> ExecutionDenied =
        Meter.CreateCounter<long>("andreja_execution_denied_total");

    public static readonly Counter<long> HostInvocations =
        Meter.CreateCounter<long>("andreja_host_invocations_total");

    public static readonly Histogram<long> GenAiTokenUsage =
        GenAiMeter.CreateHistogram<long>("gen_ai.client.token.usage", unit: "{token}");

    public static readonly Histogram<double> GenAiOperationDuration =
        GenAiMeter.CreateHistogram<double>("gen_ai.client.operation.duration", unit: "s");
}
