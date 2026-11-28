using System.Diagnostics;
using OpenTelemetry;

namespace Andreja.Adapters.OpenTelemetry;

public sealed class ContentSuppressingActivityProcessor : BaseProcessor<Activity>
{
    public static IReadOnlySet<string> AllowedTagNames => AllowedAttributes;

    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.Ordinal)
    {
        "andreja.artifact.kind",
        "andreja.denial.code",
        "andreja.endpoint.locality",
        "andreja.operation",
        "andreja.outcome",
        "db.operation.name",
        "db.response.status_code",
        "db.system",
        "db.system.name",
        "error.type",
        "gen_ai.operation.name",
        "gen_ai.provider.name",
        "gen_ai.request.model",
        "gen_ai.response.finish_reasons",
        "gen_ai.system",
        "gen_ai.usage.input_tokens",
        "gen_ai.usage.output_tokens",
        "http.request.method",
        "http.response.status_code",
        "http.route",
        "network.protocol.version",
        "url.scheme",
    };

    private static readonly HashSet<string> AllowedOperationalEventNames = new(StringComparer.Ordinal)
    {
        "received-first-response",
    };

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var removed = 0;
        foreach (var tag in data.TagObjects.Where(tag => !AllowedAttributes.Contains(tag.Key)).ToArray())
        {
            data.SetTag(tag.Key, value: null);
            removed++;
            if (IsProhibited(tag.Key))
            {
                AndrejaTelemetry.PolicyViolations.Add(1);
            }
        }

        if (!string.IsNullOrEmpty(data.StatusDescription))
        {
            data.SetStatus(data.Status, description: null);
            removed++;
        }

        var disallowedEvents = data.Events
            .Where(static @event => !IsAllowedOperationalEvent(@event))
            .ToArray();
        if (disallowedEvents.Length > 0)
        {
            var isNpgsqlSource = string.Equals(
                data.Source.Name,
                NpgsqlSpanSanitizingProcessor.NpgsqlActivitySourceName,
                StringComparison.Ordinal);
            removed += disallowedEvents.Sum(static @event => 1 + @event.Tags.Count());
            var prohibitedEvents = disallowedEvents.Count(@event =>
                HasUnexemptProhibitedEventPayload(@event, isNpgsqlSource));
            if (prohibitedEvents > 0)
            {
                AndrejaTelemetry.PolicyViolations.Add(prohibitedEvents);
            }

            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }

        AndrejaTelemetry.PolicyChecks.Add(1);
        if (removed > 0)
        {
            AndrejaTelemetry.SuppressedAttributes.Add(removed);
        }
    }

    public static bool IsProhibitedAttributeKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return !AllowedAttributes.Contains(key) && IsProhibited(key);
    }

    internal static bool IsAllowedOperationalEvent(ActivityEvent @event) =>
        AllowedOperationalEventNames.Contains(@event.Name)
        && @event.Tags.All(static tag => AllowedAttributes.Contains(tag.Key));

    private static bool HasUnexemptProhibitedEventPayload(ActivityEvent @event, bool isNpgsqlSource)
    {
        if (isNpgsqlSource && string.Equals(@event.Name, "exception", StringComparison.Ordinal))
        {
            return @event.Tags.Any(static tag =>
                !IsStandardExceptionTag(tag.Key) && IsProhibited(tag.Key));
        }

        return IsProhibitedEventName(@event.Name)
            || @event.Tags.Any(static tag => IsProhibited(tag.Key));
    }

    private static bool IsStandardExceptionTag(string key) =>
        string.Equals(key, "exception.type", StringComparison.Ordinal) ||
        string.Equals(key, "exception.message", StringComparison.Ordinal) ||
        string.Equals(key, "exception.stacktrace", StringComparison.Ordinal);

    private static bool IsProhibitedEventName(string eventName) =>
        !AllowedOperationalEventNames.Contains(eventName) && IsProhibited(eventName);

    private static bool IsProhibited(string key) =>
        key.Contains("task", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("prompt", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("response", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("input.messages", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("output.messages", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("system_instructions", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("tool.call.arguments", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("tool.call.result", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("tool.definitions", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("db.statement", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("db.query.text", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("exception.message", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("exception.stacktrace", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("url.query", StringComparison.OrdinalIgnoreCase) ||
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
