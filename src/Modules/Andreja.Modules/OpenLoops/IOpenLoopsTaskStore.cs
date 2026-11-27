using Andreja.Modules.Identity;

namespace Andreja.Modules.OpenLoops;

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
