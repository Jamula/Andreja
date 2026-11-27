using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Andreja.Platform.Contracts.Proposals;
using Microsoft.EntityFrameworkCore;

namespace Andreja.Adapters.PostgreSql;

public sealed class PostgreSqlProposalStore
    : IProposalStore, IProposalAuditSink, IOpenLoopsProposalConfirmationStore
{
    private readonly AndrejaIdentityDbContext database;
    private readonly ITenantPrincipalContextAccessor contextAccessor;
    private readonly IProposalConfirmationFaultInjector? faultInjector;

    public PostgreSqlProposalStore(
        AndrejaIdentityDbContext database,
        ITenantPrincipalContextAccessor contextAccessor)
        : this(database, contextAccessor, null)
    {
    }

    internal PostgreSqlProposalStore(
        AndrejaIdentityDbContext database,
        ITenantPrincipalContextAccessor contextAccessor,
        IProposalConfirmationFaultInjector? faultInjector)
    {
        this.database = database;
        this.contextAccessor = contextAccessor;
        this.faultInjector = faultInjector;
    }

    public async ValueTask<bool> TryCreateAsync(
        Proposal proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal.ProposalId == Guid.Empty
            || proposal.Version < 1
            || proposal.State != ProposalState.Pending
            || proposal.ExpiresAt <= proposal.CreatedAt)
        {
            throw new ArgumentException("The proposal is invalid.", nameof(proposal));
        }

        var context = RequireCurrent(proposal.TenantId, proposal.ActorId, proposal.Purpose);
        if (!await HasActorBindingAsync(context, cancellationToken).ConfigureAwait(false))
        {
            throw new IdentityAccessDeniedException(
                "The proposal actor is not an active tenant membership.");
        }

        _ = OpenLoopsTaskApplication.MaterializeTask(context, proposal);
        database.Proposals.Add(ProposalRecord.FromDomain(proposal, context.AppUserId));
        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            if (await database.Proposals
                    .AsNoTracking()
                    .AnyAsync(candidate => candidate.Id == proposal.ProposalId, cancellationToken)
                    .ConfigureAwait(false))
            {
                return false;
            }

            throw;
        }
    }

    public async ValueTask<Proposal?> GetAsync(
        Guid tenantId,
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        var current = TenantPrincipalContext.Require(contextAccessor);
        OpenLoopsPolicy.Require(current);
        if (current.TenantId.Value != tenantId)
        {
            throw new IdentityAccessDeniedException(
                "The requested proposal tenant does not match the resolved request context.");
        }

        if (!await HasActorBindingAsync(current, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var proposal = await database.Proposals
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == proposalId, cancellationToken)
            .ConfigureAwait(false);
        return proposal?.ActorAppUserId == current.AppUserId
               && proposal.ActorId == current.PrincipalId
            ? proposal.ToDomain()
            : null;
    }

    public async ValueTask<ProposalTransitionResult> TryTransitionAsync(
        ProposalTransitionRequest request,
        CancellationToken cancellationToken)
    {
        var context = RequireCurrent(request.TenantId, request.ActorId, OpenLoopsPolicy.Purpose);
        ValidateIdempotencyKey(request.IdempotencyKey);
        if (!await HasActorBindingAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return new(ProposalTransitionOutcome.Denied, null);
        }

        var intent = TransitionIntent(request);
        var replay = await FindTransitionReplayAsync(
            context,
            request.IdempotencyKey,
            intent,
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay;
        }

        var proposal = await database.Proposals
            .SingleOrDefaultAsync(candidate => candidate.Id == request.ProposalId, cancellationToken)
            .ConfigureAwait(false);
        if (proposal is null)
        {
            return new(ProposalTransitionOutcome.NotFound, null);
        }

        if (proposal.ActorAppUserId != context.AppUserId)
        {
            return new(ProposalTransitionOutcome.Denied, null);
        }

        var outcome = ValidateTransition(proposal, request);
        if (outcome is null && request.Action == ProposalAction.Confirm)
        {
            outcome = ProposalTransitionOutcome.Conflict;
        }

        if (outcome is null)
        {
            proposal.Transition(request.Action);
            outcome = ProposalTransitionOutcome.Applied;
        }
        else if (outcome == ProposalTransitionOutcome.Expired
                 && proposal.State == ProposalState.Pending)
        {
            proposal.Expire();
        }

        AddProposalAudit(proposal, request.ActorId, request.Action, outcome.Value, request.OccurredAt);
        database.ProposalReceipts.Add(ProposalReceiptRecord.ForTransition(
            request,
            intent,
            outcome.Value,
            proposal.Version));
        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new(outcome.Value, proposal.ToDomain());
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            return new(ProposalTransitionOutcome.Conflict, null);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            return await FindTransitionReplayAsync(
                    context,
                    request.IdempotencyKey,
                    intent,
                    cancellationToken).ConfigureAwait(false)
                ?? new(ProposalTransitionOutcome.Conflict, null);
        }
    }

    public async Task<ProposalConfirmationResult> ConfirmAsync(
        TenantPrincipalContext context,
        Guid proposalId,
        long expectedVersion,
        string idempotencyKey,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrent(context);
        ValidateIdempotencyKey(idempotencyKey);
        if (!await HasActorBindingAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return new(ProposalTransitionOutcome.Denied, null, null);
        }

        var intent = ConfirmationIntent(proposalId, expectedVersion);
        var replay = await FindConfirmationReplayAsync(
            context,
            idempotencyKey,
            intent,
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay;
        }

        var transaction = await database.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var proposal = await database.Proposals
                .SingleOrDefaultAsync(candidate => candidate.Id == proposalId, cancellationToken)
                .ConfigureAwait(false);
            if (proposal is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new(ProposalTransitionOutcome.NotFound, null, null);
            }

            if (proposal.ActorAppUserId != context.AppUserId)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new(ProposalTransitionOutcome.Denied, null, null);
            }

            var request = new ProposalTransitionRequest(
                proposalId,
                expectedVersion,
                context.TenantId.Value,
                context.PrincipalId.Value,
                ProposalAction.Confirm,
                idempotencyKey,
                occurredAt);
            var invalid = ValidateTransition(proposal, request);
            if (invalid is not null)
            {
                if (invalid == ProposalTransitionOutcome.Expired
                    && proposal.State == ProposalState.Pending)
                {
                    proposal.Expire();
                }

                AddProposalAudit(proposal, request.ActorId, request.Action, invalid.Value, occurredAt);
                database.ProposalReceipts.Add(ProposalReceiptRecord.ForTransition(
                    request,
                    intent,
                    invalid.Value,
                    proposal.Version));
                try
                {
                    await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return new(invalid.Value, null, proposal.ToDomain());
                }
                catch (DbUpdateException)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    database.ChangeTracker.Clear();
                    return await FindConfirmationReplayAsync(
                            context,
                            idempotencyKey,
                            intent,
                            cancellationToken).ConfigureAwait(false)
                        ?? new(ProposalTransitionOutcome.Conflict, null, null);
                }
            }

            var task = OpenLoopsTaskApplication.MaterializeTask(context, proposal.ToDomain());
            proposal.Confirm(task.Id);
            database.OpenLoopTasks.Add(task);
            database.OpenLoopTaskAudits.Add(new(
                Guid.CreateVersion7(),
                context.TenantId,
                context.PrincipalId,
                task.Id,
                "create",
                "applied",
                task.SourceKind,
                task.SourceReference,
                occurredAt));
            AddProposalAudit(
                proposal,
                context.PrincipalId.Value,
                ProposalAction.Confirm,
                ProposalTransitionOutcome.Applied,
                occurredAt);
            database.ProposalReceipts.Add(ProposalReceiptRecord.ForConfirmation(
                context,
                proposalId,
                expectedVersion,
                idempotencyKey,
                intent,
                proposal.Version,
                task.Id,
                task.Version));

            try
            {
                await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                if (faultInjector is not null)
                {
                    await faultInjector.OnCheckpointAsync(
                        ProposalConfirmationCheckpoint.BeforeCommit,
                        cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                if (faultInjector is not null)
                {
                    await faultInjector.OnCheckpointAsync(
                        ProposalConfirmationCheckpoint.AfterCommit,
                        cancellationToken).ConfigureAwait(false);
                }

                return new(ProposalTransitionOutcome.Applied, task, proposal.ToDomain());
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                database.ChangeTracker.Clear();
                return await FindConfirmationReplayAsync(
                        context,
                        idempotencyKey,
                        intent,
                        cancellationToken).ConfigureAwait(false)
                    ?? new(ProposalTransitionOutcome.Conflict, null, null);
            }
        }
    }

    public async ValueTask AppendAsync(
        ProposalAuditEntry entry,
        CancellationToken cancellationToken)
    {
        var context = RequireCurrent(entry.TenantId, entry.ActorId, OpenLoopsPolicy.Purpose);
        EnsureCurrent(context);
        database.ProposalAudits.Add(ProposalAuditRecord.FromDomain(entry));
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProposalTransitionResult?> FindTransitionReplayAsync(
        TenantPrincipalContext context,
        string idempotencyKey,
        string intent,
        CancellationToken cancellationToken)
    {
        var receipt = await FindReceiptAsync(context, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is null)
        {
            return null;
        }

        var proposal = await database.Proposals
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == receipt.ProposalId, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(receipt.Intent, intent, StringComparison.Ordinal))
        {
            return new(ProposalTransitionOutcome.Conflict, proposal?.ToDomain());
        }

        return new(
            receipt.Outcome == ProposalTransitionOutcome.Applied
                ? ProposalTransitionOutcome.IdempotentReplay
                : receipt.Outcome,
            proposal?.ToDomain());
    }

    private async Task<ProposalConfirmationResult?> FindConfirmationReplayAsync(
        TenantPrincipalContext context,
        string idempotencyKey,
        string intent,
        CancellationToken cancellationToken)
    {
        var receipt = await FindReceiptAsync(context, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is null)
        {
            return null;
        }

        var proposal = await database.Proposals
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == receipt.ProposalId, cancellationToken)
            .ConfigureAwait(false);
        OpenLoopTask? task = null;
        if (receipt.TaskId is Guid taskId)
        {
            task = await database.OpenLoopTasks
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == taskId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!string.Equals(receipt.Intent, intent, StringComparison.Ordinal))
        {
            return new(ProposalTransitionOutcome.Conflict, task, proposal?.ToDomain());
        }

        return new(
            receipt.Outcome == ProposalTransitionOutcome.Applied
                ? ProposalTransitionOutcome.IdempotentReplay
                : receipt.Outcome,
            task,
            proposal?.ToDomain());
    }

    private Task<ProposalReceiptRecord?> FindReceiptAsync(
        TenantPrincipalContext context,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        database.ProposalReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.ActorId == context.PrincipalId
                    && candidate.IdempotencyKey == idempotencyKey,
                cancellationToken);

    private Task<bool> HasActorBindingAsync(
        TenantPrincipalContext context,
        CancellationToken cancellationToken) =>
        database.Memberships
            .AsNoTracking()
            .AnyAsync(
                membership =>
                    membership.AppUserId == context.AppUserId
                    && membership.PrincipalId == context.PrincipalId
                    && membership.Status == MembershipStatus.Active,
                cancellationToken);

    private static ProposalTransitionOutcome? ValidateTransition(
        ProposalRecord proposal,
        ProposalTransitionRequest request)
    {
        if (proposal.TenantId.Value != request.TenantId
            || proposal.ActorId.Value != request.ActorId)
        {
            return ProposalTransitionOutcome.Denied;
        }

        if (request.OccurredAt >= proposal.ExpiresAt)
        {
            return ProposalTransitionOutcome.Expired;
        }

        if (proposal.Version != request.ExpectedVersion)
        {
            return ProposalTransitionOutcome.Conflict;
        }

        return proposal.State == ProposalState.Pending
            ? null
            : ProposalTransitionOutcome.InvalidState;
    }

    private void AddProposalAudit(
        ProposalRecord proposal,
        Guid actorId,
        ProposalAction action,
        ProposalTransitionOutcome outcome,
        DateTimeOffset occurredAt) =>
        database.ProposalAudits.Add(new(
            Guid.CreateVersion7(),
            proposal.TenantId,
            new PrincipalId(actorId),
            proposal.Id,
            proposal.Version,
            action,
            outcome,
            proposal.SourceKind,
            proposal.SourceReference,
            occurredAt));

    private TenantPrincipalContext RequireCurrent(
        Guid tenantId,
        Guid actorId,
        string purpose)
    {
        var current = TenantPrincipalContext.Require(contextAccessor);
        if (current.TenantId.Value != tenantId
            || current.PrincipalId.Value != actorId
            || !string.Equals(current.Purpose, purpose, StringComparison.Ordinal))
        {
            throw new IdentityAccessDeniedException(
                "The proposal does not match the resolved request context.");
        }

        OpenLoopsPolicy.Require(current);
        return current;
    }

    private void EnsureCurrent(TenantPrincipalContext supplied)
    {
        OpenLoopsPolicy.Require(supplied);
        if (TenantPrincipalContext.Require(contextAccessor) != supplied)
        {
            throw new IdentityAccessDeniedException(
                "The supplied proposal context does not match the resolved request context.");
        }
    }

    private static string ConfirmationIntent(Guid proposalId, long expectedVersion) =>
        $"confirm:{proposalId:D}:{expectedVersion}";

    private static string TransitionIntent(ProposalTransitionRequest request) =>
        $"{request.Action.ToString().ToLowerInvariant()}:{request.ProposalId:D}:{request.ExpectedVersion}";

    private static void ValidateIdempotencyKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length is < 8 or > 128)
        {
            throw new ArgumentException(
                "An idempotency key between 8 and 128 characters is required.",
                nameof(key));
        }
    }
}
