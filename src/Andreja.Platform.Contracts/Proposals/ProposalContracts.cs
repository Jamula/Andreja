namespace Andreja.Platform.Contracts.Proposals;

public enum ProposalState
{
    Pending,
    Confirmed,
    Rejected,
    Cancelled,
    Expired,
}

public enum ProposalAction
{
    Confirm,
    Reject,
    Cancel,
}

public sealed record ProposalOperation(
    string Operation,
    string ResourceReference,
    string CanonicalPayload,
    string PayloadDigest);

public sealed record ProposalDiff(
    string BeforeCanonical,
    string AfterCanonical);

public sealed record ProposalSource(
    string Kind,
    string Reference,
    Guid ActorId);

public sealed record Proposal(
    Guid ProposalId,
    long Version,
    Guid TenantId,
    Guid ActorId,
    string Purpose,
    ProposalSource Source,
    ProposalOperation Operation,
    ProposalDiff Diff,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    ProposalState State);

public sealed record ProposalTransitionRequest(
    Guid ProposalId,
    long ExpectedVersion,
    Guid TenantId,
    Guid ActorId,
    ProposalAction Action,
    string IdempotencyKey,
    DateTimeOffset OccurredAt);

public enum ProposalTransitionOutcome
{
    Applied,
    IdempotentReplay,
    NotFound,
    Conflict,
    Expired,
    Denied,
    InvalidState,
}

public sealed record ProposalTransitionResult(
    ProposalTransitionOutcome Outcome,
    Proposal? Proposal);

public sealed record ProposalAuditEntry(
    Guid AuditId,
    Guid ProposalId,
    long ProposalVersion,
    Guid TenantId,
    Guid ActorId,
    ProposalAction Action,
    ProposalTransitionOutcome Outcome,
    string SourceKind,
    string SourceReference,
    DateTimeOffset OccurredAt);

public interface IProposalStore
{
    ValueTask<bool> TryCreateAsync(Proposal proposal, CancellationToken cancellationToken);

    ValueTask<Proposal?> GetAsync(
        Guid tenantId,
        Guid proposalId,
        CancellationToken cancellationToken);

    ValueTask<ProposalTransitionResult> TryTransitionAsync(
        ProposalTransitionRequest request,
        CancellationToken cancellationToken);
}

public interface IProposalAuditSink
{
    ValueTask AppendAsync(ProposalAuditEntry entry, CancellationToken cancellationToken);
}
