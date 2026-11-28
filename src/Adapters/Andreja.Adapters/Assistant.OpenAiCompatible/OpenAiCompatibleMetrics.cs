using System.Diagnostics;
using System.Diagnostics.Metrics;
using Andreja.Adapters.OpenTelemetry;
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

        var genAiBaseTags = new TagList
        {
            { "gen_ai.operation.name", "chat" },
            { "gen_ai.provider.name", usage.Provider },
        };
        if (!string.Equals(usage.ResultClass, "completed", StringComparison.Ordinal)
            && !string.Equals(usage.ResultClass, "proposed", StringComparison.Ordinal))
        {
            genAiBaseTags.Add("error.type", usage.ResultClass);
        }

        AndrejaTelemetry.GenAiOperationDuration.Record(
            usage.Duration.TotalSeconds,
            genAiBaseTags);

        if (usage.InputUnits is { } inputUnits)
        {
            InputUnits.Add(inputUnits);
            var inputTags = genAiBaseTags;
            inputTags.Add("gen_ai.token.type", "input");
            AndrejaTelemetry.GenAiTokenUsage.Record(inputUnits, inputTags);
        }

        if (usage.OutputUnits is { } outputUnits)
        {
            OutputUnits.Add(outputUnits);
            var outputTags = genAiBaseTags;
            outputTags.Add("gen_ai.token.type", "output");
            AndrejaTelemetry.GenAiTokenUsage.Record(outputUnits, outputTags);
        }
    }
}
