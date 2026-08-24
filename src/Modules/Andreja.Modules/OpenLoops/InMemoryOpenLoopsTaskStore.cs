using Andreja.Modules.Identity;

namespace Andreja.Modules.OpenLoops;

public sealed class InMemoryOpenLoopsTaskStore : IOpenLoopsTaskStore
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, OpenLoopTask> tasks = [];
    private readonly Dictionary<(Guid TenantId, string Key), Receipt> receipts = [];

    public IReadOnlyList<TaskAuditEntry> AuditEntries
    {
        get
        {
            lock (gate)
            {
                return auditEntries.ToArray();
            }
        }
    }

    private readonly List<TaskAuditEntry> auditEntries = [];

    public Task<IReadOnlyList<OpenLoopTask>> ListAsync(
        TenantPrincipalContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateContext(context);
        lock (gate)
        {
            IReadOnlyList<OpenLoopTask> result = tasks.Values
                .Where(task =>
                    task.TenantId == context.TenantId
                    && task.OwnerPrincipalId == context.PrincipalId)
                .OrderBy(task => task.Status)
                .ThenBy(task => task.DueAt)
                .ThenBy(task => task.CreatedAt)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    public Task<TaskMutationResult> CreateAsync(
        TenantPrincipalContext context,
        OpenLoopTask task,
        Guid proposalId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateContext(context);
        ArgumentNullException.ThrowIfNull(task);
        lock (gate)
        {
            var intent = $"create:{proposalId:D}";
            if (Replay(context, idempotencyKey, intent, out var replay))
            {
                return Task.FromResult(replay);
            }

            if (task.TenantId != context.TenantId
                || task.OwnerPrincipalId != context.PrincipalId)
            {
                return Task.FromResult(StoreReceipt(
                    context,
                    idempotencyKey,
                    intent,
                    new(TaskMutationOutcome.Denied, null)));
            }

            if (!tasks.TryAdd(task.Id, task))
            {
                return Task.FromResult(StoreReceipt(
                    context,
                    idempotencyKey,
                    intent,
                    new(TaskMutationOutcome.Conflict, null)));
            }

            return Task.FromResult(StoreAndAudit(
                context,
                idempotencyKey,
                intent,
                task,
                "create",
                task.SourceKind,
                task.SourceReference,
                task.CreatedAt));
        }
    }

    public Task<TaskMutationResult> CompleteAsync(
        TenantPrincipalContext context,
        Guid taskId,
        long expectedVersion,
        string idempotencyKey,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateContext(context);
        lock (gate)
        {
            var intent = $"complete:{taskId:D}:{expectedVersion}";
            if (Replay(context, idempotencyKey, intent, out var replay))
            {
                return Task.FromResult(replay);
            }

            if (!tasks.TryGetValue(taskId, out var task)
                || task.TenantId != context.TenantId)
            {
                return Task.FromResult(StoreReceipt(
                    context,
                    idempotencyKey,
                    intent,
                    new(TaskMutationOutcome.NotFound, null)));
            }

            if (task.OwnerPrincipalId != context.PrincipalId)
            {
                return Task.FromResult(StoreReceipt(
                    context,
                    idempotencyKey,
                    intent,
                    new(TaskMutationOutcome.Denied, null)));
            }

            if (task.Version != expectedVersion || task.Status != OpenLoopTaskStatus.Open)
            {
                return Task.FromResult(StoreReceipt(
                    context,
                    idempotencyKey,
                    intent,
                    new(TaskMutationOutcome.Conflict, task)));
            }

            task.Complete(occurredAt);
            return Task.FromResult(StoreAndAudit(
                context,
                idempotencyKey,
                intent,
                task,
                "complete",
                "user",
                context.PrincipalId.Value.ToString("D"),
                occurredAt));
        }
    }

    public Task<TaskMutationResult> DeleteAsync(
        TenantPrincipalContext context,
        Guid taskId,
        long expectedVersion,
        string idempotencyKey,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateContext(context);
        lock (gate)
        {
            var intent = $"delete:{taskId:D}:{expectedVersion}";
            if (Replay(context, idempotencyKey, intent, out var replay))
            {
                return Task.FromResult(replay);
            }

            if (!tasks.TryGetValue(taskId, out var task)
                || task.TenantId != context.TenantId)
            {
                return Task.FromResult(StoreReceipt(
                    context,
                    idempotencyKey,
                    intent,
                    new(TaskMutationOutcome.NotFound, null)));
            }

            if (task.OwnerPrincipalId != context.PrincipalId)
            {
                return Task.FromResult(StoreReceipt(
                    context,
                    idempotencyKey,
                    intent,
                    new(TaskMutationOutcome.Denied, null)));
            }

            if (task.Version != expectedVersion)
            {
                return Task.FromResult(StoreReceipt(
                    context,
                    idempotencyKey,
                    intent,
                    new(TaskMutationOutcome.Conflict, task)));
            }

            tasks.Remove(taskId);
            return Task.FromResult(StoreAndAudit(
                context,
                idempotencyKey,
                intent,
                task,
                "delete",
                "user",
                context.PrincipalId.Value.ToString("D"),
                occurredAt));
        }
    }

    private bool Replay(
        TenantPrincipalContext context,
        string idempotencyKey,
        string intent,
        out TaskMutationResult result)
    {
        ValidateIdempotencyKey(idempotencyKey);
        if (!receipts.TryGetValue((context.TenantId.Value, idempotencyKey), out var receipt))
        {
            result = null!;
            return false;
        }

        result = string.Equals(receipt.Intent, intent, StringComparison.Ordinal)
            ? receipt.Result.Outcome == TaskMutationOutcome.Applied
                ? receipt.Result with { Outcome = TaskMutationOutcome.IdempotentReplay }
                : receipt.Result
            : new(TaskMutationOutcome.Conflict, receipt.Result.Task);
        return true;
    }

    private TaskMutationResult StoreAndAudit(
        TenantPrincipalContext context,
        string idempotencyKey,
        string intent,
        OpenLoopTask task,
        string operation,
        string sourceKind,
        string sourceReference,
        DateTimeOffset occurredAt)
    {
        var result = StoreReceipt(
            context,
            idempotencyKey,
            intent,
            new(TaskMutationOutcome.Applied, task));
        auditEntries.Add(new(
            Guid.CreateVersion7(),
            context.TenantId.Value,
            context.PrincipalId.Value,
            task.Id,
            operation,
            "applied",
            sourceKind,
            sourceReference,
            occurredAt));
        return result;
    }

    private TaskMutationResult StoreReceipt(
        TenantPrincipalContext context,
        string idempotencyKey,
        string intent,
        TaskMutationResult result)
    {
        receipts[(context.TenantId.Value, idempotencyKey)] = new(intent, result);
        return result;
    }

    private static void ValidateContext(TenantPrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TenantId.Value == Guid.Empty
            || context.PrincipalId.Value == Guid.Empty
            || !string.Equals(context.Purpose, OpenLoopsPolicy.Purpose, StringComparison.Ordinal))
        {
            throw new IdentityAccessDeniedException("The task access context is not authorized.");
        }
    }

    private static void ValidateIdempotencyKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length is < 8 or > 128)
        {
            throw new ArgumentException("An idempotency key between 8 and 128 characters is required.", nameof(key));
        }
    }

    private sealed record Receipt(string Intent, TaskMutationResult Result);
}
