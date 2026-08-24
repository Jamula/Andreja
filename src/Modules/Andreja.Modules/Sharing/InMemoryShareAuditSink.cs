using Andreja.Platform.Contracts.Sharing;

namespace Andreja.Modules.Sharing;

public sealed class InMemoryShareAuditSink : IShareAuditSink
{
    private readonly object gate = new();
    private readonly HashSet<Guid> entryIds = [];
    private readonly List<ShareAuditEntry> entries = [];

    public IReadOnlyList<ShareAuditEntry> Entries
    {
        get
        {
            lock (gate)
            {
                return entries.ToArray();
            }
        }
    }

    public ValueTask AppendAsync(
        ShareAuditEntry entry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!entryIds.Add(entry.AuditId))
            {
                throw new InvalidOperationException("Share audit entries are append-only and unique.");
            }

            entries.Add(entry);
        }

        return ValueTask.CompletedTask;
    }
}
