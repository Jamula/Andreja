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

        foreach (var @event in data.Events.ToArray())
        {
            if (IsProhibited(@event.Name)
                || @event.Tags.Any(tag => IsProhibited(tag.Key)))
            {
                AndrejaTelemetry.PolicyViolations.Add(1);
            }
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
