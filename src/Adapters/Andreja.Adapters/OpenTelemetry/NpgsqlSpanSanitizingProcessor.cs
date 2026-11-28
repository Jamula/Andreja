using System.Diagnostics;
using OpenTelemetry;

namespace Andreja.Adapters.OpenTelemetry;

public sealed class NpgsqlSpanSanitizingProcessor : BaseProcessor<Activity>
{
    public const string NpgsqlActivitySourceName = "Npgsql";

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!string.Equals(data.Source.Name, NpgsqlActivitySourceName, StringComparison.Ordinal))
        {
            return;
        }

        if (!IsSanitizedSpanName(data.DisplayName))
        {
            data.DisplayName = "postgresql.operation";
        }

        data.SetTag("db.query.text", value: null);
        data.SetTag("db.statement", value: null);
    }

    private static bool IsSanitizedSpanName(string displayName) =>
        displayName is "postgresql.query"
            or "postgresql.batch"
            or "postgresql.copy"
            or "postgresql.operation"
            or "postgresql.physical_open";
}
