using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Andreja.Platform.Contracts.Proposals;
using Microsoft.EntityFrameworkCore;

namespace Andreja.Adapters.PostgreSql;

public sealed class PostgreSqlProposalStore(
    AndrejaIdentityDbContext database,
    ITenantPrincipalContextAccessor contextAccessor,
    IProposalConfirmationFaultInjector? faultInjector = null)
    : IProposalStore, IProposalAuditSink, IOpenLoopsProposalConfirmationStore
{
    public async ValueTask<bool> TryCreateAsync(
        Proposal proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var context = RequireCurrent(proposal.TenantId, proposal.ActorId, proposal.Purpose);
        _ = OpenLoopsTaskApplication.MaterializeTask(context, proposal);
        database.Proposals.Add(ProposalRecord.FromDomain(proposal, context.AppUserId));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            if (await database.Proposals
                    .AsNoTracking()
                    .AnyAsync(candidate => candidate.Id == proposal.ProposalId, cancellationToken))
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
        if (current.TenantId.Value != tenantId)
        {
            throw new IdentityAccessDeniedException(
                "The requested proposal tenant does not match the resolved request context.");
        }

        var proposal = await database.Proposals
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == proposalId, cancellationToken);
        return proposal?.ActorAppUserId == current.AppUserId
            ? proposal.ToDomain()
            : null;
    }

    public async ValueTask<ProposalTransitionResult> TryTransitionAsync(
        ProposalTransitionRequest request,
        CancellationToken cancellationToken)
    {
        var context = RequireCurrent(request.TenantId, request.ActorId, OpenLoopsPolicy.Purpose);
        ValidateIdempotencyKey(request.IdempotencyKey);
        var intent = TransitionIntent(request);
        var replay = await FindTransitionReplayAsync(
            context,
            request.IdempotencyKey,
            intent,
            cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var proposal = await database.Proposals
            .SingleOrDefaultAsync(candidate => candidate.Id == request.ProposalId, cancellationToken);
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
            await database.SaveChangesAsync(cancellationToken);
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
                    cancellationToken)
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
        var intent = ConfirmationIntent(proposalId, expectedVersion);
        var replay = await FindConfirmationReplayAsync(
            context,
            idempotencyKey,
            intent,
            cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var proposal = await database.Proposals
            .SingleOrDefaultAsync(candidate => candidate.Id == proposalId, cancellationToken);
        if (proposal is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(ProposalTransitionOutcome.NotFound, null, null);
        }

        if (proposal.ActorAppUserId != context.AppUserId)
        {
            await transaction.RollbackAsync(cancellationToken);
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
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(invalid.Value, null, proposal.ToDomain());
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
                return await FindConfirmationReplayAsync(
                        context,
                        idempotencyKey,
                        intent,
                        cancellationToken)
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
            await database.SaveChangesAsync(cancellationToken);
            if (faultInjector is not null)
            {
                await faultInjector.OnCheckpointAsync(
                    ProposalConfirmationCheckpoint.BeforeCommit,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            if (faultInjector is not null)
            {
                await faultInjector.OnCheckpointAsync(
                    ProposalConfirmationCheckpoint.AfterCommit,
                    cancellationToken);
            }

            return new(ProposalTransitionOutcome.Applied, task, proposal.ToDomain());
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return await FindConfirmationReplayAsync(
                    context,
                    idempotencyKey,
                    intent,
                    cancellationToken)
                ?? new(ProposalTransitionOutcome.Conflict, null, null);
        }
    }

    public async ValueTask AppendAsync(
        ProposalAuditEntry entry,
        CancellationToken cancellationToken)
    {
        var context = RequireCurrent(entry.TenantId, entry.ActorId, OpenLoopsPolicy.Purpose);
        EnsureCurrent(context);
        database.ProposalAudits.Add(ProposalAuditRecord.FromDomain(entry));
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<ProposalTransitionResult?> FindTransitionReplayAsync(
        TenantPrincipalContext context,
        string idempotencyKey,
        string intent,
        CancellationToken cancellationToken)
    {
        var receipt = await FindReceiptAsync(context, idempotencyKey, cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        var proposal = await database.Proposals
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == receipt.ProposalId, cancellationToken);
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
        var receipt = await FindReceiptAsync(context, idempotencyKey, cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        var proposal = await database.Proposals
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == receipt.ProposalId, cancellationToken);
        OpenLoopTask? task = null;
        if (receipt.TaskId.HasValue)
        {
            task = await database.OpenLoopTasks
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == receipt.TaskId.Value, cancellationToken);
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

public enum ProposalConfirmationCheckpoint
{
    BeforeCommit,
    AfterCommit,
}

public interface IProposalConfirmationFaultInjector
{
    ValueTask OnCheckpointAsync(
        ProposalConfirmationCheckpoint checkpoint,
        CancellationToken cancellationToken);
}

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
