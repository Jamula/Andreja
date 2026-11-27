using Andreja.Modules.Identity;
using Andreja.Platform.Contracts.Proposals;

namespace Andreja.Adapters.PostgreSql;

internal sealed class ProposalRecord
{
    private ProposalRecord()
    {
    }

    public Guid Id { get; private set; }
    public long Version { get; private set; }
    public TenantId TenantId { get; private set; }
    public PrincipalId ActorId { get; private set; }
    public AppUserId ActorAppUserId { get; private set; }
    public PrincipalId SourceActorId { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string SourceKind { get; private set; } = string.Empty;
    public string SourceReference { get; private set; } = string.Empty;
    public string Operation { get; private set; } = string.Empty;
    public string ResourceReference { get; private set; } = string.Empty;
    public string CanonicalPayload { get; private set; } = string.Empty;
    public string PayloadDigest { get; private set; } = string.Empty;
    public string BeforeCanonical { get; private set; } = string.Empty;
    public string AfterCanonical { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public ProposalState State { get; private set; }
    public Guid? ActiveTaskId { get; private set; }

    public static ProposalRecord FromDomain(Proposal proposal, AppUserId actorAppUserId) =>
        new()
        {
            Id = proposal.ProposalId,
            Version = proposal.Version,
            TenantId = new TenantId(proposal.TenantId),
            ActorId = new PrincipalId(proposal.ActorId),
            ActorAppUserId = actorAppUserId,
            SourceActorId = new PrincipalId(proposal.Source.ActorId),
            Purpose = proposal.Purpose,
            SourceKind = proposal.Source.Kind,
            SourceReference = proposal.Source.Reference,
            Operation = proposal.Operation.Operation,
            ResourceReference = proposal.Operation.ResourceReference,
            CanonicalPayload = proposal.Operation.CanonicalPayload,
            PayloadDigest = proposal.Operation.PayloadDigest,
            BeforeCanonical = proposal.Diff.BeforeCanonical,
            AfterCanonical = proposal.Diff.AfterCanonical,
            CreatedAt = proposal.CreatedAt,
            ExpiresAt = proposal.ExpiresAt,
            State = proposal.State,
        };

    public Proposal ToDomain() =>
        new(
            Id,
            Version,
            TenantId.Value,
            ActorId.Value,
            Purpose,
            new(SourceKind, SourceReference, SourceActorId.Value),
            new(Operation, ResourceReference, CanonicalPayload, PayloadDigest),
            new(BeforeCanonical, AfterCanonical),
            CreatedAt,
            ExpiresAt,
            State);

    public void Transition(ProposalAction action)
    {
        State = action switch
        {
            ProposalAction.Reject => ProposalState.Rejected,
            ProposalAction.Cancel => ProposalState.Cancelled,
            _ => throw new InvalidOperationException(
                "Confirmation must use the atomic Open Loops confirmation path."),
        };
        Version++;
    }

    public void Confirm(Guid taskId)
    {
        State = ProposalState.Confirmed;
        ActiveTaskId = taskId;
        Version++;
    }

    public void Expire()
    {
        State = ProposalState.Expired;
        Version++;
    }

    public void DetachTask() => ActiveTaskId = null;
}
