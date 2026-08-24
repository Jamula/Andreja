using Andreja.Modules.Identity;

namespace Andreja.Modules.OpenLoops;

public enum OpenLoopTaskStatus
{
    Open = 1,
    Completed = 2,
}

public sealed class OpenLoopTask
{
    private OpenLoopTask()
    {
    }

    public OpenLoopTask(
        Guid id,
        TenantId tenantId,
        PrincipalId ownerPrincipalId,
        string title,
        string? details,
        DateTimeOffset? dueAt,
        string sourceKind,
        string sourceReference,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty
            || tenantId.Value == Guid.Empty
            || ownerPrincipalId.Value == Guid.Empty)
        {
            throw new ArgumentException("Task, tenant, and owner IDs are required.");
        }

        Id = id;
        TenantId = tenantId;
        OwnerPrincipalId = ownerPrincipalId;
        Title = NormalizeRequired(title, nameof(title), 200);
        Details = NormalizeOptional(details, nameof(details), 4000);
        DueAt = dueAt;
        SourceKind = NormalizeRequired(sourceKind, nameof(sourceKind), 32);
        SourceReference = NormalizeRequired(sourceReference, nameof(sourceReference), 200);
        CreatedAt = createdAt == default
            ? throw new ArgumentException("A creation time is required.", nameof(createdAt))
            : createdAt;
        Status = OpenLoopTaskStatus.Open;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public long Version { get; private set; }

    public TenantId TenantId { get; private set; }

    public PrincipalId OwnerPrincipalId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Details { get; private set; }

    public DateTimeOffset? DueAt { get; private set; }

    public OpenLoopTaskStatus Status { get; private set; }

    public string SourceKind { get; private set; } = string.Empty;

    public string SourceReference { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public void Complete(DateTimeOffset occurredAt)
    {
        if (Status != OpenLoopTaskStatus.Open)
        {
            throw new InvalidOperationException("Only an open task can be completed.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(occurredAt, CreatedAt);

        Status = OpenLoopTaskStatus.Completed;
        CompletedAt = occurredAt;
        Version++;
    }

    private static string NormalizeRequired(string value, string name, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        var normalized = value.Trim();
        return normalized.Length > maximumLength
            ? throw new ArgumentOutOfRangeException(name, $"Maximum length is {maximumLength}.")
            : normalized;
    }

    private static string? NormalizeOptional(string? value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length > maximumLength
            ? throw new ArgumentOutOfRangeException(name, $"Maximum length is {maximumLength}.")
            : normalized;
    }
}

public enum TaskMutationOutcome
{
    Applied,
    IdempotentReplay,
    NotFound,
    Conflict,
    Denied,
}

public sealed record TaskMutationResult(
    TaskMutationOutcome Outcome,
    OpenLoopTask? Task);

public sealed record TaskAuditEntry(
    Guid AuditId,
    Guid TenantId,
    Guid ActorId,
    Guid ResourceId,
    string Operation,
    string Outcome,
    string SourceKind,
    string SourceReference,
    DateTimeOffset OccurredAt);

public interface IOpenLoopsTaskStore
{
    Task<IReadOnlyList<OpenLoopTask>> ListAsync(
        TenantPrincipalContext context,
        CancellationToken cancellationToken = default);

    Task<TaskMutationResult> CreateAsync(
        TenantPrincipalContext context,
        OpenLoopTask task,
        Guid proposalId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<TaskMutationResult> CompleteAsync(
        TenantPrincipalContext context,
        Guid taskId,
        long expectedVersion,
        string idempotencyKey,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);

    Task<TaskMutationResult> DeleteAsync(
        TenantPrincipalContext context,
        Guid taskId,
        long expectedVersion,
        string idempotencyKey,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);
}
