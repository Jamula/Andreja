using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Microsoft.EntityFrameworkCore;

namespace Andreja.Adapters.PostgreSql;

public sealed class PostgreSqlOpenLoopsTaskStore(
    AndrejaIdentityDbContext database,
    ITenantPrincipalContextAccessor contextAccessor) : IOpenLoopsTaskStore
{
    public async Task<IReadOnlyList<OpenLoopTask>> ListAsync(
        TenantPrincipalContext context,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrentContext(context);
        return await database.OpenLoopTasks
            .Where(task => task.OwnerPrincipalId == context.PrincipalId)
            .OrderBy(task => task.Status)
            .ThenBy(task => task.DueAt)
            .ThenBy(task => task.CreatedAt)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<TaskMutationResult> CreateAsync(
        TenantPrincipalContext context,
        OpenLoopTask task,
        Guid proposalId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrentContext(context);
        ArgumentNullException.ThrowIfNull(task);
        var intent = $"create:{proposalId:D}";
        var replay = await FindReplayAsync(context, idempotencyKey, intent, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        if (task.TenantId != context.TenantId
            || task.OwnerPrincipalId != context.PrincipalId)
        {
            return await SaveReceiptAsync(
                context,
                idempotencyKey,
                intent,
                TaskMutationOutcome.Denied,
                null,
                cancellationToken);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        database.OpenLoopTasks.Add(task);
        database.OpenLoopTaskAudits.Add(new(
            Guid.CreateVersion7(),
            context.TenantId,
            context.PrincipalId,
            task.Id,
            "create",
            "applied",
            task.SourceKind,
            task.SourceReference,
            task.CreatedAt));
        database.OpenLoopTaskReceipts.Add(new(
            context.TenantId,
            idempotencyKey,
            intent,
            TaskMutationOutcome.Applied,
            task.Id,
            task.Version));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(TaskMutationOutcome.Applied, task);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            var concurrent = await FindReplayAsync(context, idempotencyKey, intent, cancellationToken);
            return concurrent ?? new(TaskMutationOutcome.Conflict, null);
        }
    }

    public Task<TaskMutationResult> CompleteAsync(
        TenantPrincipalContext context,
        Guid taskId,
        long expectedVersion,
        string idempotencyKey,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            context,
            taskId,
            expectedVersion,
            idempotencyKey,
            occurredAt,
            "complete",
            task => task.Complete(occurredAt),
            delete: false,
            cancellationToken);

    public Task<TaskMutationResult> DeleteAsync(
        TenantPrincipalContext context,
        Guid taskId,
        long expectedVersion,
        string idempotencyKey,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            context,
            taskId,
            expectedVersion,
            idempotencyKey,
            occurredAt,
            "delete",
            _ => { },
            delete: true,
            cancellationToken);

    private async Task<TaskMutationResult> MutateAsync(
        TenantPrincipalContext context,
        Guid taskId,
        long expectedVersion,
        string idempotencyKey,
        DateTimeOffset occurredAt,
        string operation,
        Action<OpenLoopTask> mutation,
        bool delete,
        CancellationToken cancellationToken)
    {
        EnsureCurrentContext(context);
        var intent = $"{operation}:{taskId:D}:{expectedVersion}";
        var replay = await FindReplayAsync(context, idempotencyKey, intent, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var task = await database.OpenLoopTasks.SingleOrDefaultAsync(
            candidate => candidate.Id == taskId,
            cancellationToken);
        if (task is null)
        {
            return await SaveReceiptAsync(
                context,
                idempotencyKey,
                intent,
                TaskMutationOutcome.NotFound,
                null,
                cancellationToken);
        }

        if (task.OwnerPrincipalId != context.PrincipalId)
        {
            return await SaveReceiptAsync(
                context,
                idempotencyKey,
                intent,
                TaskMutationOutcome.Denied,
                null,
                cancellationToken);
        }

        if (task.Version != expectedVersion
            || operation == "complete" && task.Status != OpenLoopTaskStatus.Open)
        {
            return await SaveReceiptAsync(
                context,
                idempotencyKey,
                intent,
                TaskMutationOutcome.Conflict,
                task,
                cancellationToken);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        mutation(task);
        database.OpenLoopTaskAudits.Add(new(
            Guid.CreateVersion7(),
            context.TenantId,
            context.PrincipalId,
            task.Id,
            operation,
            "applied",
            "user",
            context.PrincipalId.Value.ToString("D"),
            occurredAt));
        database.OpenLoopTaskReceipts.Add(new(
            context.TenantId,
            idempotencyKey,
            intent,
            TaskMutationOutcome.Applied,
            task.Id,
            task.Version));
        if (delete)
        {
            database.OpenLoopTasks.Remove(task);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(TaskMutationOutcome.Applied, task);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new(TaskMutationOutcome.Conflict, null);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            var concurrent = await FindReplayAsync(context, idempotencyKey, intent, cancellationToken);
            return concurrent ?? new(TaskMutationOutcome.Conflict, null);
        }
    }

    private async Task<TaskMutationResult?> FindReplayAsync(
        TenantPrincipalContext context,
        string idempotencyKey,
        string intent,
        CancellationToken cancellationToken)
    {
        ValidateIdempotencyKey(idempotencyKey);
        var receipt = await database.OpenLoopTaskReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        OpenLoopTask? task = null;
        if (receipt.TaskId.HasValue)
        {
            task = await database.OpenLoopTasks
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == receipt.TaskId, cancellationToken);
        }

        if (!string.Equals(receipt.Intent, intent, StringComparison.Ordinal))
        {
            return new(TaskMutationOutcome.Conflict, task);
        }

        return new(
            receipt.Outcome == TaskMutationOutcome.Applied
                ? TaskMutationOutcome.IdempotentReplay
                : receipt.Outcome,
            task);
    }

    private async Task<TaskMutationResult> SaveReceiptAsync(
        TenantPrincipalContext context,
        string idempotencyKey,
        string intent,
        TaskMutationOutcome outcome,
        OpenLoopTask? task,
        CancellationToken cancellationToken)
    {
        database.OpenLoopTaskReceipts.Add(new(
            context.TenantId,
            idempotencyKey,
            intent,
            outcome,
            task?.Id,
            task?.Version));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return new(outcome, task);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            return await FindReplayAsync(context, idempotencyKey, intent, cancellationToken)
                ?? new(TaskMutationOutcome.Conflict, null);
        }
    }

    private void EnsureCurrentContext(TenantPrincipalContext supplied)
    {
        OpenLoopsPolicy.Require(supplied);
        var current = TenantPrincipalContext.Require(contextAccessor);
        if (current != supplied)
        {
            throw new IdentityAccessDeniedException(
                "The supplied task context does not match the resolved request context.");
        }
    }

    private static void ValidateIdempotencyKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length is < 8 or > 128)
        {
            throw new ArgumentException("An idempotency key between 8 and 128 characters is required.", nameof(key));
        }
    }
}

internal sealed class OpenLoopTaskAudit
{
    private OpenLoopTaskAudit()
    {
    }

    public OpenLoopTaskAudit(
        Guid id,
        TenantId tenantId,
        PrincipalId actorId,
        Guid resourceId,
        string operation,
        string outcome,
        string sourceKind,
        string sourceReference,
        DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        ActorId = actorId;
        ResourceId = resourceId;
        Operation = operation;
        Outcome = outcome;
        SourceKind = sourceKind;
        SourceReference = sourceReference;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public PrincipalId ActorId { get; private set; }

    public Guid ResourceId { get; private set; }

    public string Operation { get; private set; } = string.Empty;

    public string Outcome { get; private set; } = string.Empty;

    public string SourceKind { get; private set; } = string.Empty;

    public string SourceReference { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }
}

internal sealed class OpenLoopTaskReceipt
{
    private OpenLoopTaskReceipt()
    {
    }

    public OpenLoopTaskReceipt(
        TenantId tenantId,
        string idempotencyKey,
        string intent,
        TaskMutationOutcome outcome,
        Guid? taskId,
        long? taskVersion)
    {
        TenantId = tenantId;
        IdempotencyKey = idempotencyKey;
        Intent = intent;
        Outcome = outcome;
        TaskId = taskId;
        TaskVersion = taskVersion;
    }

    public TenantId TenantId { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string Intent { get; private set; } = string.Empty;

    public TaskMutationOutcome Outcome { get; private set; }

    public Guid? TaskId { get; private set; }

    public long? TaskVersion { get; private set; }
}
