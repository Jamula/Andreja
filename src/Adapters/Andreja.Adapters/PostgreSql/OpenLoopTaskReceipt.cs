using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;

namespace Andreja.Adapters.PostgreSql;

internal sealed class OpenLoopTaskReceipt
{
    private OpenLoopTaskReceipt()
    {
    }

    public OpenLoopTaskReceipt(
        TenantId tenantId,
        PrincipalId actorId,
        string idempotencyKey,
        string intent,
        TaskMutationOutcome outcome,
        Guid? taskId,
        long? taskVersion)
    {
        TenantId = tenantId;
        ActorId = actorId;
        IdempotencyKey = idempotencyKey;
        Intent = intent;
        Outcome = outcome;
        TaskId = taskId;
        TaskVersion = taskVersion;
    }

    public TenantId TenantId { get; private set; }

    public PrincipalId ActorId { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string Intent { get; private set; } = string.Empty;

    public TaskMutationOutcome Outcome { get; private set; }

    public Guid? TaskId { get; private set; }

    public long? TaskVersion { get; private set; }
}
