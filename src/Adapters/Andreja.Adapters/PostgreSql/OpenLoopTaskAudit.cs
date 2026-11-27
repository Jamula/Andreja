using Andreja.Modules.Identity;

namespace Andreja.Adapters.PostgreSql;

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
