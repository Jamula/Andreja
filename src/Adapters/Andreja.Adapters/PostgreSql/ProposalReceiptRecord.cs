using Andreja.Modules.Identity;
using Andreja.Platform.Contracts.Proposals;

namespace Andreja.Adapters.PostgreSql;

internal sealed class ProposalReceiptRecord
{
    private ProposalReceiptRecord()
    {
    }

    public TenantId TenantId { get; private set; }
    public PrincipalId ActorId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Intent { get; private set; } = string.Empty;
    public Guid ProposalId { get; private set; }
    public long ProposalVersion { get; private set; }
    public ProposalTransitionOutcome Outcome { get; private set; }
    public Guid? TaskId { get; private set; }
    public long? TaskVersion { get; private set; }

    public static ProposalReceiptRecord ForTransition(
        ProposalTransitionRequest request,
        string intent,
        ProposalTransitionOutcome outcome,
        long proposalVersion) =>
        new()
        {
            TenantId = new TenantId(request.TenantId),
            ActorId = new PrincipalId(request.ActorId),
            IdempotencyKey = request.IdempotencyKey,
            Intent = intent,
            ProposalId = request.ProposalId,
            ProposalVersion = proposalVersion,
            Outcome = outcome,
        };

    public static ProposalReceiptRecord ForConfirmation(
        TenantPrincipalContext context,
        Guid proposalId,
        long expectedVersion,
        string idempotencyKey,
        string intent,
        long proposalVersion,
        Guid taskId,
        long taskVersion) =>
        new()
        {
            TenantId = context.TenantId,
            ActorId = context.PrincipalId,
            IdempotencyKey = idempotencyKey,
            Intent = intent,
            ProposalId = proposalId,
            ProposalVersion = proposalVersion,
            Outcome = ProposalTransitionOutcome.Applied,
            TaskId = taskId,
            TaskVersion = taskVersion,
        };

    public void DetachTask()
    {
        TaskId = null;
        TaskVersion = null;
    }
}
