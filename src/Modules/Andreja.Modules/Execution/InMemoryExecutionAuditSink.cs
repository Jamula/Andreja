using Andreja.Platform.Contracts.Execution;
using System.Collections.Concurrent;

namespace Andreja.Modules.Execution;

public sealed class InMemoryExecutionAuditSink : IExecutionAuditSink
{
    private readonly ConcurrentQueue<ExecutionAuditEntry> entries = new();

    public IReadOnlyList<ExecutionAuditEntry> Entries => entries.ToArray();

    public ValueTask AppendAsync(
        ExecutionAuditEntry entry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        entries.Enqueue(entry);
        return ValueTask.CompletedTask;
    }
}
