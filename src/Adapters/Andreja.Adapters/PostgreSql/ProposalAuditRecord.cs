using Andreja.Modules.Identity;
using Andreja.Platform.Contracts.Proposals;

namespace Andreja.Adapters.PostgreSql;

internal sealed class ProposalAuditRecord
{
    private ProposalAuditRecord()
    {
    }

    public ProposalAuditRecord(
        Guid id,
        TenantId tenantId,
        PrincipalId actorId,
        Guid proposalId,
        long proposalVersion,
        ProposalAction action,
        ProposalTransitionOutcome outcome,
        string sourceKind,
        string sourceReference,
        DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        ActorId = actorId;
        ProposalId = proposalId;
        ProposalVersion = proposalVersion;
        Action = action;
        Outcome = outcome;
        SourceKind = sourceKind;
        SourceReference = sourceReference;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public PrincipalId ActorId { get; private set; }
    public Guid ProposalId { get; private set; }
    public long ProposalVersion { get; private set; }
    public ProposalAction Action { get; private set; }
    public ProposalTransitionOutcome Outcome { get; private set; }
    public string SourceKind { get; private set; } = string.Empty;
    public string SourceReference { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }

    public static ProposalAuditRecord FromDomain(ProposalAuditEntry entry) =>
        new(
            entry.AuditId,
            new TenantId(entry.TenantId),
            new PrincipalId(entry.ActorId),
            entry.ProposalId,
            entry.ProposalVersion,
            entry.Action,
            entry.Outcome,
            entry.SourceKind,
            entry.SourceReference,
            entry.OccurredAt);
}
