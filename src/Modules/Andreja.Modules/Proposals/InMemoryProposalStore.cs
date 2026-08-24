using Andreja.Platform.Contracts.Proposals;
using System.Security.Cryptography;
using System.Text;

namespace Andreja.Modules.Proposals;

public sealed class InMemoryProposalStore : IProposalStore, IProposalAuditSink
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Proposal> proposals = [];
    private readonly Dictionary<(Guid ProposalId, string Key), TransitionReceipt> receipts = [];
    private readonly HashSet<Guid> auditEntryIds = [];
    private readonly List<ProposalAuditEntry> auditEntries = [];

    public IReadOnlyList<ProposalAuditEntry> AuditEntries
    {
        get
        {
            lock (gate)
            {
                return auditEntries.ToArray();
            }
        }
    }

    public ValueTask<bool> TryCreateAsync(
        Proposal proposal,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateProposal(proposal);

        lock (gate)
        {
            return ValueTask.FromResult(proposals.TryAdd(proposal.ProposalId, proposal));
        }
    }

    public ValueTask<Proposal?> GetAsync(
        Guid tenantId,
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            proposals.TryGetValue(proposalId, out var proposal);
            return ValueTask.FromResult(proposal?.TenantId == tenantId ? proposal : null);
        }
    }

    public ValueTask<ProposalTransitionResult> TryTransitionAsync(
        ProposalTransitionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                return ValueTask.FromResult(new ProposalTransitionResult(
                    ProposalTransitionOutcome.Conflict,
                    null));
            }

            var receiptKey = (request.ProposalId, request.IdempotencyKey);
            if (receipts.TryGetValue(receiptKey, out var receipt))
            {
                var exactRetry = IsSameIntent(receipt.Request, request);
                return ValueTask.FromResult(exactRetry
                    ? receipt.Result with { Outcome = ProposalTransitionOutcome.IdempotentReplay }
                    : new ProposalTransitionResult(ProposalTransitionOutcome.Conflict, receipt.Result.Proposal));
            }

            if (!proposals.TryGetValue(request.ProposalId, out var proposal))
            {
                return ValueTask.FromResult(StoreReceipt(
                    request,
                    new ProposalTransitionResult(ProposalTransitionOutcome.NotFound, null)));
            }

            if (proposal.TenantId != request.TenantId || proposal.ActorId != request.ActorId)
            {
                return ValueTask.FromResult(StoreAndAudit(
                    request,
                    proposal,
                    ProposalTransitionOutcome.Denied));
            }

            if (request.OccurredAt >= proposal.ExpiresAt)
            {
                var expired = proposal.State == ProposalState.Pending
                    ? proposal with { State = ProposalState.Expired, Version = proposal.Version + 1 }
                    : proposal;
                proposals[proposal.ProposalId] = expired;
                return ValueTask.FromResult(StoreAndAudit(
                    request,
                    expired,
                    ProposalTransitionOutcome.Expired));
            }

            if (proposal.Version != request.ExpectedVersion)
            {
                return ValueTask.FromResult(StoreAndAudit(
                    request,
                    proposal,
                    ProposalTransitionOutcome.Conflict));
            }

            if (proposal.State != ProposalState.Pending)
            {
                return ValueTask.FromResult(StoreAndAudit(
                    request,
                    proposal,
                    ProposalTransitionOutcome.InvalidState));
            }

            var targetState = request.Action switch
            {
                ProposalAction.Confirm => ProposalState.Confirmed,
                ProposalAction.Reject => ProposalState.Rejected,
                ProposalAction.Cancel => ProposalState.Cancelled,
                _ => throw new ArgumentOutOfRangeException(nameof(request), request.Action, "Unknown action."),
            };
            var transitioned = proposal with { State = targetState, Version = proposal.Version + 1 };
            proposals[proposal.ProposalId] = transitioned;
            return ValueTask.FromResult(StoreAndAudit(
                request,
                transitioned,
                ProposalTransitionOutcome.Applied));
        }
    }

    public ValueTask AppendAsync(
        ProposalAuditEntry entry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!auditEntryIds.Add(entry.AuditId))
            {
                throw new InvalidOperationException("Proposal audit entries are append-only and unique.");
            }

            auditEntries.Add(entry);
        }

        return ValueTask.CompletedTask;
    }

    private ProposalTransitionResult StoreAndAudit(
        ProposalTransitionRequest request,
        Proposal proposal,
        ProposalTransitionOutcome outcome)
    {
        var result = StoreReceipt(request, new ProposalTransitionResult(outcome, proposal));
        var entry = new ProposalAuditEntry(
            Guid.CreateVersion7(),
            proposal.ProposalId,
            proposal.Version,
            proposal.TenantId,
            request.ActorId,
            request.Action,
            outcome,
            proposal.Source.Kind,
            proposal.Source.Reference,
            request.OccurredAt);
        auditEntryIds.Add(entry.AuditId);
        auditEntries.Add(entry);
        return result;
    }

    private ProposalTransitionResult StoreReceipt(
        ProposalTransitionRequest request,
        ProposalTransitionResult result)
    {
        receipts[(request.ProposalId, request.IdempotencyKey)] = new(request, result);
        return result;
    }

    private static void ValidateProposal(Proposal proposal)
    {
        if (proposal.ProposalId == Guid.Empty
            || proposal.Version < 1
            || proposal.State != ProposalState.Pending
            || proposal.ExpiresAt <= proposal.CreatedAt
            || !string.Equals(
                proposal.Operation.CanonicalPayload,
                proposal.Diff.AfterCanonical,
                StringComparison.Ordinal)
            || !string.Equals(
                proposal.Operation.PayloadDigest,
                Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(proposal.Operation.CanonicalPayload))),
                StringComparison.Ordinal))
        {
            throw new ArgumentException("The proposal is invalid.", nameof(proposal));
        }
    }

    private static bool IsSameIntent(
        ProposalTransitionRequest first,
        ProposalTransitionRequest second) =>
        first.ProposalId == second.ProposalId
        && first.ExpectedVersion == second.ExpectedVersion
        && first.TenantId == second.TenantId
        && first.ActorId == second.ActorId
        && first.Action == second.Action
        && string.Equals(first.IdempotencyKey, second.IdempotencyKey, StringComparison.Ordinal);

    private sealed record TransitionReceipt(
        ProposalTransitionRequest Request,
        ProposalTransitionResult Result);
}
