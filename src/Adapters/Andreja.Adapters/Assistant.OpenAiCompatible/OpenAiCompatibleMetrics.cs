using System.Diagnostics.Metrics;
using Andreja.Platform.Contracts.Assistant;

namespace Andreja.Adapters.Assistant.OpenAiCompatible;

public static class OpenAiCompatibleMetrics
{
    public const string MeterName = "Andreja.Assistant.OpenAiCompatible";
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> Requests =
        Meter.CreateCounter<long>("andreja_assistant_requests_total");
    public static readonly Counter<long> Retries =
        Meter.CreateCounter<long>("andreja_assistant_retries_total");
    public static readonly Counter<long> InputUnits =
        Meter.CreateCounter<long>("andreja_assistant_input_units_total");
    public static readonly Counter<long> OutputUnits =
        Meter.CreateCounter<long>("andreja_assistant_output_units_total");
    public static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("andreja_assistant_request_duration_ms");

    internal static void Record(AssistantUsage usage)
    {
        Requests.Add(
            1,
            new KeyValuePair<string, object?>("result.class", usage.ResultClass));
        Duration.Record(usage.Duration.TotalMilliseconds);
        if (usage.InputUnits is { } inputUnits)
        {
            InputUnits.Add(inputUnits);
        }

        if (usage.OutputUnits is { } outputUnits)
        {
            OutputUnits.Add(outputUnits);
        }
    }
}
