using System.Diagnostics;
using OpenTelemetry;

namespace Andreja.Adapters.OpenTelemetry;

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
        foreach (var tag in data.TagObjects.Where(tag => !AllowedAttributes.Contains(tag.Key)).ToArray())
        {
            data.SetTag(tag.Key, value: null);
            removed++;
            if (IsProhibited(tag.Key))
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
