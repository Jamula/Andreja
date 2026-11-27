using Andreja.Modules.Identity;

namespace Andreja.Modules.OpenLoops;

public interface IOpenLoopsProposalConfirmationStore
{
    Task<ProposalConfirmationResult> ConfirmAsync(
        TenantPrincipalContext context,
        Guid proposalId,
        long expectedVersion,
        string idempotencyKey,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);
}
